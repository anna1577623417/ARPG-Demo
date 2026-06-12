#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using GraphProcessor;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

/// <summary>
/// 169.2/169.3 — Edge Inspector 诊断：仅事件触发 Log（边切换 / 字段提交 / 异常），禁止 Draw 每帧刷屏。
/// 开关：Graph 工具栏「Insp Dbg」。
/// </summary>
public static class CombatFlowGraphInspectorDiagnostics
{
    public const string PrefKey = "CombatFlowGraph.DebugInspector";
    public const string Prefix = "[CombatFlowGraph][Insp]";

    const int RingCapacity = 16;

    static readonly List<string> s_ring = new(RingCapacity);
    static string s_lastLoggedCommitGuid;
    static string s_lastLoggedSyncAfterGuid;

    public static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, false);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    public static void LogEvent(string message)
    {
        if (!Enabled)
        {
            return;
        }

        var line = $"{Prefix} {message}";
        Debug.Log(line);
        PushRing(line);
    }

    /// <summary>用户点击另一条边（仅 GUID 变化时一条）。</summary>
    public static void LogEdgeCommit(
        SerializableEdge previousEdge,
        SerializableEdge newEdge,
        float metaLateSeconds)
    {
        if (!Enabled || newEdge == null)
        {
            return;
        }

        var prevGuid = previousEdge != null ? previousEdge.GUID : "(none)";
        var nextGuid = newEdge.GUID;
        if (prevGuid == nextGuid)
        {
            return;
        }

        if (nextGuid == s_lastLoggedCommitGuid)
        {
            return;
        }

        s_lastLoggedCommitGuid = nextGuid;
        LogEvent(
            $"SELECT {ShortGuid(prevGuid)}→{ShortGuid(nextGuid)} {FormatConnection(newEdge)} metaLate={metaLateSeconds:F3}");
    }

    /// <summary>GraphView 延迟同步（仅 Committed GUID 变化或 mismatch 时一条）。</summary>
    public static void LogSelectionSync(
        in CombatFlowGraphSelectionController.Snapshot before,
        in CombatFlowGraphSelectionController.Snapshot after,
        BaseGraphView graphView)
    {
        if (!Enabled)
        {
            return;
        }

        var beforeGuid = before.Edge != null ? before.Edge.GUID : "(none)";
        var afterGuid = after.Edge != null ? after.Edge.GUID : "(none)";

        var gvEdgeGuid = "(none)";
        if (graphView?.selection != null)
        {
            foreach (var sel in graphView.selection)
            {
                if (sel is EdgeView ev
                    && CombatFlowGraphEdgeSelectionUtility.TryGetSerializableEdge(ev, out var se)
                    && se != null)
                {
                    gvEdgeGuid = se.GUID;
                }
            }
        }

        if (afterGuid == beforeGuid && (after.Edge == null || gvEdgeGuid == afterGuid))
        {
            return;
        }

        if (afterGuid == s_lastLoggedSyncAfterGuid && gvEdgeGuid == afterGuid)
        {
            return;
        }

        s_lastLoggedSyncAfterGuid = afterGuid;
        LogEvent(
            $"SYNC before={ShortGuid(beforeGuid)} after={ShortGuid(afterGuid)} gv={ShortGuid(gvEdgeGuid)}");

        if (after.Edge != null && gvEdgeGuid != afterGuid)
        {
            LogEvent(
                $"WARN gv≠committed gv={ShortGuid(gvEdgeGuid)} committed={ShortGuid(afterGuid)}");
        }
    }

    /// <summary>Inspector 绘制切换到另一条边（每切一次一条，合并 focus / meta / 异常）。</summary>
    public static void LogEdgeSwitch(
        SerializableEdge drawEdge,
        SerializableEdge committedEdge,
        CombatFlowProcessorEdgeMeta meta,
        string previousStaticGuid,
        bool focusCleared)
    {
        if (!Enabled || drawEdge == null)
        {
            return;
        }

        var drawGuid = drawEdge.GUID;
        var metaLate = meta != null ? meta.Authoring.LateWindowSeconds : -1f;
        var focused = GUI.GetNameOfFocusedControl() ?? "(none)";
        var editingTf = EditorGUIUtility.editingTextField;
        var focusOnOtherEdge = !string.IsNullOrEmpty(focused)
            && focused.StartsWith("CombatFlow.LateWindow.")
            && focused != "CombatFlow.LateWindow." + drawGuid;

        LogEvent(
            $"SWITCH {ShortGuid(previousStaticGuid)}→{ShortGuid(drawGuid)} metaLate={metaLate:F3} " +
            $"focusCleared={focusCleared} editingTF={editingTf} focused={focused}");

        if (committedEdge != null && drawGuid != committedEdge.GUID)
        {
            LogEvent(
                $"WARN draw≠committed draw={ShortGuid(drawGuid)} committed={ShortGuid(committedEdge.GUID)}");
        }

        if (meta != null && meta.EdgeGuid != drawGuid)
        {
            LogEvent($"WARN metaGuid≠edge meta={ShortGuid(meta.EdgeGuid)} edge={ShortGuid(drawGuid)}");
        }

        if (focusOnOtherEdge || (editingTf && focused.StartsWith("CombatFlow.LateWindow.")))
        {
            LogEvent(
                $"STALE-FOCUS drawing={ShortGuid(drawGuid)} metaLate={metaLate:F3} " +
                $"stillFocused={focused} (未 Enter 切边 → IMGUI 编辑缓存残留)");
        }
    }

    /// <summary>LateWindow 切边 flush 提交（EndChangeCheck 成功时一条）。</summary>
    public static void LogLateWindowSaved(SerializableEdge edge, float beforeLate, float afterLate)
    {
        if (!Enabled || edge == null || Mathf.Abs(beforeLate - afterLate) < 0.0001f)
        {
            return;
        }

        LogEvent(
            $"SAVE-LATE {ShortGuid(edge.GUID)} {beforeLate:F3}→{afterLate:F3} {FormatConnection(edge)}");
    }

    public static void LogLateWindowSavedFromFlush(string edgeGuid, float beforeLate, float afterLate)
    {
        if (!Enabled || Mathf.Abs(beforeLate - afterLate) < 0.0001f)
        {
            return;
        }

        LogEvent($"SAVE-LATE-FLUSH {ShortGuid(edgeGuid)} {beforeLate:F3}→{afterLate:F3}");
    }

    public static void DrawDebugFoldout(
        SerializableEdge serialEdge,
        CombatFlowProcessorEdgeMeta meta,
        SerializableEdge committedEdge,
        float localLateSeconds)
    {
        if (!Enabled)
        {
            EditorGUILayout.HelpBox("Insp Dbg 关闭。工具栏开启后仅事件 Log（边切换/保存/异常），不刷 Draw。", MessageType.None);
            return;
        }

        EditorGUILayout.LabelField("Live（不刷 Console）", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField("Edge", serialEdge != null ? ShortGuid(serialEdge.GUID) : "(null)", EditorStyles.miniLabel);
        EditorGUILayout.LabelField(
            "MetaLate / LocalLate",
            $"{(meta != null ? meta.Authoring.LateWindowSeconds : -1f):F3} / {localLateSeconds:F3}",
            EditorStyles.miniLabel);
        EditorGUILayout.LabelField("Focus", GUI.GetNameOfFocusedControl() ?? "(none)", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("editingTF", EditorGUIUtility.editingTextField.ToString(), EditorStyles.miniLabel);

        EditorGUILayout.Space(4);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Copy Buffer", GUILayout.Height(22f)))
            {
                EditorGUIUtility.systemCopyBuffer = BuildRingText();
            }

            if (GUILayout.Button("Clear", GUILayout.Height(22f)))
            {
                s_ring.Clear();
                s_lastLoggedCommitGuid = null;
                s_lastLoggedSyncAfterGuid = null;
            }
        }

        EditorGUILayout.TextArea(BuildRingText(), GUILayout.MaxHeight(100f));
    }

    static void PushRing(string line)
    {
        s_ring.Add(line);
        while (s_ring.Count > RingCapacity)
        {
            s_ring.RemoveAt(0);
        }
    }

    static string BuildRingText()
    {
        if (s_ring.Count == 0)
        {
            return "(empty — 复现：A 改 Late 不按 Enter → 点 B；预期 ≤5 条 SELECT/SWITCH/STALE-FOCUS)";
        }

        var sb = new StringBuilder(s_ring.Count * 80);
        for (var i = 0; i < s_ring.Count; i++)
        {
            sb.AppendLine(s_ring[i]);
        }

        return sb.ToString();
    }

    static string ShortGuid(string guid)
    {
        if (string.IsNullOrEmpty(guid) || guid == "(none)" || guid == "(null)")
        {
            return guid ?? "(null)";
        }

        return guid.Length <= 8 ? guid : guid.Substring(0, 8);
    }

    static string FormatConnection(SerializableEdge edge)
    {
        if (edge == null)
        {
            return "(null)";
        }

        var from = edge.outputNode != null ? edge.outputNode.name : "?";
        var to = edge.inputNode != null ? edge.inputNode.name : "?";
        return $"{from}→{to}";
    }
}

internal static class CombatFlowGraphEdgeInspectorContext
{
    internal static SerializableEdge CommittedEdge;
}
#endif

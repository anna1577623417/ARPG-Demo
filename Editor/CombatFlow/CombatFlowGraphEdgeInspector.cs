#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using GraphProcessor;
using UnityEditor;
using UnityEngine;

/// <summary>147.1 / 150.3 / 152.1 / 153.2 — Flow / Interrupt 边 Inspector。</summary>
public static class CombatFlowGraphEdgeInspector
{
    const string PrefFoldBasic = "CombatFlow.Inspector.Fold.Basic";
    const string PrefFoldRouting = "CombatFlow.Inspector.Fold.Routing";
    const string PrefFoldTiming = "CombatFlow.Inspector.Fold.Timing";
    const string PrefFoldConditions = "CombatFlow.Inspector.Fold.Conditions";
    const string PrefFoldDebug = "CombatFlow.Inspector.Fold.Debug";

    static readonly List<string> s_inlineErrors = new List<string>(4);

    // 168.2 / 169.3：跟踪上一次绘制的 Edge GUID。
    static string s_lastEdgeGuid;

    // 169.3：LateWindow 编辑文本缓存（切边/失焦时提交，避免 IMGUI flush 布局不对称）。
    static readonly Dictionary<string, string> s_lateWindowTextByGuid = new();
    static bool s_forceFocusClearOnNextRepaint;

    const string LateWindowControlPrefix = "CombatFlow.LateWindow.";

    /// <summary>169.2 诊断只读：EdgeInspector 静态 last GUID。</summary>
    internal static string DebugLastEdgeGuid => s_lastEdgeGuid;

    static string LateWindowControlName(string edgeGuid) => LateWindowControlPrefix + edgeGuid;

    static string FormatLateSeconds(float seconds) =>
        seconds.ToString("0.###", CultureInfo.InvariantCulture);

    static bool TryParseLateSeconds(string text, out float seconds)
    {
        return float.TryParse(
            text,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out seconds);
    }

    /// <summary>切走边之前：将缓存中的 LateWindow 文本写入 meta。</summary>
    public static void CommitLateWindowIfDirty(CombatFlowProcessorGraph graph, string edgeGuid)
    {
        if (graph == null || string.IsNullOrEmpty(edgeGuid))
        {
            return;
        }

        if (!s_lateWindowTextByGuid.TryGetValue(edgeGuid, out var text))
        {
            return;
        }

        if (!TryParseLateSeconds(text, out var parsed))
        {
            return;
        }

        ApplyLateWindowToMeta(graph, edgeGuid, parsed);
    }

    static void ApplyLateWindowToMeta(CombatFlowProcessorGraph graph, string edgeGuid, float parsed)
    {
        var meta = graph.GetOrCreateEdgeMeta(edgeGuid);
        var lateBefore = meta.Authoring.LateWindowSeconds;
        var clamped = Mathf.Max(0f, parsed);
        if (Mathf.Approximately(lateBefore, clamped))
        {
            s_lateWindowTextByGuid.Remove(edgeGuid);
            return;
        }

        var auth = meta.Authoring;
        auth.LateWindowSeconds = clamped;
        meta.Authoring = auth;
        EditorUtility.SetDirty(graph);
        s_lateWindowTextByGuid.Remove(edgeGuid);
        CombatFlowGraphInspectorDiagnostics.LogLateWindowSavedFromFlush(edgeGuid, lateBefore, clamped);
    }

    static void ClearImguiEditFocus()
    {
        GUIUtility.keyboardControl = 0;
        EditorGUIUtility.editingTextField = false;
        GUI.FocusControl(null);
    }

    /// <summary>GraphView 点选切边时调用（IMGUI 外）；下一帧 Repaint 清焦点。</summary>
    public static void ClearImguiEditFocusForSelectionChange()
    {
        s_forceFocusClearOnNextRepaint = true;
    }

    static void SyncLateWindowDisplayText(string edgeGuid, float lateSeconds)
    {
        s_lateWindowTextByGuid[edgeGuid] = FormatLateSeconds(lateSeconds);
    }

    static void DrawLateWindowField(
        SerializableEdge serialEdge,
        ref CombatFlowEdgeAuthoring edge,
        CombatFlowProcessorEdgeMeta meta,
        CombatFlowProcessorGraph processorGraph)
    {
        var guid = serialEdge.GUID;
        var controlName = LateWindowControlName(guid);
        var isFocused = GUI.GetNameOfFocusedControl() == controlName && EditorGUIUtility.editingTextField;

        if (!s_lateWindowTextByGuid.TryGetValue(guid, out var text) || !isFocused)
        {
            text = FormatLateSeconds(edge.LateWindowSeconds);
            if (!isFocused)
            {
                s_lateWindowTextByGuid[guid] = text;
            }
        }

        GUI.SetNextControlName(controlName);
        EditorGUI.BeginChangeCheck();
        var newText = EditorGUILayout.TextField(
            new GUIContent("Late Window (s)", "失焦或 Enter 提交；切边时自动保存。"),
            text);
        s_lateWindowTextByGuid[guid] = newText;

        if (!EditorGUI.EndChangeCheck() || !TryParseLateSeconds(newText, out var parsed))
        {
            return;
        }

        var clamped = Mathf.Max(0f, parsed);
        if (Mathf.Approximately(edge.LateWindowSeconds, clamped))
        {
            return;
        }

        var lateBefore = edge.LateWindowSeconds;
        edge.LateWindowSeconds = clamped;
        meta.Authoring = edge;
        EditorUtility.SetDirty(processorGraph);
        SyncLateWindowDisplayText(guid, clamped);
        CombatFlowGraphInspectorDiagnostics.LogLateWindowSaved(serialEdge, lateBefore, clamped);
    }

    public static bool Draw(
        CombatGraphAsset ownerAsset,
        CombatFlowProcessorGraph processorGraph,
        SerializableEdge serialEdge)
    {
        if (processorGraph == null || serialEdge == null)
        {
            return false;
        }

        var prevLabel = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = CombatFlowGraphInspectorLayout.LabelWidth;

        var meta = processorGraph.GetOrCreateEdgeMeta(serialEdge.GUID);
        var edge = meta.Authoring;
        var prevStaticGuid = s_lastEdgeGuid;
        var guidChanged = !string.IsNullOrEmpty(prevStaticGuid) && serialEdge.GUID != prevStaticGuid;

        if (s_forceFocusClearOnNextRepaint
            && Event.current != null
            && Event.current.type == EventType.Repaint)
        {
            ClearImguiEditFocus();
            s_forceFocusClearOnNextRepaint = false;
        }

        if (guidChanged)
        {
            CommitLateWindowIfDirty(processorGraph, prevStaticGuid);
        }

        // 169.3：s_lastEdgeGuid 只能在 Repaint 更新；Layout 提前写入会导致同帧 Repaint 不再清焦点 → STALE-FOCUS。
        if (guidChanged && Event.current != null && Event.current.type == EventType.Repaint)
        {
            ClearImguiEditFocus();
            s_lastEdgeGuid = serialEdge.GUID;
            SyncLateWindowDisplayText(serialEdge.GUID, edge.LateWindowSeconds);
            CombatFlowGraphInspectorDiagnostics.LogEdgeSwitch(
                serialEdge,
                CombatFlowGraphEdgeInspectorContext.CommittedEdge,
                meta,
                prevStaticGuid,
                focusCleared: true);
        }
        else if (string.IsNullOrEmpty(s_lastEdgeGuid)
            && Event.current != null
            && Event.current.type == EventType.Repaint)
        {
            s_lastEdgeGuid = serialEdge.GUID;
            SyncLateWindowDisplayText(serialEdge.GUID, edge.LateWindowSeconds);
        }

        EditorGUILayout.LabelField(
            edge.EdgeKind == CombatFlowEdgeKind.Interrupt ? "Interrupt Edge" : "Flow Edge",
            EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        if (DrawFoldoutSection(PrefFoldBasic, "Basic", true))
        {
            edge.Transition = (CombatFlowTransitionMode)EditorGUILayout.EnumPopup("Transition", edge.Transition);
            edge.EdgeKind = (CombatFlowEdgeKind)EditorGUILayout.EnumPopup("Edge Kind", edge.EdgeKind);
            DrawEdgeKindSemantics(edge.EdgeKind);
            edge.Label = EditorGUILayout.TextField("Label", edge.Label);
            edge.Priority = EditorGUILayout.IntField("Priority", edge.Priority);
            EndFoldoutSection();
            CombatFlowGraphInspectorLayout.SectionGap();
        }

        if (DrawFoldoutSection(PrefFoldRouting, "Routing", true))
        {
            edge.TargetRoute = (SkillRouteDefinition)EditorGUILayout.ObjectField(
                "Target Route",
                edge.TargetRoute,
                typeof(SkillRouteDefinition),
                false);

            DrawRouteEntryPreview(edge.TargetRoute);
            DrawLiveConnectionHint(serialEdge);

            if (edge.Transition == CombatFlowTransitionMode.OnInput)
            {
                EditorGUILayout.LabelField("Context Input", EditorStyles.miniBoldLabel);
                edge.InputSlot = (SkillEntrySlot)EditorGUILayout.EnumPopup("Input Slot", edge.InputSlot);
                edge.InputSemantic = (InputSemanticType)EditorGUILayout.EnumPopup("Input Semantic", edge.InputSemantic);
            }

            if (edge.Transition == CombatFlowTransitionMode.OnSegmentComplete && edge.TargetRoute != null)
            {
                DrawComboAdvanceHint(ownerAsset, edge.TargetRoute);
            }

            if (serialEdge.outputNode is CombatFlowStartNode && edge.Transition == CombatFlowTransitionMode.OnInput)
        {
            EditorGUILayout.HelpBox(
                "【ComboChain】Start 上的 OnInput 起手易导致连点重复首段。\n" +
                "推荐：LM Entry.NormalRoute 起手；Start→ComboA 改为 OnSegmentComplete（Target Route 留空）；\n" +
                "ComboA→ComboB→… 再用 OnInput + Flow 边 + 各段 NormalRoute。",
                MessageType.Warning);
        }

        DrawInlineEdgeKindValidation(ownerAsset, serialEdge, in edge);

            EndFoldoutSection();
            CombatFlowGraphInspectorLayout.SectionGap();
        }

        if (DrawFoldoutSection(PrefFoldTiming, "Timing", true))
        {
            DrawEarlyWindowProjection(serialEdge);
            EditorGUILayout.Space(4);

            if (edge.Transition == CombatFlowTransitionMode.OnInput)
            {
                DrawLateWindowField(serialEdge, ref edge, meta, processorGraph);
            }

            EndFoldoutSection();
            CombatFlowGraphInspectorLayout.SectionGap();
        }

        if (DrawFoldoutSection(PrefFoldConditions, "Conditions", true))
        {
            CombatFlowEdgeConditionsDrawer.Draw(ref edge, ownerAsset?.ConditionPool);
            EndFoldoutSection();
            CombatFlowGraphInspectorLayout.SectionGap();
        }

        if (DrawFoldoutSection(PrefFoldDebug, "Debug", CombatFlowGraphInspectorDiagnostics.Enabled))
        {
            EditorGUILayout.LabelField("Edge GUID", serialEdge.GUID, EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"Connect: {serialEdge.outputNode?.name} → {serialEdge.inputNode?.name}",
                EditorStyles.miniLabel);
            CombatFlowGraphInspectorDiagnostics.DrawDebugFoldout(
                serialEdge,
                meta,
                CombatFlowGraphEdgeInspectorContext.CommittedEdge,
                edge.LateWindowSeconds);
            EndFoldoutSection();
        }

        var changed = EditorGUI.EndChangeCheck();
        if (changed)
        {
            var lateBefore = meta.Authoring.LateWindowSeconds;
            meta.Authoring = edge;
            CombatFlowGraphInspectorDiagnostics.LogLateWindowSaved(serialEdge, lateBefore, edge.LateWindowSeconds);
            EditorUtility.SetDirty(processorGraph);
        }

        EditorGUIUtility.labelWidth = prevLabel;
        return changed;
    }

    public static void DrawMultiSelectionSummary(in CombatFlowGraphSelectionController.Snapshot snap)
    {
        EditorGUILayout.LabelField(snap.Summary, EditorStyles.boldLabel);
        if (snap.Edge != null && snap.Kind == CombatFlowInspectorTargetKind.Multi)
        {
            EditorGUILayout.LabelField(
                $"含边：{CombatFlowGraphSelectionClassifier.FormatEdgeConnection(snap.Edge)}",
                EditorStyles.miniLabel);
        }

        if (snap.NodeCount > 0)
        {
            EditorGUILayout.LabelField($"Nodes: {snap.NodeCount}", EditorStyles.miniLabel);
        }

        if (snap.EdgeCount > 0)
        {
            EditorGUILayout.LabelField($"Edges: {snap.EdgeCount}", EditorStyles.miniLabel);
        }

        EditorGUILayout.HelpBox(
            "多选时不编辑单条边属性。请单选一条边或一个节点。\n" +
            "框选多个对象时与此一致（同 Animator）。",
            MessageType.Info);
    }

    static void DrawEdgeKindSemantics(CombatFlowEdgeKind kind)
    {
        if (kind == CombatFlowEdgeKind.Flow)
        {
            EditorGUILayout.HelpBox(
                "【Flow】连招边：To 须为 ActionNode；Route 入口 Action 须与 To 节点一致。\n" +
                "Normal / Derivative 单入口 Route 可用；MultiStage 请改用 Interrupt→End。",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "【Interrupt】打断边：Route 藏在边内；To 须为 End 节点。\n" +
                "用于取消、派生、MultiStage 技能入口。",
                MessageType.Info);
        }
    }

    static void DrawRouteEntryPreview(SkillRouteDefinition route)
    {
        if (route == null)
        {
            return;
        }

        EditorGUILayout.LabelField("Route → Entry Action", EditorStyles.miniBoldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.EnumPopup("Graph Type", route.GraphType);
        }

        if (route.TryResolveGraphEntryAction(out var entryAction, out var error))
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Resolved Entry", entryAction, typeof(ActionDataSO), false);
            }

            if (route is NormalRouteDefinition normal && normal.IsSingleStageForGraph)
            {
                EditorGUILayout.LabelField("NormalRoute · single stage OK", EditorStyles.miniLabel);
            }
        }
        else
        {
            EditorGUILayout.HelpBox(error ?? "无法解析入口 Action", MessageType.Warning);
        }
    }

    static void DrawLiveConnectionHint(SerializableEdge serialEdge)
    {
        if (serialEdge.inputNode is CombatFlowActionNode toAction)
        {
            var actionName = toAction.action != null ? toAction.action.name : "(null)";
            EditorGUILayout.LabelField($"To ActionNode: {toAction.nodeId} → {actionName}", EditorStyles.miniLabel);
        }
        else if (serialEdge.inputNode is CombatFlowEndNode end)
        {
            EditorGUILayout.LabelField($"To End: {end.nodeId}", EditorStyles.miniLabel);
        }
        else if (serialEdge.inputNode != null)
        {
            EditorGUILayout.LabelField($"To: {serialEdge.inputNode.name} ({serialEdge.inputNode.GetType().Name})", EditorStyles.miniLabel);
        }
    }

    static void DrawInlineEdgeKindValidation(
        CombatGraphAsset ownerAsset,
        SerializableEdge serialEdge,
        in CombatFlowEdgeAuthoring edge)
    {
        var nodesById = ownerAsset?.Nodes != null
            ? CombatFlowEdgeKindRules.BuildNodeLookup(ownerAsset.Nodes)
            : new Dictionary<string, CombatFlowNodeAuthoring>();

        var validationEdge = edge;
        ApplyLiveConnection(serialEdge, nodesById, ref validationEdge);

        s_inlineErrors.Clear();
        CombatFlowEdgeKindRules.CollectErrors(
            in validationEdge,
            nodesById,
            CombatFlowEdgeKindRules.FormatConnection(in validationEdge, nodesById),
            s_inlineErrors);

        for (var i = 0; i < s_inlineErrors.Count; i++)
        {
            EditorGUILayout.HelpBox(s_inlineErrors[i], MessageType.Error);
        }

        if (s_inlineErrors.Count == 0 && validationEdge.TargetRoute != null)
        {
            if (validationEdge.EdgeKind == CombatFlowEdgeKind.Flow
                && validationEdge.TargetRoute.TryResolveGraphEntryAction(out var entry, out _)
                && TryGetLiveToAction(serialEdge, out var toAction)
                && toAction == entry)
            {
                EditorGUILayout.HelpBox(
                    $"Flow 合法：Route「{validationEdge.TargetRoute.name}」→ {entry.name} 与 To 节点一致。",
                    MessageType.Info);
            }
            else if (validationEdge.EdgeKind == CombatFlowEdgeKind.Interrupt
                     && serialEdge.inputNode is CombatFlowEndNode)
            {
                EditorGUILayout.HelpBox(
                    "Interrupt 合法：Route 藏在边内，To 已接 End。",
                    MessageType.Info);
            }
        }
    }

    static void ApplyLiveConnection(
        SerializableEdge serialEdge,
        Dictionary<string, CombatFlowNodeAuthoring> nodesById,
        ref CombatFlowEdgeAuthoring edge)
    {
        if (TryResolveLiveNode(serialEdge.outputNode, out var fromId, out _, out _))
        {
            edge.FromNodeId = fromId;
        }

        if (!TryResolveLiveNode(serialEdge.inputNode, out var toId, out var toKind, out var toAction))
        {
            return;
        }

        edge.ToNodeId = toId;
        nodesById[toId] = new CombatFlowNodeAuthoring
        {
            NodeId = toId,
            Kind = toKind,
            Action = toAction,
        };
    }

    static bool TryResolveLiveNode(
        BaseNode node,
        out string nodeId,
        out CombatFlowNodeKind kind,
        out ActionDataSO action)
    {
        nodeId = null;
        kind = default;
        action = null;
        if (node == null)
        {
            return false;
        }

        switch (node)
        {
            case CombatFlowStartNode start:
                nodeId = string.IsNullOrEmpty(start.nodeId) ? "Start" : start.nodeId;
                kind = CombatFlowNodeKind.Start;
                return true;
            case CombatFlowActionNode actionNode:
                nodeId = string.IsNullOrEmpty(actionNode.nodeId) ? "Action" : actionNode.nodeId;
                kind = CombatFlowNodeKind.FlowAction;
                action = actionNode.action;
                return true;
            case CombatFlowRouteSwitchNode routeNode:
                nodeId = string.IsNullOrEmpty(routeNode.nodeId) ? "Route" : routeNode.nodeId;
                kind = CombatFlowNodeKind.RouteSwitch;
                return true;
            case CombatFlowEndNode end:
                nodeId = string.IsNullOrEmpty(end.nodeId) ? "Idle" : end.nodeId;
                kind = CombatFlowNodeKind.End;
                return true;
            default:
                return false;
        }
    }

    static bool TryGetLiveToAction(SerializableEdge serialEdge, out ActionDataSO action)
    {
        action = serialEdge.inputNode is CombatFlowActionNode toNode ? toNode.action : null;
        return action != null;
    }

    static void DrawEarlyWindowProjection(SerializableEdge serialEdge)
    {
        EditorGUILayout.LabelField("Early Window (read-only)", EditorStyles.miniBoldLabel);

        if (serialEdge?.outputNode is not CombatFlowActionNode actionNode || actionNode.action == null)
        {
            EditorGUILayout.HelpBox(
                "Early 来自 From 节点 Action 的 ActionWindow（Interruptible By Categories）。\n" +
                "From 非 FlowAction 或无 Action 时不显示。",
                MessageType.Info);
            return;
        }

        var action = actionNode.action;
        var logicDur = action.ResolveLogicalDurationSeconds();
        var windows = action.Windows;
        if (windows == null || windows.Count == 0)
        {
            EditorGUILayout.HelpBox($"{action.name} 无 ActionWindow。", MessageType.Warning);
            return;
        }

        var any = false;
        for (var i = 0; i < windows.Count; i++)
        {
            var w = windows[i];
            if (w.InterruptibleByCategories == ActionCategory.None)
            {
                continue;
            }

            any = true;
            var absStart = w.NormalizedStart * logicDur;
            var absEnd = w.NormalizedEnd * logicDur;
            var catText = CombatFlowGraphInspectorLayout.FormatActionCategories(w.InterruptibleByCategories);
            var catCount = CombatFlowGraphInspectorLayout.CountActionCategories(w.InterruptibleByCategories);
            var line = catCount > 1
                ? $"  #{i} {w.NormalizedStart:P0}~{w.NormalizedEnd:P0}  ({absStart:F2}~{absEnd:F2}s)  {catCount} cats: {catText}"
                : $"  #{i} {w.NormalizedStart:P0}~{w.NormalizedEnd:P0}  ({absStart:F2}~{absEnd:F2}s)  cat={catText}";

            var content = new GUIContent(line, catText);
            var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(rect, content, EditorStyles.miniLabel);
        }

        if (!any)
        {
            EditorGUILayout.HelpBox(
                $"{action.name} 无 InterruptibleByCategories 窗口；Cancel 需配 Movement/Offense 等类别。",
                MessageType.Warning);
        }
    }

    static bool DrawFoldoutSection(string prefsKey, string title, bool defaultExpanded)
    {
        var expanded = EditorPrefs.GetBool(prefsKey, defaultExpanded);
        expanded = EditorGUILayout.Foldout(expanded, title, true, EditorStyles.foldoutHeader);
        EditorPrefs.SetBool(prefsKey, expanded);
        if (!expanded)
        {
            return false;
        }

        EditorGUI.indentLevel++;
        return true;
    }

    static void EndFoldoutSection()
    {
        EditorGUI.indentLevel--;
    }

    static void DrawComboAdvanceHint(CombatGraphAsset ownerAsset, SkillRouteDefinition targetRoute)
    {
        if (ownerAsset?.RoutePool == null)
        {
            return;
        }

        for (var i = 0; i < ownerAsset.RoutePool.Length; i++)
        {
            if (ownerAsset.RoutePool[i] is not ComboRouteDefinition combo || !combo.ContainsSubRoute(targetRoute))
            {
                continue;
            }

            var msg = combo.AllowFlowSegmentAdvance
                ? $"{combo.name}.AllowFlowSegmentAdvance=true — 运行时允许段后 Flow 施放。"
                : $"{combo.name}.AllowFlowSegmentAdvance=false — 编译/运行时将 BLOCK 此边。";
            EditorGUILayout.HelpBox(msg, combo.AllowFlowSegmentAdvance ? MessageType.Info : MessageType.Warning);
            return;
        }
    }
}
#endif

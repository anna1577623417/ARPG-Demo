#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 178.1 W7+W8 — Transition Analyzer：单对比对 + 全库审计。
/// </summary>
public sealed class TransitionAnalyzerWindow : EditorWindow
{
    [MenuItem("GameMain/Animation/Transition Analyzer")]
    public static void Open() => GetWindow<TransitionAnalyzerWindow>("Transition Analyzer");

    TransitionTagMatrixSO m_matrix;
    TransitionDatabaseSO m_clipDb;
    TransitionConfigSO m_config;
    ActionDataSO m_from;
    ActionDataSO m_to;
    Vector2 m_auditScroll;
    string m_auditReport;

    AnimationTransitionService _service;

    AnimationTransitionService Service =>
        _service ??= new AnimationTransitionService(m_matrix, m_clipDb, m_config);

    void OnGUI()
    {
        EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);
        var prevMatrix = m_matrix;
        m_matrix = (TransitionTagMatrixSO)EditorGUILayout.ObjectField(
            "Matrix", m_matrix, typeof(TransitionTagMatrixSO), false);
        m_clipDb = (TransitionDatabaseSO)EditorGUILayout.ObjectField(
            "Clip DB (可选)", m_clipDb, typeof(TransitionDatabaseSO), false);
        m_config = (TransitionConfigSO)EditorGUILayout.ObjectField(
            "Config", m_config, typeof(TransitionConfigSO), false);

        if (m_matrix != prevMatrix || _service == null)
        {
            _service = new AnimationTransitionService(m_matrix, m_clipDb, m_config);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Single Pair", EditorStyles.boldLabel);
        m_from = (ActionDataSO)EditorGUILayout.ObjectField("From", m_from, typeof(ActionDataSO), false);
        m_to = (ActionDataSO)EditorGUILayout.ObjectField("To", m_to, typeof(ActionDataSO), false);

        if (m_to != null)
        {
            var p = Service.Resolve(m_from, m_to);
            EditorGUILayout.HelpBox(
                $"Resolved:\n" +
                $"  Layer:    {p.Layer}\n" +
                $"  Mode:     {p.Mode}\n" +
                $"  Blend:    {p.BlendDuration:F3}s\n" +
                $"  EaseCurve:{(p.EaseCurve == null ? "<none>" : "<custom>")}",
                MessageType.None);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Library Audit", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(m_matrix == null))
        {
            if (GUILayout.Button("Run Full-Library Audit", GUILayout.Height(24)))
            {
                RunLibraryAudit();
            }
        }

        if (!string.IsNullOrEmpty(m_auditReport))
        {
            m_auditScroll = EditorGUILayout.BeginScrollView(m_auditScroll, GUILayout.Height(280));
            EditorGUILayout.TextArea(m_auditReport, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
    }

    void RunLibraryAudit()
    {
        var actions = CollectAllActions();
        if (actions.Count == 0)
        {
            m_auditReport = "未找到任何 ActionDataSO。";
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"[Library Audit] {System.DateTime.Now:HH:mm}");
        sb.AppendLine($"Actions scanned: {actions.Count}");
        sb.AppendLine($"Pairs: {actions.Count * actions.Count}");
        sb.AppendLine();

        var red = 0;
        var yellow = 0;
        var green = 0;
        var details = new StringBuilder();

        // 仅扫所有 (i, j) 的 Resolve 结果做 sanity check（不依赖运行时 Animator）
        for (var i = 0; i < actions.Count; i++)
        {
            for (var j = 0; j < actions.Count; j++)
            {
                if (i == j) continue;
                var p = Service.Resolve(actions[i], actions[j]);

                // 🔴 Tag=Hit 但 Mode 不是 Snap → 错配
                if (actions[i].TransitionTag == TransitionTag.Hit
                    && p.Mode != TransitionMode.Snap
                    && p.BlendDuration > 0.0001f)
                {
                    red++;
                    if (red <= 20)
                    {
                        details.AppendLine($"🔴 {actions[i].name} → {actions[j].name}: Hit 应 Snap，实际 Blend={p.BlendDuration:F3} Mode={p.Mode}");
                    }
                    continue;
                }

                // 🟡 Blend > 0.30 → 推荐过长
                if (p.BlendDuration > 0.30f)
                {
                    yellow++;
                    if (yellow <= 20)
                    {
                        details.AppendLine($"🟡 {actions[i].name} → {actions[j].name}: Blend={p.BlendDuration:F3} (>{0.30:F2})");
                    }
                    continue;
                }

                green++;
            }
        }

        sb.AppendLine($"🔴 严重错配:   {red}");
        sb.AppendLine($"🟡 推荐过长:   {yellow}");
        sb.AppendLine($"🟢 正常:       {green}");
        sb.AppendLine();
        sb.AppendLine("─── Top 20 Detail ───");
        sb.Append(details);
        m_auditReport = sb.ToString();
    }

    static List<ActionDataSO> CollectAllActions()
    {
        var list = new List<ActionDataSO>();
        var guids = AssetDatabase.FindAssets("t:ActionDataSO");
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var a = AssetDatabase.LoadAssetAtPath<ActionDataSO>(path);
            if (a != null) list.Add(a);
        }
        return list;
    }
}
#endif

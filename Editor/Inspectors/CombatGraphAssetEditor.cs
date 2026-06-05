#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CombatGraphAsset))]
public sealed class CombatGraphAssetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var graph = (CombatGraphAsset)target;
        if (graph == null)
        {
            return;
        }

        DrawProcessorViewStatus(graph);
        if (GUILayout.Button("Open Graph Editor (GraphProcessor)", GUILayout.Height(26f)))
        {
            if (CombatFlowGraphSync.TryEnsureProcessorView(graph, out var err))
            {
                CombatFlowGraphWindow.Open(graph);
            }
            else if (!string.IsNullOrEmpty(err))
            {
                EditorUtility.DisplayDialog("Combat Flow Graph", err, "OK");
            }
        }

        Draw147Header(graph);
        EditorGUILayout.Space(6f);
        DrawDefaultInspector();
        EditorGUILayout.Space(8f);
        DrawCompileSection(graph);
        EditorGUILayout.Space(6f);
        DrawFlowPreview(graph);
    }

    static void DrawProcessorViewStatus(CombatGraphAsset graph)
    {
        if (graph.ProcessorView != null)
        {
            return;
        }

        if (!EditorUtility.IsPersistent(graph))
        {
            EditorGUILayout.HelpBox(
                "Graph 视图子资产需要先保存本 CombatGraphAsset（Ctrl+S），再点 Open Graph Editor。",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox(
            "尚未创建 Graph 视图子资产；点 Open Graph Editor 将自动创建。",
            MessageType.Info);
    }

    static void Draw147Header(CombatGraphAsset graph)
    {
        EditorGUILayout.HelpBox(
            "147.1 Combat Flow Authoring\n" +
            "· 资源池：Action / Route / Condition\n" +
            "· flowEdges：Conditions 内联 + ConditionRefs（AND）\n" +
            "· Combo 段后自动链：ComboRoute.AllowFlowSegmentAdvance（默认 false）\n" +
            "· 运行时只读 CompiledData — Open Graph Editor → Sync && Compile",
            MessageType.Info);

        var valid = graph.CompileValid;
        var msg = valid ? "已编译，Runner 可读 CompiledData" : "未编译或无效 — Play Mode 流转 OPEN";
        EditorGUILayout.HelpBox(msg, valid ? MessageType.None : MessageType.Warning);
    }

    void DrawCompileSection(CombatGraphAsset graph)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Validate", GUILayout.Height(24f)))
            {
                var result = CombatFlowGraphValidator.Validate(graph);
                EditorUtility.DisplayDialog("Combat Flow Validate", result.Summary, "OK");
            }

            if (GUILayout.Button("Validate && Compile", GUILayout.Height(24f)))
            {
                if (graph.ProcessorView != null)
                {
                    CombatFlowGraphSync.PushToAuthoring(graph, graph.ProcessorView);
                }
                else if (CombatFlowGraphSync.TryEnsureProcessorView(graph, out var err))
                {
                    CombatFlowGraphSync.PushToAuthoring(graph, graph.ProcessorView);
                }
                else if (!string.IsNullOrEmpty(err))
                {
                    EditorUtility.DisplayDialog("Combat Flow Graph", err, "OK");
                    return;
                }

                CombatFlowGraphCompiler.TryCompile(graph, out var report);
                EditorUtility.DisplayDialog("Combat Flow Compile", report, "OK");
            }
        }

        if (!string.IsNullOrEmpty(graph.LastCompileReport))
        {
            EditorGUILayout.LabelField("Compile Report", EditorStyles.miniBoldLabel);
            EditorGUILayout.TextArea(graph.LastCompileReport, EditorStyles.wordWrappedLabel);
        }
    }

    static void DrawFlowPreview(CombatGraphAsset graph)
    {
        var data = graph.CompiledData;
        if (data == null || data.IsEmpty)
        {
            return;
        }

        EditorGUILayout.LabelField("Compiled Flow Preview", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Start: {data.StartNodeId}  Idle: {data.IdleNodeId}", EditorStyles.miniLabel);

        if (data.Edges == null || data.Edges.Length == 0)
        {
            return;
        }

        for (var i = 0; i < data.Edges.Length; i++)
        {
            var e = data.Edges[i];
            EditorGUILayout.LabelField(
                $"#{i} pri={e.Priority} {e.FromNodeId}→{e.ToNodeId} [{e.Transition}] route={(e.TargetRoute != null ? e.TargetRoute.name : "—")}",
                EditorStyles.miniLabel);
        }
    }
}
#endif



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
            "149.3 Contextual Entry Resolution · 153.2 Flow/Interrupt\n" +
            "· Flow 边：Action→Action；Interrupt 边：Action→End（Route 藏边内）\n" +
            "· 解析优先级：Graph Edge > Derivative > Default Entry\n" +
            "· Miss Policy：动作中未命中边 Block / FallbackToEntry\n" +
            "· Skill Link Preview 见下方；Open Graph Editor → Validate && Compile",
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
                PushProcessorViewToAuthoring(graph);
                var result = CombatFlowGraphValidator.Validate(graph);
                EditorUtility.DisplayDialog("Combat Flow Validate", result.Summary, "OK");
            }

            if (GUILayout.Button("Validate && Compile", GUILayout.Height(24f)))
            {
                if (!PushProcessorViewToAuthoring(graph, out var pushErr))
                {
                    if (!string.IsNullOrEmpty(pushErr))
                    {
                        EditorUtility.DisplayDialog("Combat Flow Graph", pushErr, "OK");
                    }

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

    /// <summary>Graph 视图边属性（EdgeKind 等）写入 asset.flowEdges；Validate/Compile 前必须调用。</summary>
    static bool PushProcessorViewToAuthoring(CombatGraphAsset graph, out string error)
    {
        error = null;
        if (graph == null)
        {
            error = "graph=null";
            return false;
        }

        if (graph.ProcessorView != null)
        {
            CombatFlowGraphSync.PushToAuthoring(graph, graph.ProcessorView);
            return true;
        }

        if (CombatFlowGraphSync.TryEnsureProcessorView(graph, out error))
        {
            CombatFlowGraphSync.PushToAuthoring(graph, graph.ProcessorView);
            return true;
        }

        return false;
    }

    static void PushProcessorViewToAuthoring(CombatGraphAsset graph)
    {
        PushProcessorViewToAuthoring(graph, out _);
    }

    static void DrawFlowPreview(CombatGraphAsset graph)
    {
        CombatFlowChainPreviewDrawer.Draw(graph, foldout: false);
    }
}
#endif



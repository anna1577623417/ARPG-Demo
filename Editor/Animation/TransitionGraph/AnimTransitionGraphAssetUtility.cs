#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Editor-only persistence bridge. It records one graph asset and its compiler child, never a mirror graph.</summary>
public static class AnimTransitionGraphAssetUtility
{
    public static bool TryCompileAndPersist(AnimTransitionAuthoringGraph graph, out AnimTransitionGraphHealthReport report)
    {
        var previous = graph != null ? graph.CompiledGraph : null;
        if (!AnimTransitionGraphCompiler.TryCompile(graph, out var compiled, out report)) return false;
        if (graph == null || compiled == null) return false;

        Undo.RecordObject(graph, "Compile Animation Transition Graph");
        if (AssetDatabase.Contains(graph))
        {
            if (previous != null && AssetDatabase.IsSubAsset(previous)) Undo.DestroyObjectImmediate(previous);
            AssetDatabase.AddObjectToAsset(compiled, graph);
        }

        graph.EditorSetCompiledGraph(compiled, true, report.Summary);
        EditorUtility.SetDirty(compiled);
        EditorUtility.SetDirty(graph);
        return true;
    }

    public static void MarkAuthoringChanged(AnimTransitionAuthoringGraph graph, string undoName)
    {
        if (graph == null) return;
        Undo.RecordObject(graph, undoName ?? "Edit Animation Transition Graph");
        graph.MarkCompileRequired();
        EditorUtility.SetDirty(graph);
    }
}
#endif

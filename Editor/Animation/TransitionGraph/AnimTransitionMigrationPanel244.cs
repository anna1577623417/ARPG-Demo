#if UNITY_EDITOR
using UnityEngine.UIElements;

public sealed class AnimTransitionMigrationPanel244
{
    readonly Label label;
    public AnimTransitionMigrationPanel244(VisualElement parent)
    {
        label = new Label { name = "AnimTransitionMigrationStatus" };
        label.style.whiteSpace = WhiteSpace.Normal;
        parent.Add(label);
    }

    public void Refresh(AnimTransitionAuthoringGraph graph)
    {
        if (label == null) return;
        if (graph == null)
        {
            label.text = "Migration Preview: select a graph asset.";
            return;
        }

        if (graph.MigrationRequired)
        {
            label.text = "Migration Required · schema " + graph.SchemaVersion + " → " + AnimTransitionAuthoringGraph.CurrentSchemaVersion + " · Apply remains disabled.";
            return;
        }

        var report = AnimationTransitionLegacyBaselineAnalyzer244.AnalyzeGraph(graph);
        label.text = report.Summary + "\nNo automatic asset write.";
    }
}
#endif

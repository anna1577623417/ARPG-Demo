using UnityEngine.UIElements;

/// <summary>Maps 243.7 validation output to temporary UI text; it never mutates graph data.</summary>
public sealed class AnimTransitionValidationOverlay
{
    readonly Label target;

    public AnimTransitionValidationOverlay(Label targetLabel)
    {
        target = targetLabel;
    }

    public void Show(AnimTransitionGraphHealthReport report, int crossings = 0)
    {
        if (target == null) return;
        if (report == null)
        {
            target.text = "No validation result.";
            return;
        }

        var actionable = report.Issues.Count > 0 ? " · " + AnimTransitionValidationPresenter244.Describe(report.Issues[0]).Summary : string.Empty;
        target.text = report.Summary
            + " fanout=" + report.MaxFanOut + "/" + AnimTransitionGraphValidator.MaxFanOut
            + " depth=" + report.MaxDepth + "/" + AnimTransitionGraphValidator.MaxDepth
            + " crossings=" + crossings
            + " budget=" + AnimTransitionGraphValidator.MaxNodes + "/" + AnimTransitionGraphValidator.MaxEdges
            + actionable;
    }
}

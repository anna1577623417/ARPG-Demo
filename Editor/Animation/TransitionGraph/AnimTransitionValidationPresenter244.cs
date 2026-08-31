#if UNITY_EDITOR
using System;

public readonly struct AnimTransitionValidationAction244
{
    public readonly string Code;
    public readonly string Title;
    public readonly string Instruction;
    public readonly string NodeGuid;

    public AnimTransitionValidationAction244(string code, string title, string instruction, string nodeGuid)
    {
        Code = code ?? string.Empty;
        Title = title ?? string.Empty;
        Instruction = instruction ?? string.Empty;
        NodeGuid = nodeGuid ?? string.Empty;
    }

    public bool CanFocusNode => !string.IsNullOrEmpty(NodeGuid);
    public string Summary => string.IsNullOrEmpty(Instruction) ? Title : Title + ": " + Instruction;
}

/// <summary>Maps stable issue codes to actionable authoring guidance without guessing a fix.</summary>
public static class AnimTransitionValidationPresenter244
{
    public static AnimTransitionValidationAction244 Describe(AnimTransitionGraphIssue issue)
    {
        if (issue == null) return new AnimTransitionValidationAction244("", "No validation result", "Run Validate again.", "");
        var instruction = "Inspect the referenced node and correct the reported contract.";
        switch (issue.Code)
        {
            case "ATG003": instruction = "Give every node a unique GUID before compiling."; break;
            case "ATG004": instruction = "Keep exactly one Entry node."; break;
            case "ATG005": instruction = "Keep exactly one Output node."; break;
            case "ATG010": instruction = "Connect the Selector Fallback port."; break;
            case "ATG011": instruction = "Connect this node from Entry or remove it."; break;
            case "ATG012": instruction = "Remove the execution cycle; authoring flow must be acyclic."; break;
            case "ATG016": instruction = "Use an atomic handoff or a safe policy across spaces."; break;
            case "ATG017": instruction = "Add an explicit presentation adapter before RootMotionBlend."; break;
            case "ATG101":
            case "ATG102":
            case "ATG103":
            case "ATG104": instruction = "Reduce graph complexity or split the graph deliberately."; break;
        }
        return new AnimTransitionValidationAction244(issue.Code, issue.Severity == AnimTransitionGraphIssueSeverity.Error ? "Error" : "Warning", instruction, issue.NodeGuid);
    }

    public static string BuildReport(AnimTransitionGraphHealthReport report)
    {
        if (report == null) return "No validation result.";
        if (report.Issues.Count == 0) return report.Summary;
        var lines = new string[report.Issues.Count + 1];
        lines[0] = report.Summary;
        for (var i = 0; i < report.Issues.Count; i++) lines[i + 1] = Describe(report.Issues[i]).Summary;
        return string.Join("\n", lines);
    }
}
#endif

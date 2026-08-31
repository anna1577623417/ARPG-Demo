/// <summary>Pure source selector used by the L4 scaffold; it never invokes the writer.</summary>
public static class AnimationPresentationExecutionGate244
{
    public static bool TrySelect(
        AnimationPipelineMode mode,
        in AnimationPresentationCoordinatorResult244 result,
        out TransitionPlan plan,
        out AnimationTransitionPlanSource244 source,
        out string reason)
    {
        plan = default;
        source = AnimationTransitionPlanSource244.None;
        reason = string.Empty;
        if (!result.IsAccepted)
        {
            reason = "ArbitrationRejected";
            return false;
        }

        if (mode == AnimationPipelineMode.Disabled)
        {
            reason = "PipelineDisabled";
            return false;
        }

        if (mode == AnimationPipelineMode.Canary
            && result.GraphDecision.IsAccepted
            && result.GraphPlan.ShouldSubmitPlayback)
        {
            plan = result.GraphPlan;
            source = AnimationTransitionPlanSource244.Graph;
            reason = "GraphCanary";
            return true;
        }

        if (result.LegacyPlan.ShouldSubmitPlayback)
        {
            plan = result.LegacyPlan;
            source = AnimationTransitionPlanSource244.Legacy;
            reason = mode == AnimationPipelineMode.Canary ? "GraphFallbackLegacy" : "LegacyDefault";
            return true;
        }

        reason = "PlanDoesNotSubmit";
        return false;
    }
}

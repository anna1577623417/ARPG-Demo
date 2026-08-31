using System;

/// <summary>First behavior-relevant divergence between legacy and compiled-graph shadow outputs.</summary>
public enum AnimationTransitionShadowDifferenceKind243 : byte
{
    None = 0,
    GraphUnavailable = 1,
    GraphHashMismatch = 2,
    GraphPathMissing = 3,
    GraphCandidateSource = 4,
    RequestDomain = 5,
    RequestSemantic = 6,
    RequestClipKey = 7,
    Arbitration = 8,
    TransitionMode = 9,
    SpatialHandoff = 10,
    Fallback = 11,
}

/// <summary>One immutable Shadow comparison. Path/hash are recorded as provenance, not parity failures.</summary>
public readonly struct AnimationTransitionShadowSample243
{
    public readonly AnimationPlayRequest LegacyRequest;
    public readonly AnimationPlayRequest GraphRequest;
    public readonly AnimationArbitrationDecision LegacyDecision;
    public readonly AnimationArbitrationDecision GraphDecision;
    public readonly TransitionPlan LegacyPlan;
    public readonly TransitionPlan GraphPlan;
    public readonly string GraphNodePath;
    public readonly string GraphHash;
    public readonly AnimationTransitionShadowDifferenceKind243 DifferenceKind;
    public readonly string DifferenceReason;

    public bool IsReady => DifferenceKind == AnimationTransitionShadowDifferenceKind243.None;

    public AnimationTransitionShadowSample243(
        in AnimationPlayRequest legacyRequest,
        in AnimationPlayRequest graphRequest,
        in AnimationArbitrationDecision legacyDecision,
        in AnimationArbitrationDecision graphDecision,
        in TransitionPlan legacyPlan,
        in TransitionPlan graphPlan,
        string graphNodePath,
        string graphHash,
        AnimationTransitionShadowDifferenceKind243 differenceKind,
        string differenceReason)
    {
        LegacyRequest = legacyRequest;
        GraphRequest = graphRequest;
        LegacyDecision = legacyDecision;
        GraphDecision = graphDecision;
        LegacyPlan = legacyPlan;
        GraphPlan = graphPlan;
        GraphNodePath = graphNodePath ?? string.Empty;
        GraphHash = graphHash ?? string.Empty;
        DifferenceKind = differenceKind;
        DifferenceReason = differenceReason ?? string.Empty;
    }
}

/// <summary>
/// 243.9 L2 — Pure, non-playing Shadow harness. Graph request production remains an explicit later
/// concern; this type only verifies provenance and compares the two candidates under one observation.
/// </summary>
public static class AnimationTransitionGraphShadowEvaluator243
{
    public static AnimationTransitionShadowSample243 Evaluate(
        in AnimationObservation observation,
        in AnimationArbitrationState legacyState,
        in TransitionContext legacyContext,
        in AnimationArbitrationState graphState,
        in TransitionContext graphContext,
        CompiledAnimTransitionGraphReader graphReader)
    {
        var legacyRequest = legacyContext.Request;
        var graphRequest = graphContext.Request;
        var graphHash = graphReader != null ? graphReader.GraphHash : string.Empty;
        if (graphReader == null || !graphReader.IsAvailable)
        {
            return Invalid(
                in legacyRequest, in graphRequest, graphContext.GraphNodePath, graphHash,
                AnimationTransitionShadowDifferenceKind243.GraphUnavailable, "compiled-graph-unavailable");
        }

        if (!string.Equals(graphContext.GraphHash, graphHash, StringComparison.Ordinal))
        {
            return Invalid(
                in legacyRequest, in graphRequest, graphContext.GraphNodePath, graphHash,
                AnimationTransitionShadowDifferenceKind243.GraphHashMismatch, "compiled-hash-mismatch");
        }

        if (string.IsNullOrEmpty(graphContext.GraphNodePath))
        {
            return Invalid(
                in legacyRequest, in graphRequest, graphContext.GraphNodePath, graphHash,
                AnimationTransitionShadowDifferenceKind243.GraphPathMissing, "graph-path-missing");
        }

        if (graphRequest.SourceKind != AnimationRequestSourceKind.Graph)
        {
            return Invalid(
                in legacyRequest, in graphRequest, graphContext.GraphNodePath, graphHash,
                AnimationTransitionShadowDifferenceKind243.GraphCandidateSource, "graph-request-source-required");
        }

        AnimationRequestArbiter.Evaluate(in legacyState, in legacyRequest, in observation, out var legacyDecision);
        AnimationRequestArbiter.Evaluate(in graphState, in graphRequest, in observation, out var graphDecision);
        var legacyPlan = legacyDecision.IsAccepted ? AnimationTransitionSafetyResolver.Resolve(in legacyContext) : default;
        var graphPlan = graphDecision.IsAccepted ? AnimationTransitionSafetyResolver.Resolve(in graphContext) : default;
        var difference = FindFirstBehaviorDifference(
            in legacyRequest, in graphRequest,
            in legacyDecision, in graphDecision,
            in legacyPlan, in graphPlan,
            out var reason);
        return new AnimationTransitionShadowSample243(
            in legacyRequest, in graphRequest,
            in legacyDecision, in graphDecision,
            in legacyPlan, in graphPlan,
            graphContext.GraphNodePath, graphHash, difference, reason);
    }

    static AnimationTransitionShadowSample243 Invalid(
        in AnimationPlayRequest legacyRequest,
        in AnimationPlayRequest graphRequest,
        string graphNodePath,
        string graphHash,
        AnimationTransitionShadowDifferenceKind243 kind,
        string reason)
    {
        var rejectedState = default(AnimationArbitrationState);
        var rejectedDecision = new AnimationArbitrationDecision(
            AnimationArbitrationDecisionKind.Rejected,
            AnimationArbitrationReason.None,
            in rejectedState);
        var emptyPlan = default(TransitionPlan);
        return new AnimationTransitionShadowSample243(
            in legacyRequest, in graphRequest,
            in rejectedDecision, in rejectedDecision,
            in emptyPlan, in emptyPlan,
            graphNodePath, graphHash, kind, reason);
    }

    static AnimationTransitionShadowDifferenceKind243 FindFirstBehaviorDifference(
        in AnimationPlayRequest legacyRequest,
        in AnimationPlayRequest graphRequest,
        in AnimationArbitrationDecision legacyDecision,
        in AnimationArbitrationDecision graphDecision,
        in TransitionPlan legacyPlan,
        in TransitionPlan graphPlan,
        out string reason)
    {
        if (legacyRequest.Domain != graphRequest.Domain)
            return Difference(AnimationTransitionShadowDifferenceKind243.RequestDomain, "request-domain", out reason);
        if (!string.Equals(legacyRequest.Semantic, graphRequest.Semantic, StringComparison.Ordinal))
            return Difference(AnimationTransitionShadowDifferenceKind243.RequestSemantic, "request-semantic", out reason);
        if (!string.Equals(legacyRequest.ClipKey, graphRequest.ClipKey, StringComparison.Ordinal))
            return Difference(AnimationTransitionShadowDifferenceKind243.RequestClipKey, "request-clip", out reason);
        if (legacyDecision.Kind != graphDecision.Kind || legacyDecision.Reason != graphDecision.Reason)
            return Difference(AnimationTransitionShadowDifferenceKind243.Arbitration, "arbiter", out reason);

        if (!legacyDecision.IsAccepted)
        {
            reason = string.Empty;
            return AnimationTransitionShadowDifferenceKind243.None;
        }

        if (legacyPlan.TransitionMode != graphPlan.TransitionMode)
            return Difference(AnimationTransitionShadowDifferenceKind243.TransitionMode, "plan-mode", out reason);
        if (legacyPlan.SpatialHandoffMode != graphPlan.SpatialHandoffMode)
            return Difference(AnimationTransitionShadowDifferenceKind243.SpatialHandoff, "plan-handoff", out reason);
        if (legacyPlan.FallbackReason != graphPlan.FallbackReason)
            return Difference(AnimationTransitionShadowDifferenceKind243.Fallback, "plan-fallback", out reason);

        reason = string.Empty;
        return AnimationTransitionShadowDifferenceKind243.None;
    }

    static AnimationTransitionShadowDifferenceKind243 Difference(
        AnimationTransitionShadowDifferenceKind243 kind,
        string reason,
        out string outputReason)
    {
        outputReason = reason;
        return kind;
    }
}

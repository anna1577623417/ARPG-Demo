using System;

public enum AnimationTransitionPlanSource244 : byte
{
    None = 0,
    Legacy = 1,
    Graph = 2,
}

/// <summary>All facts needed to create one graph/legacy candidate pair. It carries no writer reference.</summary>
public readonly struct AnimationPresentationSubmission244
{
    public readonly AnimationPlayRequest Request;
    public readonly AnimationObservation Observation;
    public readonly AnimationPresentationIdentity244 FromIdentity;
    public readonly AnimationPresentationIdentity244 ToIdentity;
    public readonly AnimationPresentationState243 SourcePresentation;
    public readonly string TargetRootSpaceKey;
    public readonly float TargetFootPhase;
    public readonly bool TargetHasValidFootPhase;
    public readonly TransitionMode RequestedMode;
    public readonly RootTranslationChannelMode RequestedRootTranslationMode;
    public readonly float RequestedBlendDuration;
    public readonly float RequestedInertializationDuration;
    public readonly AnimationPhaseMatchMode PhaseMatchMode;
    public readonly bool IsHardReaction;
    public readonly bool HasRootMotionAdapter;
    public readonly LegacyTransitionBaseline244 LegacyBaseline;

    public AnimationPresentationSubmission244(
        in AnimationPlayRequest request,
        in AnimationObservation observation,
        in AnimationPresentationIdentity244 fromIdentity,
        in AnimationPresentationIdentity244 toIdentity,
        in AnimationPresentationState243 sourcePresentation,
        string targetRootSpaceKey,
        float targetFootPhase,
        bool targetHasValidFootPhase,
        TransitionMode requestedMode,
        RootTranslationChannelMode requestedRootTranslationMode,
        float requestedBlendDuration,
        float requestedInertializationDuration,
        AnimationPhaseMatchMode phaseMatchMode,
        bool isHardReaction,
        bool hasRootMotionAdapter,
        in LegacyTransitionBaseline244 legacyBaseline)
    {
        Request = request;
        Observation = observation;
        FromIdentity = fromIdentity;
        ToIdentity = toIdentity;
        SourcePresentation = sourcePresentation;
        TargetRootSpaceKey = targetRootSpaceKey ?? string.Empty;
        TargetFootPhase = targetFootPhase;
        TargetHasValidFootPhase = targetHasValidFootPhase;
        RequestedMode = requestedMode;
        RequestedRootTranslationMode = requestedRootTranslationMode;
        RequestedBlendDuration = requestedBlendDuration;
        RequestedInertializationDuration = requestedInertializationDuration;
        PhaseMatchMode = phaseMatchMode;
        IsHardReaction = isHardReaction;
        HasRootMotionAdapter = hasRootMotionAdapter;
        LegacyBaseline = legacyBaseline;
    }
}

public readonly struct AnimationPresentationSnapshot244
{
    public readonly bool HasCurrent;
    public readonly AnimationArbitrationState ArbitrationState;
    public readonly AnimationPlayRequest CurrentRequest;
    public readonly TransitionPlan CurrentPlan;
    public readonly AnimationTransitionDecision244 CurrentGraphDecision;
    public readonly int Revision;

    public AnimationPresentationSnapshot244(
        bool hasCurrent,
        in AnimationArbitrationState arbitrationState,
        in AnimationPlayRequest currentRequest,
        in TransitionPlan currentPlan,
        in AnimationTransitionDecision244 currentGraphDecision,
        int revision)
    {
        HasCurrent = hasCurrent;
        ArbitrationState = arbitrationState;
        CurrentRequest = currentRequest;
        CurrentPlan = currentPlan;
        CurrentGraphDecision = currentGraphDecision;
        Revision = revision;
    }
}

public readonly struct AnimationPresentationCoordinatorResult244
{
    public readonly AnimationArbitrationDecision Arbitration;
    public readonly AnimationTransitionDecision244 GraphDecision;
    public readonly TransitionPlan LegacyPlan;
    public readonly TransitionPlan GraphPlan;
    public readonly AnimationTransitionPlanSource244 SelectedSource;
    public readonly TransitionPlan SelectedPlan;
    public readonly AnimationPresentationSnapshot244 ProductionSnapshot;
    public readonly AnimationPresentationSnapshot244 ShadowSnapshot;

    public bool IsAccepted => Arbitration.IsAccepted;

    public AnimationPresentationCoordinatorResult244(
        in AnimationArbitrationDecision arbitration,
        in AnimationTransitionDecision244 graphDecision,
        in TransitionPlan legacyPlan,
        in TransitionPlan graphPlan,
        AnimationTransitionPlanSource244 selectedSource,
        in TransitionPlan selectedPlan,
        in AnimationPresentationSnapshot244 productionSnapshot,
        in AnimationPresentationSnapshot244 shadowSnapshot)
    {
        Arbitration = arbitration;
        GraphDecision = graphDecision;
        LegacyPlan = legacyPlan;
        GraphPlan = graphPlan;
        SelectedSource = selectedSource;
        SelectedPlan = selectedPlan;
        ProductionSnapshot = productionSnapshot;
        ShadowSnapshot = shadowSnapshot;
    }
}

/// <summary>
/// 244.9 L3 — Actor-owned, pure plan coordinator. It arbitrates once, computes Graph and Legacy
/// candidates, and commits only presentation snapshots. Playback remains owned by the later executor gate.
/// </summary>
public sealed class AnimationPresentationCoordinator244
{
    readonly CompiledAnimTransitionGraphReader reader;
    readonly TransitionChannelCapabilities243 capabilities;
    AnimationArbitrationState arbitrationState;
    AnimationPresentationSnapshot244 productionSnapshot;
    AnimationPresentationSnapshot244 shadowSnapshot;
    int revision;

    public AnimationPresentationCoordinator244(
        CompiledAnimTransitionGraphReader compiledReader,
        in TransitionChannelCapabilities243 channelCapabilities)
    {
        reader = compiledReader;
        capabilities = channelCapabilities;
        arbitrationState = default;
        productionSnapshot = default;
        shadowSnapshot = default;
    }

    public AnimationPresentationSnapshot244 ProductionSnapshot => productionSnapshot;
    public AnimationPresentationSnapshot244 ShadowSnapshot => shadowSnapshot;

    public AnimationPresentationCoordinatorResult244 Submit(in AnimationPresentationSubmission244 submission)
    {
        AnimationRequestArbiter.Evaluate(
            in arbitrationState,
            in submission.Request,
            in submission.Observation,
            out var arbitration);

        var graphDecision = AnimationTransitionDecision244.Rejected(
            default,
            default,
            AnimationTransitionCapabilityRequirement244.None,
            reader != null ? reader.GraphHash : string.Empty,
            arbitration.IsAccepted ? string.Empty : "ArbitrationRejected");
        var legacyPlan = default(TransitionPlan);
        var graphPlan = default(TransitionPlan);
        if (arbitration.IsAccepted)
        {
            graphDecision = CompiledAnimTransitionGraphEvaluator244.Evaluate(
                reader,
                submission.FromIdentity,
                submission.ToIdentity,
                in capabilities);
            var legacyContext = LegacyTransitionBaselineAdapter244.BuildContext(
                in submission,
                in submission.LegacyBaseline);
            legacyPlan = AnimationTransitionSafetyResolver.Resolve(in legacyContext);
            if (graphDecision.IsAccepted)
            {
                var graphContext = BuildGraphContext(in submission, in graphDecision);
                graphPlan = AnimationTransitionSafetyResolver.Resolve(in graphContext);
            }

            arbitrationState = arbitration.NextState;
            revision++;
            productionSnapshot = new AnimationPresentationSnapshot244(
                true,
                in arbitrationState,
                in submission.Request,
                in legacyPlan,
                in graphDecision,
                revision);
            // Shadow is deliberately a separate value snapshot; it never advances arbitrationState.
            shadowSnapshot = new AnimationPresentationSnapshot244(
                true,
                in arbitrationState,
                in submission.Request,
                in graphPlan,
                in graphDecision,
                revision);
        }

        // Canary/Executor are later landings. Legacy is the safe default and the only selected source here.
        var selected = legacyPlan;
        return new AnimationPresentationCoordinatorResult244(
            in arbitration,
            in graphDecision,
            in legacyPlan,
            in graphPlan,
            AnimationTransitionPlanSource244.Legacy,
            in selected,
            in productionSnapshot,
            in shadowSnapshot);
    }

    public void Reset()
    {
        arbitrationState = default;
        productionSnapshot = default;
        shadowSnapshot = default;
        revision = 0;
    }

    static TransitionContext BuildGraphContext(
        in AnimationPresentationSubmission244 submission,
        in AnimationTransitionDecision244 decision)
    {
        var policy = decision.Policy;
        return new TransitionContext(
            in submission.Request,
            in submission.SourcePresentation,
            submission.TargetRootSpaceKey,
            submission.TargetFootPhase,
            submission.TargetHasValidFootPhase,
            policy.TransitionMode,
            policy.RootTranslationMode,
            policy.BlendDuration,
            policy.InertializationDuration,
            policy.PhaseMatchMode,
            submission.IsHardReaction,
            submission.HasRootMotionAdapter,
            decision.GraphNodePath,
            decision.GraphHash);
    }
}

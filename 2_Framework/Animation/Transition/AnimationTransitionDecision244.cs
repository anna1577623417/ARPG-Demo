using System;

public enum AnimationTransitionDecisionDisposition244 : byte
{
    Matched = 0,
    Defaulted = 1,
    Rejected = 2,
    GraphUnavailable = 3,
}

/// <summary>Immutable, allocation-free result of the typed graph decision stage.</summary>
public readonly struct AnimationTransitionDecision244
{
    public readonly AnimationTransitionDecisionDisposition244 Disposition;
    public readonly string RuleId;
    public readonly CompiledAnimationTransitionRuleKind244 RuleKind;
    public readonly int Specificity;
    public readonly int PolicyIndex;
    public readonly CompiledAnimationTransitionPolicy244 Policy;
    public readonly AnimationTransitionPolicySource244 PolicySource;
    public readonly string SourceProfileGuid;
    public readonly string SourceProfileHash;
    public readonly string ImportedBaselineKey;
    public readonly float ResolvedBlendDuration;
    public readonly AnimationTransitionCapabilityRequirement244 RequiredCapabilities;
    public readonly AnimationTransitionCapabilityRequirement244 MissingCapabilities;
    public readonly string GraphNodePath;
    public readonly string GraphHash;
    public readonly string Reason;

    public bool IsAccepted => Disposition == AnimationTransitionDecisionDisposition244.Matched
        || Disposition == AnimationTransitionDecisionDisposition244.Defaulted;

    public bool HasMissingCapabilities => MissingCapabilities != AnimationTransitionCapabilityRequirement244.None;

    internal AnimationTransitionDecision244(
        AnimationTransitionDecisionDisposition244 disposition,
        CompiledAnimationTransitionRule244 rule,
        CompiledAnimationTransitionPolicy244 policy,
        AnimationTransitionCapabilityRequirement244 missingCapabilities,
        string graphHash,
        string reason)
    {
        Disposition = disposition;
        RuleId = rule.RuleId ?? string.Empty;
        RuleKind = rule.RuleKind;
        Specificity = rule.Specificity;
        PolicyIndex = rule.PolicyIndex;
        Policy = policy;
        PolicySource = policy.Source;
        SourceProfileGuid = policy.SourceProfileGuid ?? string.Empty;
        SourceProfileHash = policy.SourceProfileHash ?? string.Empty;
        ImportedBaselineKey = policy.ImportedBaselineKey ?? string.Empty;
        ResolvedBlendDuration = policy.BlendDuration;
        RequiredCapabilities = policy.CapabilityRequirements;
        MissingCapabilities = missingCapabilities;
        GraphNodePath = rule.SourceNodeGuid ?? string.Empty;
        GraphHash = graphHash ?? string.Empty;
        Reason = reason ?? string.Empty;
    }

    internal static AnimationTransitionDecision244 Unavailable(string graphHash, string reason)
    {
        return new AnimationTransitionDecision244(
            AnimationTransitionDecisionDisposition244.GraphUnavailable,
            default,
            default,
            AnimationTransitionCapabilityRequirement244.None,
            graphHash,
            reason);
    }

    internal static AnimationTransitionDecision244 Rejected(
        CompiledAnimationTransitionRule244 rule,
        CompiledAnimationTransitionPolicy244 policy,
        AnimationTransitionCapabilityRequirement244 missingCapabilities,
        string graphHash,
        string reason)
    {
        return new AnimationTransitionDecision244(
            AnimationTransitionDecisionDisposition244.Rejected,
            rule,
            policy,
            missingCapabilities,
            graphHash,
            reason);
    }

    internal static AnimationTransitionDecision244 Accepted(
        CompiledAnimationTransitionRule244 rule,
        CompiledAnimationTransitionPolicy244 policy,
        AnimationTransitionCapabilityRequirement244 missingCapabilities,
        string graphHash,
        string reason)
    {
        var disposition = rule.RuleKind == CompiledAnimationTransitionRuleKind244.Default
            ? AnimationTransitionDecisionDisposition244.Defaulted
            : AnimationTransitionDecisionDisposition244.Matched;
        return new AnimationTransitionDecision244(
            disposition,
            rule,
            policy,
            missingCapabilities,
            graphHash,
            reason);
    }
}

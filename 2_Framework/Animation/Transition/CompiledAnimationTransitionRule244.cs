using System;

public enum CompiledAnimationTransitionRuleKind244 : byte
{
    Exception = 0,
    Family = 1,
    Default = 2,
}

[Serializable]
public struct CompiledAnimationTransitionRule244
{
    public string RuleId;
    public string SourceNodeGuid;
    public AnimationRequestDomain Domain;
    public string FromKey;
    public string ToKey;
    public AnimationPresentationSemanticMask244 RequiredSemantics;
    public int Specificity;
    public int PolicyIndex;
    public CompiledAnimationTransitionRuleKind244 RuleKind;
    public string ReasonId;

    public bool Matches(AnimationPresentationIdentity244 from, AnimationPresentationIdentity244 to)
    {
        if (RuleKind != CompiledAnimationTransitionRuleKind244.Default && (Domain != from.Domain || Domain != to.Domain)) return false;
        if (RuleKind == CompiledAnimationTransitionRuleKind244.Default && Domain != AnimationRequestDomain.Unknown
            && (Domain != from.Domain || Domain != to.Domain)) return false;
        if (!string.IsNullOrEmpty(FromKey) && !string.Equals(FromKey, from.Key, StringComparison.Ordinal)) return false;
        if (!string.IsNullOrEmpty(ToKey) && !string.Equals(ToKey, to.Key, StringComparison.Ordinal)) return false;
        return RequiredSemantics == AnimationPresentationSemanticMask244.None
            || ((from.Semantics & RequiredSemantics) == RequiredSemantics && (to.Semantics & RequiredSemantics) == RequiredSemantics);
    }
}

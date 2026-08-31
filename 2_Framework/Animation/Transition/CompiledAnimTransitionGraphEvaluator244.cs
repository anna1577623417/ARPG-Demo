using System;

/// <summary>Pure typed-table evaluator. It never parses node configuration strings or touches Unity objects.</summary>
public static class CompiledAnimTransitionGraphEvaluator244
{
    public static AnimationTransitionDecision244 Evaluate(
        CompiledAnimTransitionGraphReader reader,
        in AnimationPresentationIdentity244 from,
        in AnimationPresentationIdentity244 to,
        in TransitionChannelCapabilities243 capabilities)
    {
        if (reader == null || !reader.IsAvailable)
        {
            return AnimationTransitionDecision244.Unavailable(
                reader != null ? reader.GraphHash : string.Empty,
                "GraphUnavailable");
        }

        if (!from.IsValid || !to.IsValid)
        {
            return AnimationTransitionDecision244.Rejected(
                default,
                default,
                AnimationTransitionCapabilityRequirement244.None,
                reader.GraphHash,
                "InvalidIdentity");
        }

        if (!TryResolve(reader, from, to, out var rule, out var ambiguous))
        {
            return AnimationTransitionDecision244.Rejected(
                default,
                default,
                AnimationTransitionCapabilityRequirement244.None,
                reader.GraphHash,
                ambiguous ? "AmbiguousPolicy" : "NoMatchingRule");
        }

        if (!reader.TryGetTypedPolicy(rule.PolicyIndex, out var policy))
        {
            return AnimationTransitionDecision244.Rejected(
                rule,
                default,
                AnimationTransitionCapabilityRequirement244.None,
                reader.GraphHash,
                "MissingPolicy");
        }

        var missing = FindMissingCapabilities(policy.CapabilityRequirements, capabilities);
        if (missing != AnimationTransitionCapabilityRequirement244.None)
        {
            return AnimationTransitionDecision244.Rejected(
                rule,
                policy,
                missing,
                reader.GraphHash,
                "MissingCapabilities");
        }

        var reason = rule.RuleKind == CompiledAnimationTransitionRuleKind244.Default
            ? "DefaultFallback"
            : (string.IsNullOrEmpty(rule.ReasonId) ? "RuleMatched" : rule.ReasonId);
        return AnimationTransitionDecision244.Accepted(rule, policy, missing, reader.GraphHash, reason);
    }

    public static bool TryResolve(
        CompiledAnimationTransitionRule244[] rules,
        AnimationPresentationIdentity244 from,
        AnimationPresentationIdentity244 to,
        out CompiledAnimationTransitionRule244 winner)
    {
        return TryResolve(rules, from, to, out winner, out _);
    }

    static bool TryResolve(
        CompiledAnimTransitionGraphReader reader,
        AnimationPresentationIdentity244 from,
        AnimationPresentationIdentity244 to,
        out CompiledAnimationTransitionRule244 winner,
        out bool ambiguous)
    {
        winner = default;
        ambiguous = false;
        if (reader == null || !from.IsValid || !to.IsValid) return false;

        var found = false;
        for (var i = 0; i < reader.RuleCount; i++)
        {
            if (!reader.TryGetRule(i, out var candidate) || !candidate.Matches(from, to)) continue;
            if (!found || candidate.Specificity > winner.Specificity)
            {
                winner = candidate;
                found = true;
                continue;
            }

            if (candidate.Specificity == winner.Specificity && candidate.PolicyIndex != winner.PolicyIndex)
            {
                winner = default;
                ambiguous = true;
                return false;
            }

            if (candidate.Specificity == winner.Specificity
                && candidate.PolicyIndex == winner.PolicyIndex
                && string.CompareOrdinal(candidate.RuleId, winner.RuleId) < 0)
            {
                winner = candidate;
            }
        }
        return found;
    }

    static bool TryResolve(
        CompiledAnimationTransitionRule244[] rules,
        AnimationPresentationIdentity244 from,
        AnimationPresentationIdentity244 to,
        out CompiledAnimationTransitionRule244 winner,
        out bool ambiguous)
    {
        winner = default;
        ambiguous = false;
        if (rules == null || !from.IsValid || !to.IsValid) return false;
        var found = false;
        for (var i = 0; i < rules.Length; i++)
        {
            var candidate = rules[i];
            if (!candidate.Matches(from, to)) continue;
            if (!found || candidate.Specificity > winner.Specificity)
            {
                winner = candidate;
                found = true;
                continue;
            }

            if (candidate.Specificity == winner.Specificity && candidate.PolicyIndex != winner.PolicyIndex)
            {
                winner = default;
                ambiguous = true;
                return false;
            }

            if (candidate.Specificity == winner.Specificity
                && candidate.PolicyIndex == winner.PolicyIndex
                && string.CompareOrdinal(candidate.RuleId, winner.RuleId) < 0)
            {
                winner = candidate;
            }
        }

        return found;
    }

    static AnimationTransitionCapabilityRequirement244 FindMissingCapabilities(
        AnimationTransitionCapabilityRequirement244 required,
        in TransitionChannelCapabilities243 capabilities)
    {
        var missing = AnimationTransitionCapabilityRequirement244.None;
        if ((required & AnimationTransitionCapabilityRequirement244.RootMotionAdapter) != 0
            && !capabilities.SupportsRootMotionBlend)
        {
            missing |= AnimationTransitionCapabilityRequirement244.RootMotionAdapter;
        }
        if ((required & AnimationTransitionCapabilityRequirement244.PhaseMatching) != 0
            && !capabilities.SupportsPhaseMatch)
        {
            missing |= AnimationTransitionCapabilityRequirement244.PhaseMatching;
        }
        if ((required & AnimationTransitionCapabilityRequirement244.Inertialization) != 0
            && !capabilities.SupportsPoseInertialization)
        {
            missing |= AnimationTransitionCapabilityRequirement244.Inertialization;
        }
        if ((required & (AnimationTransitionCapabilityRequirement244.Layer | AnimationTransitionCapabilityRequirement244.Sync)) != 0
            && !capabilities.SupportsLayerSync)
        {
            missing |= required & (AnimationTransitionCapabilityRequirement244.Layer | AnimationTransitionCapabilityRequirement244.Sync);
        }
        return missing;
    }
}

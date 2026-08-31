#if UNITY_EDITOR
using System;
using System.Collections.Generic;

public struct AnimTransitionPreviewRequest244
{
    public AnimationPresentationIdentity244 From;
    public AnimationPresentationIdentity244 To;

    public AnimTransitionPreviewRequest244(AnimationPresentationIdentity244 from, AnimationPresentationIdentity244 to)
    {
        From = from;
        To = to;
    }
}

public struct AnimTransitionPreviewResult244
{
    public bool Resolved;
    public bool Ambiguous;
    public string RuleId;
    public int PolicyIndex;
    public string Summary;
}

/// <summary>Editor-only preview facade. It calls the same typed evaluator as Runtime will use, without playback.</summary>
public static class AnimTransitionPreviewLab244
{
    public static AnimTransitionPreviewResult244 Evaluate(CompiledAnimTransitionGraphReader reader, AnimTransitionPreviewRequest244 request)
    {
        if (reader == null || reader.RuleCount == 0)
        {
            return new AnimTransitionPreviewResult244 { Summary = "No typed rules compiled." };
        }

        var rules = new List<CompiledAnimationTransitionRule244>(reader.RuleCount);
        for (var i = 0; i < reader.RuleCount; i++)
        {
            if (reader.TryGetRule(i, out var rule)) rules.Add(rule);
        }

        if (!CompiledAnimTransitionGraphEvaluator244.TryResolve(rules.ToArray(), request.From, request.To, out var winner))
        {
            return new AnimTransitionPreviewResult244
            {
                Ambiguous = request.From.IsValid && request.To.IsValid,
                Summary = request.From.IsValid && request.To.IsValid ? "No unique typed rule matched." : "Preview identities are incomplete.",
            };
        }

        return new AnimTransitionPreviewResult244
        {
            Resolved = true,
            RuleId = winner.RuleId,
            PolicyIndex = winner.PolicyIndex,
            Summary = winner.RuleKind + " → policy[" + winner.PolicyIndex + "] · " + winner.RuleId,
        };
    }
}
#endif

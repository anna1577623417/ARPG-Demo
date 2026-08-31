using System;
using GraphProcessor;
using UnityEngine;

/// <summary>244.8 L4 — Business-facing nodes. They serialize directly in BaseGraph; no hidden mirror rules.</summary>
[Serializable, NodeMenuItem("Animation Transition/Business/Domain Entry", typeof(AnimTransitionAuthoringGraph))]
public sealed class AnimGraphDomainEntryNode244 : AnimTransitionGraphNode
{
    [SerializeField] AnimTransitionGraphDomain domain = AnimTransitionGraphDomain.Any;
    [Output(name = "Request", allowMultiple = false)] public AnimGraphRequestPort request;
    public override string name => "Domain Entry";
    public override AnimTransitionGraphNodeKind Kind => AnimTransitionGraphNodeKind.DomainEntry;
    public override AnimTransitionGraphDomain Domain => domain;
    public AnimTransitionGraphDomain EntryDomain => domain;
    public void EditorSetDomain(AnimTransitionGraphDomain value) => domain = value;
    public override string BuildDeterministicConfiguration() => "domain-entry:" + (byte)domain;
}

[Serializable, NodeMenuItem("Animation Transition/Business/Presentation Resolve", typeof(AnimTransitionAuthoringGraph))]
public sealed class AnimGraphPresentationResolveNode244 : AnimTransitionGraphNode
{
    [SerializeField] AnimationRequestDomain domain = AnimationRequestDomain.Unknown;
    [SerializeField] string semanticKey = string.Empty;
    [SerializeField] AnimationPresentationSemanticMask244 semantics;
    [SerializeField] string rootSpaceKey = string.Empty;
    [Input(name = "Request", allowMultiple = false)] public AnimGraphRequestPort request;
    [Output(name = "Plan", allowMultiple = false)] public AnimGraphPlanDraftPort plan;
    public override string name => "Presentation Resolve";
    public override AnimTransitionGraphNodeKind Kind => AnimTransitionGraphNodeKind.PresentationResolve;
    public override AnimTransitionGraphDomain Domain => ToGraphDomain(domain);
    public AnimationRequestDomain RequestDomain => domain;
    public string SemanticKey => semanticKey ?? string.Empty;
    public AnimationPresentationSemanticMask244 Semantics => semantics;
    public string RootSpaceKey => rootSpaceKey ?? string.Empty;
    public AnimationPresentationIdentity244 Identity => new AnimationPresentationIdentity244(domain, SemanticKey, semantics, RootSpaceKey);
    public void EditorSetIdentity(AnimationRequestDomain valueDomain, string key, AnimationPresentationSemanticMask244 valueSemantics, string valueRootSpace)
    {
        domain = valueDomain;
        semanticKey = key ?? string.Empty;
        semantics = valueSemantics;
        rootSpaceKey = valueRootSpace ?? string.Empty;
    }
    public override string BuildDeterministicConfiguration() => "resolve:" + Identity.BuildDeterministicKey();
    static AnimTransitionGraphDomain ToGraphDomain(AnimationRequestDomain value)
    {
        switch (value)
        {
            case AnimationRequestDomain.Locomotion: return AnimTransitionGraphDomain.Locomotion;
            case AnimationRequestDomain.Airborne: return AnimTransitionGraphDomain.Airborne;
            case AnimationRequestDomain.Action: return AnimTransitionGraphDomain.Action;
            case AnimationRequestDomain.Turn: return AnimTransitionGraphDomain.Turn;
            case AnimationRequestDomain.Reaction: return AnimTransitionGraphDomain.Hit;
            default: return AnimTransitionGraphDomain.Any;
        }
    }
}

[Serializable]
public abstract class AnimTransitionRuleNode244 : AnimTransitionGraphNode
{
    [SerializeField] AnimationRequestDomain matchDomain = AnimationRequestDomain.Unknown;
    [SerializeField] string fromKey = string.Empty;
    [SerializeField] string toKey = string.Empty;
    [SerializeField] AnimationPresentationSemanticMask244 requiredSemantics;
    [SerializeField] AnimationTransitionPolicyProfileSO244 profile;
    [Input(name = "Plan In", allowMultiple = false)] public AnimGraphPlanDraftPort input;
    [Output(name = "Plan Out", allowMultiple = false)] public AnimGraphPlanDraftPort output;
    public string FromKey => fromKey ?? string.Empty;
    public string ToKey => toKey ?? string.Empty;
    public AnimationRequestDomain MatchDomain => matchDomain;
    public AnimationPresentationSemanticMask244 RequiredSemantics => requiredSemantics;
    public AnimationTransitionPolicyProfileSO244 Profile => profile;
    public void EditorSetMatcher(string from, string to, AnimationPresentationSemanticMask244 semantics)
    {
        fromKey = from ?? string.Empty;
        toKey = to ?? string.Empty;
        requiredSemantics = semantics;
    }
    public void EditorSetMatchDomain(AnimationRequestDomain value) => matchDomain = value;
    public void EditorSetProfile(AnimationTransitionPolicyProfileSO244 value) => profile = value;
    protected string BuildMatcherConfiguration(string prefix) => string.Concat(
        prefix, ":", (byte)MatchDomain, ":", FromKey, ":", ToKey, ":", (uint)RequiredSemantics, ":", Profile != null ? Profile.ProfileId : string.Empty);
}

[Serializable, NodeMenuItem("Animation Transition/Business/Transition Family", typeof(AnimTransitionAuthoringGraph))]
public sealed class AnimGraphTransitionFamilyNode244 : AnimTransitionRuleNode244
{
    public override string name => "Transition Family";
    public override AnimTransitionGraphNodeKind Kind => AnimTransitionGraphNodeKind.TransitionFamily;
    public override string BuildDeterministicConfiguration() => BuildMatcherConfiguration("family");
}

[Serializable, NodeMenuItem("Animation Transition/Business/Exception Rule", typeof(AnimTransitionAuthoringGraph))]
public sealed class AnimGraphExceptionRuleNode244 : AnimTransitionRuleNode244
{
    [SerializeField] string reason = string.Empty;
    public override string name => "Exception Rule";
    public override AnimTransitionGraphNodeKind Kind => AnimTransitionGraphNodeKind.ExceptionRule;
    public string Reason => reason ?? string.Empty;
    public void EditorSetReason(string value) => reason = value ?? string.Empty;
    public override string BuildDeterministicConfiguration() => BuildMatcherConfiguration("exception") + ":" + Reason;
}

[Serializable, NodeMenuItem("Animation Transition/Business/Policy Profile", typeof(AnimTransitionAuthoringGraph))]
public sealed class AnimGraphPolicyProfileNode244 : AnimTransitionGraphNode
{
    [SerializeField] AnimationTransitionPolicyProfileSO244 profile;
    [Input(name = "Plan In", allowMultiple = false)] public AnimGraphPlanDraftPort input;
    [Output(name = "Plan Out", allowMultiple = false)] public AnimGraphPlanDraftPort output;
    public override string name => "Policy Profile";
    public override AnimTransitionGraphNodeKind Kind => AnimTransitionGraphNodeKind.PolicyProfile;
    public AnimationTransitionPolicyProfileSO244 Profile => profile;
    public void EditorSetProfile(AnimationTransitionPolicyProfileSO244 value) => profile = value;
    public override string BuildDeterministicConfiguration() => "profile:" + (Profile != null ? Profile.ProfileId : string.Empty);
}

[Serializable, NodeMenuItem("Animation Transition/Business/Default Fallback", typeof(AnimTransitionAuthoringGraph))]
public sealed class AnimGraphDefaultFallbackNode244 : AnimTransitionGraphNode
{
    [SerializeField] AnimTransitionGraphDomain domain = AnimTransitionGraphDomain.Any;
    [SerializeField] AnimationTransitionPolicyProfileSO244 profile;
    [Input(name = "Plan In", allowMultiple = false)] public AnimGraphPlanDraftPort input;
    [Output(name = "Plan Out", allowMultiple = false)] public AnimGraphPlanDraftPort output;
    public override string name => "Default Fallback";
    public override AnimTransitionGraphNodeKind Kind => AnimTransitionGraphNodeKind.DefaultFallback;
    public override AnimTransitionGraphDomain Domain => domain;
    public AnimTransitionGraphDomain FallbackDomain => domain;
    public AnimationTransitionPolicyProfileSO244 Profile => profile;
    public void EditorSetFallback(AnimTransitionGraphDomain valueDomain, AnimationTransitionPolicyProfileSO244 valueProfile)
    {
        domain = valueDomain;
        profile = valueProfile;
    }
    public override string BuildDeterministicConfiguration() => "default:" + (byte)domain + ":" + (Profile != null ? Profile.ProfileId : string.Empty);
}

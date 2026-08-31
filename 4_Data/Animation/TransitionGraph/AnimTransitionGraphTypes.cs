using System;

/// <summary>243.7 — Authoring domains are presentation-only routing categories.</summary>
public enum AnimTransitionGraphDomain : byte
{
    Any = 0,
    Locomotion = 1,
    Airborne = 2,
    Action = 3,
    Turn = 4,
    Hit = 5,
    Death = 6,
}

public enum AnimTransitionGraphNodeKind : byte
{
    Entry = 0,
    Predicate = 1,
    Selector = 2,
    Variant = 3,
    TransitionPolicy = 4,
    SpatialHandoff = 5,
    Layer = 6,
    Sync = 7,
    SubGraph = 8,
    Output = 9,
    Reroute = 10,
    DomainEntry = 11,
    PresentationResolve = 12,
    TransitionFamily = 13,
    ExceptionRule = 14,
    PolicyProfile = 15,
    DefaultFallback = 16,
}

public enum AnimGraphPredicateKind : byte
{
    RequestDomain = 0,
    ObservationFlag = 1,
    ActionLease = 2,
    PresentationTag = 3,
}

public enum AnimGraphRootSpaceRelation : byte
{
    SameSpace = 0,
    CrossSpace = 1,
}

/// <summary>Typed GraphProcessor connection token. It contains no behavior or priority data.</summary>
[Serializable]
public struct AnimGraphRequestPort { }

/// <summary>Typed branch token emitted by predicates and selectors.</summary>
[Serializable]
public struct AnimGraphBranchPort { }

/// <summary>Typed plan-draft token. Policies live on nodes, never on the edge.</summary>
[Serializable]
public struct AnimGraphPlanDraftPort { }

/// <summary>Stable, serializable policy owned by a TransitionPolicy node.</summary>
[Serializable]
public struct AnimTransitionPolicyHandle
{
    public TransitionMode TransitionMode;
    public RootYawChannelMode RootYawMode;
    public RootTranslationChannelMode RootTranslationMode;
    public PoseChannelMode PoseMode;
    public float BlendDuration;
    public AnimationInterruptPolicy InterruptPolicy;

    public static AnimTransitionPolicyHandle Default => new AnimTransitionPolicyHandle
    {
        TransitionMode = TransitionMode.CrossFade,
        RootYawMode = RootYawChannelMode.Preserve,
        RootTranslationMode = RootTranslationChannelMode.Preserve,
        PoseMode = PoseChannelMode.CrossFade,
        BlendDuration = 0.1f,
        InterruptPolicy = AnimationInterruptPolicy.Interruptible,
    };
}

/// <summary>Compiler-facing edge description. It deliberately carries connection identity only.</summary>
[Serializable]
public struct CompiledAnimTransitionLink
{
    public int FromNodeIndex;
    public int ToNodeIndex;
    public string FromPortId;
    public string ToPortId;

    public CompiledAnimTransitionLink(int fromNodeIndex, int toNodeIndex, string fromPortId, string toPortId)
    {
        FromNodeIndex = fromNodeIndex;
        ToNodeIndex = toNodeIndex;
        FromPortId = fromPortId ?? string.Empty;
        ToPortId = toPortId ?? string.Empty;
    }
}

[Serializable]
public struct CompiledAnimTransitionNode
{
    public string NodeGuid;
    public AnimTransitionGraphNodeKind Kind;
    public AnimTransitionGraphDomain Domain;
    public string Configuration;

    public CompiledAnimTransitionNode(string nodeGuid, AnimTransitionGraphNodeKind kind, AnimTransitionGraphDomain domain, string configuration)
    {
        NodeGuid = nodeGuid ?? string.Empty;
        Kind = kind;
        Domain = domain;
        Configuration = configuration ?? string.Empty;
    }
}

[Serializable]
public struct CompiledAnimTransitionOutput
{
    public int NodeIndex;
    public string OutputId;

    public CompiledAnimTransitionOutput(int nodeIndex, string outputId)
    {
        NodeIndex = nodeIndex;
        OutputId = outputId ?? string.Empty;
    }
}

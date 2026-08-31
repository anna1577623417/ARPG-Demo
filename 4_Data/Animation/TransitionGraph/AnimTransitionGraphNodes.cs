using System;
using GraphProcessor;
using UnityEngine;

/// <summary>Common immutable compiler description for a single-responsibility authoring node.</summary>
[Serializable]
public abstract class AnimTransitionGraphNode : BaseNode
{
    public abstract AnimTransitionGraphNodeKind Kind { get; }
    public virtual AnimTransitionGraphDomain Domain => AnimTransitionGraphDomain.Any;
    public abstract string BuildDeterministicConfiguration();
}

[Serializable, NodeMenuItem("Animation Transition/Entry", typeof(AnimTransitionAuthoringGraph))]
public sealed class AnimGraphEntryNode : AnimTransitionGraphNode
{
    [Output(name = "Request", allowMultiple = false)] public AnimGraphRequestPort request;
    public override string name => "Entry";
    public override AnimTransitionGraphNodeKind Kind => AnimTransitionGraphNodeKind.Entry;
    public override string BuildDeterministicConfiguration() => "entry";
}

[Serializable, NodeMenuItem("Animation Transition/Predicate", typeof(AnimTransitionAuthoringGraph))]
public sealed class AnimGraphPredicateNode : AnimTransitionGraphNode
{
    [SerializeField] AnimGraphPredicateKind predicate = AnimGraphPredicateKind.RequestDomain;
    [SerializeField] string operand = string.Empty;

    [Input(name = "Request", allowMultiple = false)] public AnimGraphRequestPort request;
    [Output(name = "Match", allowMultiple = false)] public AnimGraphBranchPort match;
    [Output(name = "Else", allowMultiple = false)] public AnimGraphBranchPort elseBranch;
    [Output(name = "Invalid", allowMultiple = false)] public AnimGraphBranchPort invalid;

    public override string name => "Predicate";
    public override AnimTransitionGraphNodeKind Kind => AnimTransitionGraphNodeKind.Predicate;
    public AnimGraphPredicateKind Predicate => predicate;
    public string Operand => operand ?? string.Empty;
    public void EditorSetPredicate(AnimGraphPredicateKind value, string valueOperand)
    {
        predicate = value;
        operand = valueOperand ?? string.Empty;
    }

    public override string BuildDeterministicConfiguration() => $"predicate:{(byte)predicate}:{Operand}";
}

[Serializable, NodeMenuItem("Animation Transition/Selector", typeof(AnimTransitionAuthoringGraph))]
public sealed class AnimGraphSelectorNode : AnimTransitionGraphNode
{
    [Input(name = "P0", allowMultiple = false)] public AnimGraphBranchPort priority0;
    [Input(name = "P1", allowMultiple = false)] public AnimGraphBranchPort priority1;
    [Input(name = "P2", allowMultiple = false)] public AnimGraphBranchPort priority2;
    [Input(name = "Fallback", allowMultiple = false)] public AnimGraphBranchPort fallback;
    [Output(name = "Selected", allowMultiple = false)] public AnimGraphBranchPort selected;

    public override string name => "Selector";
    public override AnimTransitionGraphNodeKind Kind => AnimTransitionGraphNodeKind.Selector;
    public override string BuildDeterministicConfiguration() => "selector:p0,p1,p2,fallback";
}

[Serializable, NodeMenuItem("Animation Transition/Variant", typeof(AnimTransitionAuthoringGraph))]
public sealed class AnimGraphVariantNode : AnimTransitionGraphNode
{
    [SerializeField] string variantSetId = string.Empty;
    [SerializeField] string fallbackVariantId = string.Empty;

    [Input(name = "Selected", allowMultiple = false)] public AnimGraphBranchPort selected;
    [Output(name = "Plan", allowMultiple = false)] public AnimGraphPlanDraftPort plan;

    public override string name => "Variant";
    public override AnimTransitionGraphNodeKind Kind => AnimTransitionGraphNodeKind.Variant;
    public string VariantSetId => variantSetId ?? string.Empty;
    public string FallbackVariantId => fallbackVariantId ?? string.Empty;
    public void EditorSetVariants(string setId, string fallbackId)
    {
        variantSetId = setId ?? string.Empty;
        fallbackVariantId = fallbackId ?? string.Empty;
    }

    public override string BuildDeterministicConfiguration() => $"variant:{VariantSetId}:{FallbackVariantId}";
}

[Serializable, NodeMenuItem("Animation Transition/Transition Policy", typeof(AnimTransitionAuthoringGraph))]
public sealed class AnimGraphTransitionPolicyNode : AnimTransitionGraphNode
{
    [SerializeField] AnimTransitionPolicyHandle policy = default;

    [Input(name = "Plan", allowMultiple = false)] public AnimGraphPlanDraftPort input;
    [Output(name = "Plan", allowMultiple = false)] public AnimGraphPlanDraftPort output;

    public override string name => "Transition Policy";
    public override AnimTransitionGraphNodeKind Kind => AnimTransitionGraphNodeKind.TransitionPolicy;
    public AnimTransitionPolicyHandle Policy => policy;
    public void EditorSetPolicy(AnimTransitionPolicyHandle value) => policy = value;
    public override string BuildDeterministicConfiguration()
    {
        return $"policy:{(byte)policy.TransitionMode}:{(byte)policy.RootYawMode}:{(byte)policy.RootTranslationMode}:" +
            $"{(byte)policy.PoseMode}:{policy.BlendDuration:R}:{(byte)policy.InterruptPolicy}";
    }
}

[Serializable, NodeMenuItem("Animation Transition/Spatial Handoff", typeof(AnimTransitionAuthoringGraph))]
public sealed class AnimGraphSpatialHandoffNode : AnimTransitionGraphNode
{
    [SerializeField] AnimGraphRootSpaceRelation rootSpaceRelation = AnimGraphRootSpaceRelation.SameSpace;
    [SerializeField] SpatialHandoffMode handoffMode = SpatialHandoffMode.SameSpace;
    [SerializeField] bool hasRootMotionAdapter;

    [Input(name = "Plan", allowMultiple = false)] public AnimGraphPlanDraftPort input;
    [Output(name = "Plan", allowMultiple = false)] public AnimGraphPlanDraftPort output;

    public override string name => "Spatial Handoff";
    public override AnimTransitionGraphNodeKind Kind => AnimTransitionGraphNodeKind.SpatialHandoff;
    public AnimGraphRootSpaceRelation RootSpaceRelation => rootSpaceRelation;
    public SpatialHandoffMode HandoffMode => handoffMode;
    public bool HasRootMotionAdapter => hasRootMotionAdapter;
    public void EditorSetSpatial(AnimGraphRootSpaceRelation relation, SpatialHandoffMode mode, bool rootMotionAdapter)
    {
        rootSpaceRelation = relation;
        handoffMode = mode;
        hasRootMotionAdapter = rootMotionAdapter;
    }

    public override string BuildDeterministicConfiguration() =>
        $"spatial:{(byte)rootSpaceRelation}:{(byte)handoffMode}:{hasRootMotionAdapter}";
}

[Serializable, NodeMenuItem("Animation Transition/Layer", typeof(AnimTransitionAuthoringGraph))]
public sealed class AnimGraphLayerNode : AnimTransitionGraphNode
{
    [SerializeField] int layer;
    [Input(name = "Plan", allowMultiple = false)] public AnimGraphPlanDraftPort input;
    [Output(name = "Plan", allowMultiple = false)] public AnimGraphPlanDraftPort output;
    public override string name => "Layer";
    public override AnimTransitionGraphNodeKind Kind => AnimTransitionGraphNodeKind.Layer;
    public int Layer => layer;
    public void EditorSetLayer(int value) => layer = Mathf.Max(0, value);
    public override string BuildDeterministicConfiguration() => $"layer:{layer}";
}

[Serializable, NodeMenuItem("Animation Transition/Sync", typeof(AnimTransitionAuthoringGraph))]
public sealed class AnimGraphSyncNode : AnimTransitionGraphNode
{
    [SerializeField] string syncGroup = string.Empty;
    [Input(name = "Plan", allowMultiple = false)] public AnimGraphPlanDraftPort input;
    [Output(name = "Plan", allowMultiple = false)] public AnimGraphPlanDraftPort output;
    public override string name => "Sync";
    public override AnimTransitionGraphNodeKind Kind => AnimTransitionGraphNodeKind.Sync;
    public string SyncGroup => syncGroup ?? string.Empty;
    public void EditorSetSyncGroup(string value) => syncGroup = value ?? string.Empty;
    public override string BuildDeterministicConfiguration() => $"sync:{SyncGroup}";
}

[Serializable, NodeMenuItem("Animation Transition/Sub Graph", typeof(AnimTransitionAuthoringGraph))]
public sealed class AnimGraphSubGraphNode : AnimTransitionGraphNode
{
    [SerializeField] AnimTransitionAuthoringGraph subGraph;
    [SerializeField] AnimTransitionGraphDomain interfaceDomain = AnimTransitionGraphDomain.Any;

    [Input(name = "Branch In", allowMultiple = false)] public AnimGraphBranchPort input;
    [Output(name = "Branch Out", allowMultiple = false)] public AnimGraphBranchPort output;

    public override string name => "Sub Graph";
    public override AnimTransitionGraphNodeKind Kind => AnimTransitionGraphNodeKind.SubGraph;
    public AnimTransitionAuthoringGraph SubGraph => subGraph;
    public AnimTransitionGraphDomain InterfaceDomain => interfaceDomain;
    public void EditorSetSubGraph(AnimTransitionAuthoringGraph value, AnimTransitionGraphDomain valueDomain)
    {
        subGraph = value;
        interfaceDomain = valueDomain;
    }

    public override string BuildDeterministicConfiguration()
    {
        return $"sub:{(subGraph != null ? subGraph.GraphGuid : string.Empty)}:{(byte)interfaceDomain}";
    }
}

[Serializable, NodeMenuItem("Animation Transition/Output", typeof(AnimTransitionAuthoringGraph))]
public sealed class AnimGraphOutputNode : AnimTransitionGraphNode
{
    [SerializeField] string outputId = "Main";
    [Input(name = "Transition Plan", allowMultiple = false)] public AnimGraphPlanDraftPort input;
    public override string name => "Output";
    public override AnimTransitionGraphNodeKind Kind => AnimTransitionGraphNodeKind.Output;
    public string OutputId => string.IsNullOrEmpty(outputId) ? "Main" : outputId;
    public void EditorSetOutputId(string value) => outputId = value ?? string.Empty;
    public override string BuildDeterministicConfiguration() => $"output:{OutputId}";
}

[Serializable, NodeMenuItem("Animation Transition/Reroute", typeof(AnimTransitionAuthoringGraph))]
public sealed class AnimGraphRerouteNode : AnimTransitionGraphNode
{
    [Input(name = "Plan In", allowMultiple = false)] public AnimGraphPlanDraftPort input;
    [Output(name = "Plan Out", allowMultiple = true)] public AnimGraphPlanDraftPort output;
    public override string name => "Reroute";
    public override AnimTransitionGraphNodeKind Kind => AnimTransitionGraphNodeKind.Reroute;
    public override string BuildDeterministicConfiguration() => "reroute";
}

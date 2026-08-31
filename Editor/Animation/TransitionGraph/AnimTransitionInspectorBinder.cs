#if UNITY_EDITOR
using System;
using GraphProcessor;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Inspector commits node properties through History after blur, Enter, or a 0.4s pause.</summary>
public sealed class AnimTransitionInspectorBinder
{
    const long MergeDelayMs = 400;
    readonly VisualElement root;
    readonly AnimTransitionGraphWindow window;
    Label summary;
    VisualElement editors;
    AnimTransitionNodeSnapshot pendingBefore;
    IVisualElementScheduledItem mergeJob;
    string boundGuid = string.Empty;

    public AnimTransitionInspectorBinder(VisualElement inspectorRoot, AnimTransitionGraphWindow owner)
    {
        root = inspectorRoot;
        window = owner;
        summary = inspectorRoot.Q<Label>("AnimTransitionInspectorSelection");
        editors = new VisualElement { name = "AnimTransitionInspectorEditors" };
        inspectorRoot.Add(editors);
    }

    public void Refresh(AnimTransitionAuthoringGraph graph, GraphElement primary)
    {
        if (primary is BaseNodeView nodeView && nodeView.nodeTarget is AnimTransitionGraphNode node)
        {
            if (boundGuid != node.GUID)
            {
                CommitPending();
                RebuildEditors(node);
            }
            else if (summary != null)
            {
                summary.text = node.Kind + "\n" + node.BuildDeterministicConfiguration() + "\nGUID: " + node.GUID;
            }

            return;
        }

        CommitPending();
        boundGuid = string.Empty;
        editors.Clear();
        if (summary == null) return;
        if (primary is EdgeView edgeView && edgeView.serializedEdge != null)
        {
            summary.text = "Connection (read-only)\n" + edgeView.serializedEdge.outputFieldName + " → " + edgeView.serializedEdge.inputFieldName;
            return;
        }

        if (graph == null)
        {
            summary.text = "Select an AnimTransitionAuthoringGraph asset to author it.";
            return;
        }

        summary.text = "Graph Overview\nDomain: " + graph.Domain + "\nNodes: " + graph.nodes.Count + "\nEdges: " + graph.edges.Count +
            "\nHash: " + (graph.CompiledGraph != null ? graph.CompiledGraph.GraphHash : "uncompiled");
    }

    public void CommitPending()
    {
        mergeJob?.Pause();
        FlushIfChanged();
    }

    void RebuildEditors(AnimTransitionGraphNode node)
    {
        boundGuid = node.GUID;
        pendingBefore = default;
        editors.Clear();
        if (summary != null)
        {
            summary.text = node.Kind + "\n" + node.BuildDeterministicConfiguration() + "\nGUID: " + node.GUID;
        }

        if (EditorApplication.isPlaying)
        {
            editors.Add(new Label("PlayMode read-only"));
            return;
        }

        if (node is AnimGraphDomainEntryNode244 domainEntry)
        {
            var domain = new EnumField("Domain", domainEntry.EntryDomain);
            domain.RegisterValueChangedCallback(evt => BeginEdit(domainEntry, () => domainEntry.EditorSetDomain((AnimTransitionGraphDomain)evt.newValue)));
            editors.Add(domain);
        }
        else if (node is AnimGraphPresentationResolveNode244 resolve)
        {
            var domain = new EnumField("Request Domain", resolve.RequestDomain);
            var key = new TextField("Semantic Key") { value = resolve.SemanticKey };
            var semantics = new EnumFlagsField("Semantics", resolve.Semantics);
            var rootSpace = new TextField("Root Space") { value = resolve.RootSpaceKey };
            Action apply = () => resolve.EditorSetIdentity((AnimationRequestDomain)domain.value, key.value,
                (AnimationPresentationSemanticMask244)semantics.value, rootSpace.value);
            domain.RegisterValueChangedCallback(_ => BeginEdit(resolve, apply));
            semantics.RegisterValueChangedCallback(_ => BeginEdit(resolve, apply));
            key.RegisterValueChangedCallback(_ => BeginEdit(resolve, apply));
            rootSpace.RegisterValueChangedCallback(_ => BeginEdit(resolve, apply));
            editors.Add(domain);
            editors.Add(key);
            editors.Add(semantics);
            editors.Add(rootSpace);
        }
        else if (node is AnimGraphTransitionFamilyNode244 family)
        {
            AddRuleEditors(family, "Family");
        }
        else if (node is AnimGraphExceptionRuleNode244 exception)
        {
            AddRuleEditors(exception, "Exception");
            AddStringField("Reason", exception.Reason, value => BeginEdit(exception, () => exception.EditorSetReason(value)));
        }
        else if (node is AnimGraphPolicyProfileNode244 profileNode)
        {
            AddProfileField(profileNode, profileNode.Profile, value => profileNode.EditorSetProfile(value));
        }
        else if (node is AnimGraphDefaultFallbackNode244 fallback)
        {
            var domain = new EnumField("Fallback Domain", fallback.FallbackDomain);
            domain.RegisterValueChangedCallback(evt => BeginEdit(fallback, () => fallback.EditorSetFallback((AnimTransitionGraphDomain)evt.newValue, fallback.Profile)));
            editors.Add(domain);
            AddProfileField(fallback, fallback.Profile, value => fallback.EditorSetFallback(fallback.FallbackDomain, value));
        }
        else if (node is AnimGraphPredicateNode predicate)
        {
            var kind = new EnumField("Predicate", predicate.Predicate);
            var operand = new TextField("Operand") { value = predicate.Operand };
            kind.RegisterValueChangedCallback(evt =>
                BeginEdit(predicate, () => predicate.EditorSetPredicate((AnimGraphPredicateKind)evt.newValue, operand.value)));
            operand.RegisterValueChangedCallback(evt =>
                BeginEdit(predicate, () => predicate.EditorSetPredicate((AnimGraphPredicateKind)kind.value, evt.newValue)));
            operand.RegisterCallback<BlurEvent>(_ => CommitPending());
            operand.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) CommitPending();
            });
            editors.Add(kind);
            editors.Add(operand);
        }
        else if (node is AnimGraphVariantNode variant)
        {
            AddStringField("Variant Set", variant.VariantSetId, value =>
                BeginEdit(variant, () => variant.EditorSetVariants(value, variant.FallbackVariantId)));
            AddStringField("Fallback Variant", variant.FallbackVariantId, value =>
                BeginEdit(variant, () => variant.EditorSetVariants(variant.VariantSetId, value)));
        }
        else if (node is AnimGraphLayerNode layer)
        {
            var field = new IntegerField("Layer") { value = layer.Layer };
            field.RegisterValueChangedCallback(evt => BeginEdit(layer, () => layer.EditorSetLayer(evt.newValue)));
            field.RegisterCallback<BlurEvent>(_ => CommitPending());
            editors.Add(field);
        }
        else if (node is AnimGraphSyncNode sync)
        {
            AddStringField("Sync Group", sync.SyncGroup, value => BeginEdit(sync, () => sync.EditorSetSyncGroup(value)));
        }
        else if (node is AnimGraphOutputNode output)
        {
            AddStringField("Output Id", output.OutputId, value => BeginEdit(output, () => output.EditorSetOutputId(value)));
        }
        else if (node is AnimGraphSubGraphNode subGraph)
        {
            var field = new ObjectField("Sub Graph")
            {
                objectType = typeof(AnimTransitionAuthoringGraph),
                value = subGraph.SubGraph,
            };
            field.RegisterValueChangedCallback(evt =>
                BeginEdit(subGraph, () => subGraph.EditorSetSubGraph(evt.newValue as AnimTransitionAuthoringGraph, subGraph.InterfaceDomain)));
            var domain = new EnumField("Interface Domain", subGraph.InterfaceDomain);
            domain.RegisterValueChangedCallback(evt =>
                BeginEdit(subGraph, () => subGraph.EditorSetSubGraph(subGraph.SubGraph, (AnimTransitionGraphDomain)evt.newValue)));
            editors.Add(field);
            editors.Add(domain);
            var enter = new Button(() => window.EnterSubGraph(subGraph.SubGraph)) { text = "Enter Sub Graph" };
            editors.Add(enter);
        }
        else
        {
            editors.Add(new Label("Node configuration is edited here. The card stays a read-only projection."));
        }
    }

    void AddStringField(string title, string value, Action<string> setter)
    {
        var field = new TextField(title) { value = value ?? string.Empty };
        field.RegisterValueChangedCallback(evt => setter(evt.newValue));
        field.RegisterCallback<BlurEvent>(_ => CommitPending());
        field.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) CommitPending();
        });
        editors.Add(field);
    }

    void AddRuleEditors(AnimTransitionRuleNode244 rule, string label)
    {
        var domain = new EnumField(label + " Domain", rule.MatchDomain);
        domain.RegisterValueChangedCallback(evt => BeginEdit(rule, () => rule.EditorSetMatchDomain((AnimationRequestDomain)evt.newValue)));
        editors.Add(domain);
        AddStringField(label + " From Key", rule.FromKey,
            value => BeginEdit(rule, () => rule.EditorSetMatcher(value, rule.ToKey, rule.RequiredSemantics)));
        AddStringField(label + " To Key", rule.ToKey,
            value => BeginEdit(rule, () => rule.EditorSetMatcher(rule.FromKey, value, rule.RequiredSemantics)));
        var semantics = new EnumFlagsField(label + " Semantics", rule.RequiredSemantics);
        semantics.RegisterValueChangedCallback(evt => BeginEdit(rule,
            () => rule.EditorSetMatcher(rule.FromKey, rule.ToKey, (AnimationPresentationSemanticMask244)evt.newValue)));
        editors.Add(semantics);
        AddProfileField(rule, rule.Profile, value => rule.EditorSetProfile(value));
    }

    void AddProfileField(AnimTransitionGraphNode node, AnimationTransitionPolicyProfileSO244 value, Action<AnimationTransitionPolicyProfileSO244> setter)
    {
        var field = new ObjectField("Policy Profile")
        {
            objectType = typeof(AnimationTransitionPolicyProfileSO244),
            value = value,
        };
        field.RegisterValueChangedCallback(evt => BeginEdit(node, () => setter(evt.newValue as AnimationTransitionPolicyProfileSO244)));
        editors.Add(field);
    }

    void BeginEdit(AnimTransitionGraphNode node, Action mutate)
    {
        if (node == null || mutate == null || EditorApplication.isPlaying) return;
        if (string.IsNullOrEmpty(pendingBefore.Guid))
        {
            pendingBefore = AnimTransitionNodeSnapshot.Capture(node);
        }

        mutate();
        if (summary != null)
        {
            summary.text = node.Kind + "\n" + node.BuildDeterministicConfiguration() + "\nGUID: " + node.GUID;
        }

        mergeJob?.Pause();
        mergeJob = root.schedule.Execute(FlushIfChanged).StartingIn(MergeDelayMs);
    }

    void FlushIfChanged()
    {
        if (string.IsNullOrEmpty(pendingBefore.Guid) || window.AuthoringGraph == null) return;
        if (!window.AuthoringGraph.nodesPerGUID.TryGetValue(pendingBefore.Guid, out var live))
        {
            pendingBefore = default;
            return;
        }

        var after = AnimTransitionNodeSnapshot.Capture(live);
        if (after.JsonDatas == pendingBefore.JsonDatas)
        {
            pendingBefore = default;
            return;
        }

        var command = new AnimTransitionSetNodePropertyCommand(pendingBefore, after);
        pendingBefore = default;
        window.History.Execute(window.AuthoringGraph, command);
        window.NotifyInspectorCommitted();
    }
}
#endif

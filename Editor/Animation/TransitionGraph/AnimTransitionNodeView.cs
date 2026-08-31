using GraphProcessor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Compact read-only projection of an authoring node; configuration remains in the graph node/Inspector.</summary>
[NodeCustomEditor(typeof(AnimTransitionGraphNode))]
public sealed class AnimTransitionNodeView : BaseNodeView
{
    Label summary;
    Label health;

    public override void Enable(bool fromInspector = false)
    {
        var node = nodeTarget as AnimTransitionGraphNode;
        if (node == null) return;
        style.minWidth = 156f;
        style.maxWidth = 260f;
        style.width = GetWidth(node.Kind);
        AddToClassList("anim-transition-node");
        title = node.Kind.ToString();
        summary = new Label(BuildSummary(node)) { name = "AnimTransitionNodeSummary" };
        health = new Label { name = "AnimTransitionNodeHealth" };
        controlsContainer.Add(summary);
        controlsContainer.Add(health);
        for (var i = 0; i < inputPortViews.Count; i++) ConfigurePort(inputPortViews[i]);
        for (var i = 0; i < outputPortViews.Count; i++) ConfigurePort(outputPortViews[i]);
        UpdateLod(owner.viewTransform.scale.x);
    }

    public void UpdateLod(float zoom)
    {
        if (summary == null || health == null) return;
        if (zoom < 0.55f)
        {
            summary.style.display = DisplayStyle.None;
            health.style.display = DisplayStyle.None;
        }
        else if (zoom > 1.2f || selected)
        {
            summary.style.display = DisplayStyle.Flex;
            health.style.display = DisplayStyle.Flex;
            health.text = "Ports " + inputPortViews.Count + " in / " + outputPortViews.Count + " out";
        }
        else
        {
            summary.style.display = DisplayStyle.Flex;
            health.style.display = DisplayStyle.None;
        }
    }

    static void ConfigurePort(PortView port)
    {
        port.AddToClassList("anim-transition-port");
        port.tooltip = "Typed port: " + port.portType.Name + " · " + (port.direction == UnityEditor.Experimental.GraphView.Direction.Input ? "Input" : "Output");
    }

    static float GetWidth(AnimTransitionGraphNodeKind kind)
    {
        switch (kind)
        {
            case AnimTransitionGraphNodeKind.Entry:
            case AnimTransitionGraphNodeKind.Output: return 190f;
            case AnimTransitionGraphNodeKind.Predicate: return 210f;
            case AnimTransitionGraphNodeKind.Selector: return 220f;
            case AnimTransitionGraphNodeKind.PresentationResolve:
            case AnimTransitionGraphNodeKind.TransitionFamily:
            case AnimTransitionGraphNodeKind.ExceptionRule: return 280f;
            case AnimTransitionGraphNodeKind.DomainEntry:
            case AnimTransitionGraphNodeKind.PolicyProfile:
            case AnimTransitionGraphNodeKind.DefaultFallback: return 250f;
            case AnimTransitionGraphNodeKind.Variant:
            case AnimTransitionGraphNodeKind.SubGraph: return 230f;
            default: return 240f;
        }
    }

    static string BuildSummary(AnimTransitionGraphNode node)
    {
        if (node is AnimGraphDomainEntryNode244 domainEntry)
        {
            return "Domain Entry · " + domainEntry.EntryDomain;
        }

        if (node is AnimGraphPresentationResolveNode244 resolve)
        {
            return "Resolve · " + (string.IsNullOrEmpty(resolve.SemanticKey) ? "未设置语义 Key" : resolve.SemanticKey);
        }

        if (node is AnimGraphTransitionFamilyNode244 family)
        {
            return "Family · " + family.FromKey + " → " + family.ToKey;
        }

        if (node is AnimGraphExceptionRuleNode244 exception)
        {
            return "Exception · " + exception.FromKey + " → " + exception.ToKey;
        }

        if (node is AnimGraphPolicyProfileNode244 profile)
        {
            return "Profile · " + (profile.Profile != null ? profile.Profile.ProfileId : "未绑定");
        }

        if (node is AnimGraphDefaultFallbackNode244 fallback)
        {
            return "Default · " + fallback.FallbackDomain;
        }

        var value = node.BuildDeterministicConfiguration();
        return string.IsNullOrEmpty(value) ? node.Kind.ToString() : value;
    }
}

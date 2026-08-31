#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using GraphProcessor;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Quick Add from a dangling port. Creates the node and one edge as a single history command.</summary>
public sealed class AnimTransitionQuickAddProvider : ScriptableObject, ISearchWindowProvider
{
    static AnimTransitionQuickAddProvider instance;
    static Texture2D indentIcon;

    AnimTransitionGraphWindow window;
    PortView sourcePort;
    Vector2 graphPosition;

    public static void Open(AnimTransitionGraphWindow owner, PortView port, Vector2 screenPosition)
    {
        if (owner == null || port == null || EditorApplication.isPlaying) return;
        if (instance == null) instance = CreateInstance<AnimTransitionQuickAddProvider>();
        instance.window = owner;
        instance.sourcePort = port;
        var view = owner.TransitionCanvas;
        var canvasPosition = view != null
            ? AnimTransitionCanvasCoordinates244.PanelToCanvas(view, screenPosition)
            : screenPosition;
        instance.graphPosition = view != null
            ? AnimTransitionCanvasCoordinates244.CanvasToGraph(view, canvasPosition)
            : canvasPosition;
        if (indentIcon == null)
        {
            indentIcon = new Texture2D(1, 1);
            indentIcon.SetPixel(0, 0, Color.clear);
            indentIcon.Apply();
        }

        SearchWindow.Open(new SearchWindowContext(screenPosition), instance);
    }

    public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
    {
        var tree = new List<SearchTreeEntry>
        {
            new SearchTreeGroupEntry(new GUIContent("Quick Add"), 0),
        };
        var types = AnimTransitionNodeCatalog.CompatibleTypes(sourcePort);
        for (var i = 0; i < types.Count; i++)
        {
            tree.Add(new SearchTreeEntry(new GUIContent(AnimTransitionNodeCatalog.DisplayName(types[i]), indentIcon))
            {
                level = 1,
                userData = types[i],
            });
        }

        if (types.Count == 0)
        {
            tree.Add(new SearchTreeEntry(new GUIContent("No compatible node", indentIcon)) { level = 1, userData = null });
        }

        return tree;
    }

    public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
    {
        if (!(entry?.userData is Type type) || window == null || sourcePort == null) return false;
        return window.QuickAddFromPort(sourcePort, type, graphPosition);
    }
}

public static class AnimTransitionNodeCatalog
{
    public static readonly Type[] All =
    {
        typeof(AnimGraphEntryNode),
        typeof(AnimGraphDomainEntryNode244),
        typeof(AnimGraphPresentationResolveNode244),
        typeof(AnimGraphTransitionFamilyNode244),
        typeof(AnimGraphExceptionRuleNode244),
        typeof(AnimGraphPolicyProfileNode244),
        typeof(AnimGraphDefaultFallbackNode244),
        typeof(AnimGraphPredicateNode),
        typeof(AnimGraphSelectorNode),
        typeof(AnimGraphVariantNode),
        typeof(AnimGraphTransitionPolicyNode),
        typeof(AnimGraphSpatialHandoffNode),
        typeof(AnimGraphLayerNode),
        typeof(AnimGraphSyncNode),
        typeof(AnimGraphSubGraphNode),
        typeof(AnimGraphOutputNode),
        typeof(AnimGraphRerouteNode),
    };

    public static string DisplayName(Type type)
    {
        if (type == typeof(AnimGraphEntryNode)) return "Entry";
        if (type == typeof(AnimGraphDomainEntryNode244)) return "Domain Entry";
        if (type == typeof(AnimGraphPresentationResolveNode244)) return "Presentation Resolve";
        if (type == typeof(AnimGraphTransitionFamilyNode244)) return "Transition Family";
        if (type == typeof(AnimGraphExceptionRuleNode244)) return "Exception Rule";
        if (type == typeof(AnimGraphPolicyProfileNode244)) return "Policy Profile";
        if (type == typeof(AnimGraphDefaultFallbackNode244)) return "Default Fallback";
        if (type == typeof(AnimGraphPredicateNode)) return "Predicate";
        if (type == typeof(AnimGraphSelectorNode)) return "Selector";
        if (type == typeof(AnimGraphVariantNode)) return "Variant";
        if (type == typeof(AnimGraphTransitionPolicyNode)) return "Transition Policy";
        if (type == typeof(AnimGraphSpatialHandoffNode)) return "Spatial Handoff";
        if (type == typeof(AnimGraphLayerNode)) return "Layer";
        if (type == typeof(AnimGraphSyncNode)) return "Sync";
        if (type == typeof(AnimGraphSubGraphNode)) return "Sub Graph";
        if (type == typeof(AnimGraphOutputNode)) return "Output";
        if (type == typeof(AnimGraphRerouteNode)) return "Reroute";
        return type != null ? type.Name : string.Empty;
    }

    public static List<Type> CompatibleTypes(PortView source)
    {
        var result = new List<Type>();
        if (source == null) return result;
        var needInput = source.direction == Direction.Output;
        for (var i = 0; i < All.Length; i++)
        {
            if (FindCompatibleField(All[i], source.portType, needInput, out _)) result.Add(All[i]);
        }

        return result;
    }

    public static bool FindCompatibleField(Type nodeType, Type portType, bool wantInput, out string fieldName)
    {
        fieldName = string.Empty;
        if (nodeType == null || portType == null) return false;
        var fields = nodeType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (var i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            var isInput = field.GetCustomAttribute<InputAttribute>() != null;
            var isOutput = field.GetCustomAttribute<OutputAttribute>() != null;
            if (wantInput && !isInput) continue;
            if (!wantInput && !isOutput) continue;
            var connectable = wantInput
                ? BaseGraph.TypesAreConnectable(portType, field.FieldType)
                : BaseGraph.TypesAreConnectable(field.FieldType, portType);
            if (!connectable) continue;
            fieldName = field.Name;
            return true;
        }

        return false;
    }
}
#endif

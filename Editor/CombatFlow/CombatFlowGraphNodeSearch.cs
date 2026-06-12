#if UNITY_EDITOR
using System;
using GraphProcessor;
using UnityEngine.UIElements;

/// <summary>150.3 P2 — 节点搜索过滤（按 nodeId / Action / Route 名匹配）。</summary>
public static class CombatFlowGraphNodeSearch
{
    public static bool NodeMatchesQuery(BaseNode node, string queryLower)
    {
        if (string.IsNullOrEmpty(queryLower))
        {
            return true;
        }

        if (node == null)
        {
            return false;
        }

        if (node.name != null && node.name.IndexOf(queryLower, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        switch (node)
        {
            case CombatFlowStartNode start:
                return Contains(start.nodeId, queryLower);
            case CombatFlowActionNode action:
                return Contains(action.nodeId, queryLower)
                    || Contains(action.action != null ? action.action.name : null, queryLower);
            case CombatFlowRouteSwitchNode routeSwitch:
                return Contains(routeSwitch.nodeId, queryLower)
                    || Contains(routeSwitch.route != null ? routeSwitch.route.name : null, queryLower);
            case CombatFlowEndNode end:
                return Contains(end.nodeId, queryLower);
            default:
                return false;
        }
    }

    public static void ApplyToGraphView(BaseGraphView graphView, string query)
    {
        if (graphView == null)
        {
            return;
        }

        var queryLower = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
        var hasFilter = queryLower.Length > 0;

        WalkVisualTree(graphView, ve =>
        {
            if (ve is not BaseNodeView nodeView || nodeView.nodeTarget == null)
            {
                return;
            }

            var match = !hasFilter || NodeMatchesQuery(nodeView.nodeTarget, queryLower);
            nodeView.style.opacity = match ? 1f : 0.22f;
        });
    }

    static bool Contains(string text, string queryLower)
    {
        return !string.IsNullOrEmpty(text)
            && text.IndexOf(queryLower, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static void WalkVisualTree(VisualElement root, Action<VisualElement> visit)
    {
        if (root == null)
        {
            return;
        }

        visit(root);
        var count = root.hierarchy.childCount;
        for (var i = 0; i < count; i++)
        {
            WalkVisualTree(root.hierarchy[i], visit);
        }
    }
}
#endif

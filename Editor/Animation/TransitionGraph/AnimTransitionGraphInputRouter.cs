#if UNITY_EDITOR
using System.Collections.Generic;
using GraphProcessor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Canvas/Inspector shortcut router. Graph history is used only when a text field is not editing.</summary>
public static class AnimTransitionGraphInputRouter
{
    public static bool TryHandle(KeyDownEvent evt, AnimTransitionGraphWindow window)
    {
        if (evt == null || window == null || window.AuthoringGraph == null) return false;
        window.FocusController.Refresh(evt.currentTarget);
        var historyKeys = evt.keyCode == KeyCode.Z || evt.keyCode == KeyCode.Y;
        if (historyKeys && !window.FocusController.ShouldRouteGraphHistory) return false;

        if (evt.keyCode == KeyCode.Escape)
        {
            // Text input owns Escape first (for example, a Library/Inspector edit). The canvas
            // only closes after its transient interaction and selection layers are already clear.
            if (window.FocusController.IsTextFieldFocused) return false;
            if (!window.CancelTransientInteraction()) window.Close();
            evt.StopPropagation();
            return true;
        }

        if (IsAction(evt) && evt.keyCode == KeyCode.Z && !evt.shiftKey)
        {
            window.UndoAuthoring();
            evt.StopPropagation();
            return true;
        }

        if ((IsAction(evt) && evt.keyCode == KeyCode.Y) || (IsAction(evt) && evt.shiftKey && evt.keyCode == KeyCode.Z))
        {
            window.RedoAuthoring();
            evt.StopPropagation();
            return true;
        }

        if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
        {
            window.DeleteSelectionAsCommand();
            evt.StopPropagation();
            return true;
        }

        if (IsAction(evt) && evt.keyCode == KeyCode.D)
        {
            window.DuplicateSelectionAsCommand();
            evt.StopPropagation();
            return true;
        }

        if (IsAction(evt) && evt.keyCode == KeyCode.C)
        {
            window.CopySelectionToClipboard();
            evt.StopPropagation();
            return true;
        }

        if (IsAction(evt) && evt.keyCode == KeyCode.V)
        {
            window.PasteClipboardAsCommand();
            evt.StopPropagation();
            return true;
        }

        return false;
    }

    public static bool TryHandleCommand(string commandName, AnimTransitionGraphWindow window)
    {
        if (window == null || string.IsNullOrEmpty(commandName)) return false;
        switch (commandName)
        {
            case "Undo":
                window.UndoAuthoring();
                return true;
            case "Redo":
                window.RedoAuthoring();
                return true;
            case "Delete":
            case "SoftDelete":
                window.DeleteSelectionAsCommand();
                return true;
            case "Copy":
                window.CopySelectionToClipboard();
                return true;
            case "Paste":
                window.PasteClipboardAsCommand();
                return true;
            case "Duplicate":
                window.DuplicateSelectionAsCommand();
                return true;
            case "Cut":
                window.CopySelectionToClipboard();
                window.DeleteSelectionAsCommand();
                return true;
            default:
                return false;
        }
    }

    static bool IsAction(KeyDownEvent evt) => evt.actionKey || evt.ctrlKey || evt.commandKey;
}

public readonly struct AnimTransitionSelectedAuthoring
{
    public readonly List<string> NodeGuids;
    public readonly List<AnimTransitionEdgeSnapshot> Edges;

    public AnimTransitionSelectedAuthoring(List<string> nodeGuids, List<AnimTransitionEdgeSnapshot> edges)
    {
        NodeGuids = nodeGuids ?? new List<string>();
        Edges = edges ?? new List<AnimTransitionEdgeSnapshot>();
    }

    public static AnimTransitionSelectedAuthoring From(
        AnimTransitionAuthoringGraph graph,
        IReadOnlyList<GraphElement> selection)
    {
        var nodeGuids = new List<string>();
        var edges = new List<AnimTransitionEdgeSnapshot>();
        if (selection == null) return new AnimTransitionSelectedAuthoring(nodeGuids, edges);
        var seenNodes = new HashSet<string>();
        var seenEdges = new HashSet<string>();
        for (var i = 0; i < selection.Count; i++)
        {
            if (selection[i] is BaseNodeView nodeView && nodeView.nodeTarget != null)
            {
                if (seenNodes.Add(nodeView.nodeTarget.GUID)) nodeGuids.Add(nodeView.nodeTarget.GUID);
            }
            else if (selection[i] is EdgeView edgeView && edgeView.serializedEdge != null)
            {
                if (seenEdges.Add(edgeView.serializedEdge.GUID))
                {
                    edges.Add(AnimTransitionEdgeSnapshot.Capture(edgeView.serializedEdge));
                }
            }
        }

        if (graph != null && nodeGuids.Count > 0 && edges.Count == 0)
        {
            edges = AnimTransitionGraphMutation.CaptureIncidentEdges(graph, nodeGuids);
        }

        return new AnimTransitionSelectedAuthoring(nodeGuids, edges);
    }
}
#endif

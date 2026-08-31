#if UNITY_EDITOR
using GraphProcessor;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Routes port drops through GraphHistory. Preview / Esc / illegal drops write no command.</summary>
public sealed class AnimTransitionEdgeConnectorListener : BaseEdgeConnectorListener
{
    readonly AnimTransitionGraphWindow window;

    public AnimTransitionEdgeConnectorListener(AnimTransitionGraphView view, AnimTransitionGraphWindow owner)
        : base(view)
    {
        window = owner;
    }

    public override void OnDropOutsidePort(Edge edge, Vector2 position)
    {
        var edgeView = edge as EdgeView;
        if (edgeView == null || edgeView.isGhostEdge) return;
        window.FocusController.IsCanvasGestureActive = false;
        (graphView as AnimTransitionGraphView)?.GestureState.End("edge-drop-outside");

        var inputPort = edgeView.input as PortView;
        var outputPort = edgeView.output as PortView;
        var isPreview = !edgeView.isConnected || edgeView.serializedEdge == null;
        if (isPreview && (inputPort == null) != (outputPort == null))
        {
            DiscardCandidate(edgeView);
            window.ShowQuickAdd(inputPort ?? outputPort, position);
            return;
        }

        DiscardCandidate(edgeView);
        window.RebuildCanvas("Connection preview discarded.");
    }

    public override void OnDrop(GraphView graphView, Edge edge)
    {
        var edgeView = edge as EdgeView;
        window.FocusController.IsCanvasGestureActive = false;
        (this.graphView as AnimTransitionGraphView)?.GestureState.End("edge-drop");
        if (edgeView?.input == null || edgeView.output == null) return;

        var input = edgeView.input as PortView;
        var output = edgeView.output as PortView;
        if (input == null || output == null) return;

        var accepted = window.TryCommitPortConnection(output, input, edgeView.serializedEdge);
        DiscardCandidate(edgeView);
        if (!accepted)
        {
            window.RebuildCanvas(null);
        }
    }

    void DiscardCandidate(EdgeView edgeView)
    {
        if (edgeView == null) return;
        if (edgeView.isConnected && edgeView.serializedEdge != null)
        {
            graphView.DisconnectView(edgeView, false);
            return;
        }

        if (edgeView.parent != null) graphView.RemoveElement(edgeView);
    }
}
#endif

using System.Collections.Generic;
using GraphProcessor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>GraphProcessor canvas projection. It owns no serialized graph behavior and no Undo stack.</summary>
public sealed class AnimTransitionGraphView : BaseGraphView
{
    readonly AnimTransitionGraphWindow window;
    readonly AnimTransitionGraphSelectionController selectionController;
    readonly AnimTransitionGestureState244 gestureState;
    readonly AnimTransitionRectangleSelector244 rectangleSelector;
    readonly Dictionary<string, Rect> dragStartPositions = new Dictionary<string, Rect>();

    public AnimTransitionGraphWindow Window => window;
    public AnimTransitionGestureState244 GestureState => gestureState;

    public AnimTransitionGraphView(AnimTransitionGraphWindow owner, AnimTransitionGraphSelectionController selection)
        : base(owner)
    {
        window = owner;
        selectionController = selection;
        gestureState = new AnimTransitionGestureState244();
        name = "AnimTransitionGraphCanvas";
        style.backgroundColor = new Color(0.067f, 0.094f, 0.118f);
        rectangleSelector = new AnimTransitionRectangleSelector244(this, gestureState);
        viewTransformChanged += _ => RefreshLod();
        RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        RegisterCallback<ExecuteCommandEvent>(OnExecuteCommand, TrickleDown.TrickleDown);
        RegisterCallback<ValidateCommandEvent>(OnValidateCommand, TrickleDown.TrickleDown);
        RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
        RegisterCallback<MouseUpEvent>(OnMouseUp);
        RegisterCallback<MouseUpEvent>(_ => SyncSelection());
    }

    public override EdgeView CreateEdgeView() => new AnimTransitionEdgeView(gestureState);

    protected override BaseEdgeConnectorListener CreateEdgeConnectorListener() =>
        new AnimTransitionEdgeConnectorListener(this, window);

    public override void AddToSelection(ISelectable selectable)
    {
        base.AddToSelection(selectable);
        SyncSelection();
    }

    public override void RemoveFromSelection(ISelectable selectable)
    {
        base.RemoveFromSelection(selectable);
        SyncSelection();
    }

    public override void ClearSelection()
    {
        base.ClearSelection();
        SyncSelection();
    }

    void SyncSelection() => selectionController.SetSelectables(selection);

    public int SelectNodesInGraphRect(Rect graphRect, bool additive)
    {
        if (!additive) ClearSelection();
        var hitCount = 0;
        for (var i = 0; i < nodeViews.Count; i++)
        {
            var nodeView = nodeViews[i];
            var node = nodeView?.nodeTarget;
            if (node == null || !graphRect.Overlaps(node.position, true)) continue;
            if (!selection.Contains(nodeView)) AddToSelection(nodeView);
            hitCount++;
        }

        SyncSelection();
        return hitCount;
    }

    public void SetReadOnly(bool value)
    {
        SetEnabled(!value);
    }

    public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
    {
        base.BuildContextualMenu(evt);
        evt.menu.AppendSeparator();
        evt.menu.AppendAction("Animation Transition/Add Reroute", _ => window.AddRerouteAt(evt.localMousePosition));
        evt.menu.AppendAction("Animation Transition/Auto Layout", _ => window.ApplyLayoutAsCommand());
        evt.menu.AppendAction("Animation Transition/Undo", _ => window.UndoAuthoring());
        evt.menu.AppendAction("Animation Transition/Redo", _ => window.RedoAuthoring());
    }

    public void RebuildProjection()
    {
        if (graph == null) return;
        var selectedGuids = new HashSet<string>();
        for (var i = 0; i < selectionController.NodeGuids.Count; i++)
        {
            selectedGuids.Add(selectionController.NodeGuids[i]);
        }

        ClearGraphElements();
        if (graph.nodes != null)
        {
            for (var i = 0; i < graph.nodes.Count; i++)
            {
                if (graph.nodes[i] != null) AddNodeView(graph.nodes[i]);
            }
        }

        if (graph.edges != null)
        {
            for (var i = 0; i < graph.edges.Count; i++)
            {
                var serializedEdge = graph.edges[i];
                if (serializedEdge == null) continue;
                serializedEdge.Deserialize();
                if (serializedEdge.inputNode == null || serializedEdge.outputNode == null) continue;
                if (!nodeViewsPerNode.TryGetValue(serializedEdge.inputNode, out var inputNodeView)) continue;
                if (!nodeViewsPerNode.TryGetValue(serializedEdge.outputNode, out var outputNodeView)) continue;
                var edgeView = CreateEdgeView();
                edgeView.userData = serializedEdge;
                edgeView.input = inputNodeView.GetPortViewFromFieldName(serializedEdge.inputFieldName, serializedEdge.inputPortIdentifier);
                edgeView.output = outputNodeView.GetPortViewFromFieldName(serializedEdge.outputFieldName, serializedEdge.outputPortIdentifier);
                if (edgeView.input == null || edgeView.output == null) continue;
                ConnectView(edgeView, false);
            }
        }

        ClearSelection();
        for (var i = 0; i < nodeViews.Count; i++)
        {
            if (selectedGuids.Contains(nodeViews[i].nodeTarget.GUID)) AddToSelection(nodeViews[i]);
        }

        selectionController.Set(SelectedGraphElements());
        RefreshLod();
    }

    IEnumerable<GraphElement> SelectedGraphElements()
    {
        var list = new List<GraphElement>();
        for (var i = 0; i < selection.Count; i++)
        {
            if (selection[i] is GraphElement element) list.Add(element);
        }

        return list;
    }

    void OnKeyDown(KeyDownEvent evt)
    {
        var focused = panel != null && panel.focusController != null ? panel.focusController.focusedElement : null;
        window.FocusController.Refresh(focused);
        if (AnimTransitionGraphInputRouter.TryHandle(evt, window))
        {
            evt.StopImmediatePropagation();
        }
    }

    void OnValidateCommand(ValidateCommandEvent evt)
    {
        if (evt.commandName == "Undo" || evt.commandName == "Redo" || evt.commandName == "Delete"
            || evt.commandName == "SoftDelete" || evt.commandName == "Copy" || evt.commandName == "Paste"
            || evt.commandName == "Duplicate" || evt.commandName == "Cut")
        {
            evt.StopPropagation();
        }
    }

    void OnExecuteCommand(ExecuteCommandEvent evt)
    {
        if (!AnimTransitionGraphInputRouter.TryHandleCommand(evt.commandName, window)) return;
        evt.StopImmediatePropagation();
    }

    void OnMouseDown(MouseDownEvent evt)
    {
        if (evt.button != 0 || rectangleSelector.IsActive) return;
        dragStartPositions.Clear();
        var target = evt.target as VisualElement;
        var gesture = ResolveGestureContext(target, out var activeNodeGuid, out var activeEdgeGuid);
        var affectedNodes = new List<string>();
        if (gesture == AnimTransitionGestureKind244.NodeDrag)
        {
            for (var i = 0; i < selectionController.NodeGuids.Count; i++) affectedNodes.Add(selectionController.NodeGuids[i]);
            if (!string.IsNullOrEmpty(activeNodeGuid) && !affectedNodes.Contains(activeNodeGuid)) affectedNodes.Add(activeNodeGuid);
        }
        else if (!string.IsNullOrEmpty(activeNodeGuid))
        {
            affectedNodes.Add(activeNodeGuid);
        }

        gestureState.Begin(gesture, "pointer-down", affectedNodes, activeEdgeGuid);
        window.FocusController.IsCanvasGestureActive = true;
        for (var i = 0; i < selectionController.NodeGuids.Count; i++)
        {
            var guid = selectionController.NodeGuids[i];
            if (graph != null && graph.nodesPerGUID.TryGetValue(guid, out var node))
            {
                dragStartPositions[guid] = node.position;
            }
        }
    }

    void OnMouseUp(MouseUpEvent evt)
    {
        if (rectangleSelector.IsActive) return;
        window.FocusController.IsCanvasGestureActive = false;
        window.CommitMoveIfChanged(dragStartPositions);
        dragStartPositions.Clear();
        gestureState.End("pointer-up");
    }

    static AnimTransitionGestureKind244 ResolveGestureContext(VisualElement target, out string nodeGuid, out string edgeGuid)
    {
        nodeGuid = string.Empty;
        edgeGuid = string.Empty;
        for (var current = target; current != null; current = current.parent)
        {
            if (current is PortView port)
            {
                nodeGuid = port.owner != null && port.owner.nodeTarget != null ? port.owner.nodeTarget.GUID : string.Empty;
                return port.connected
                    ? AnimTransitionGestureKind244.Reconnect
                    : AnimTransitionGestureKind244.Connect;
            }
            if (current is AnimTransitionEdgeView edgeView)
            {
                edgeGuid = edgeView.EdgeGuid;
                return AnimTransitionGestureKind244.Reconnect;
            }
            if (current is BaseNodeView nodeView)
            {
                nodeGuid = nodeView.nodeTarget != null ? nodeView.nodeTarget.GUID : string.Empty;
                return AnimTransitionGestureKind244.NodeDrag;
            }
        }

        return AnimTransitionGestureKind244.Idle;
    }

    void RefreshLod()
    {
        var zoom = viewTransform.scale.x;
        for (var i = 0; i < nodeViews.Count; i++)
        {
            if (nodeViews[i] is AnimTransitionNodeView nodeView) nodeView.UpdateLod(zoom);
        }
    }
}

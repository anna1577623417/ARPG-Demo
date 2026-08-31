#if UNITY_EDITOR
using GraphProcessor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Canvas-local rectangle selector. Trickle-down capture prevents GraphView's built-in selector
/// from mixing panel and absolute-layout coordinates without modifying GraphProcessor.
/// </summary>
public sealed class AnimTransitionRectangleSelector244
{
    const float DragThreshold = 3f;

    readonly AnimTransitionGraphView view;
    readonly AnimTransitionGestureState244 gestures;
    readonly VisualElement marquee;
    Vector2 startCanvas;
    bool additive;

    public bool IsActive { get; private set; }

    public AnimTransitionRectangleSelector244(
        AnimTransitionGraphView graphView,
        AnimTransitionGestureState244 gestureState)
    {
        view = graphView;
        gestures = gestureState;
        marquee = CreateMarquee();
        view.hierarchy.Add(marquee);
        view.RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
        view.RegisterCallback<MouseMoveEvent>(OnMouseMove, TrickleDown.TrickleDown);
        view.RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
        view.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
    }

    VisualElement CreateMarquee()
    {
        var element = new VisualElement
        {
            name = "AnimTransitionRectangleSelector244",
            pickingMode = PickingMode.Ignore,
        };
        element.style.position = Position.Absolute;
        element.style.display = DisplayStyle.None;
        element.style.backgroundColor = new Color(0.24f, 0.58f, 0.82f, 0.12f);
        element.style.borderLeftWidth = 1f;
        element.style.borderRightWidth = 1f;
        element.style.borderTopWidth = 1f;
        element.style.borderBottomWidth = 1f;
        element.style.borderLeftColor = new Color(0.56f, 0.82f, 1f, 0.95f);
        element.style.borderRightColor = new Color(0.56f, 0.82f, 1f, 0.95f);
        element.style.borderTopColor = new Color(0.56f, 0.82f, 1f, 0.95f);
        element.style.borderBottomColor = new Color(0.56f, 0.82f, 1f, 0.95f);
        return element;
    }

    void OnMouseDown(MouseDownEvent evt)
    {
        if (evt.button != 0 || evt.altKey || IsInteractiveTarget(evt.target as VisualElement)) return;

        IsActive = true;
        additive = evt.shiftKey || evt.actionKey;
        startCanvas = AnimTransitionCanvasCoordinates244.PanelToCanvas(view, evt.mousePosition);
        UpdateMarquee(startCanvas);
        marquee.style.display = DisplayStyle.Flex;
        marquee.BringToFront();
        view.CaptureMouse();
        view.Focus();
        gestures.Begin(AnimTransitionGestureKind244.BoxSelect, "canvas-background");
        view.Window.FocusController.IsCanvasGestureActive = true;
        evt.StopImmediatePropagation();
    }

    void OnMouseMove(MouseMoveEvent evt)
    {
        if (!IsActive) return;
        var current = AnimTransitionCanvasCoordinates244.PanelToCanvas(view, evt.mousePosition);
        UpdateMarquee(current);
        evt.StopImmediatePropagation();
    }

    void OnMouseUp(MouseUpEvent evt)
    {
        if (!IsActive || evt.button != 0) return;
        var current = AnimTransitionCanvasCoordinates244.PanelToCanvas(view, evt.mousePosition);
        var canvasRect = AnimTransitionCanvasCoordinates244.NormalizeRect(startCanvas, current);
        if (canvasRect.width >= DragThreshold || canvasRect.height >= DragThreshold)
        {
            var graphRect = AnimTransitionCanvasCoordinates244.CanvasToGraph(view, canvasRect);
            view.SelectNodesInGraphRect(graphRect, additive);
        }
        else if (!additive)
        {
            view.ClearSelection();
        }

        End("pointer-up");
        evt.StopImmediatePropagation();
    }

    void OnKeyDown(KeyDownEvent evt)
    {
        if (!IsActive || evt.keyCode != KeyCode.Escape) return;
        End("escape");
        evt.StopImmediatePropagation();
    }

    void UpdateMarquee(Vector2 currentCanvas)
    {
        var rect = AnimTransitionCanvasCoordinates244.NormalizeRect(startCanvas, currentCanvas);
        marquee.style.left = rect.xMin;
        marquee.style.top = rect.yMin;
        marquee.style.width = rect.width;
        marquee.style.height = rect.height;
    }

    void End(string reason)
    {
        if (!IsActive) return;
        IsActive = false;
        if (view.HasMouseCapture()) view.ReleaseMouse();
        marquee.style.display = DisplayStyle.None;
        gestures.End(reason);
        view.Window.FocusController.IsCanvasGestureActive = false;
    }

    bool IsInteractiveTarget(VisualElement target)
    {
        for (var current = target; current != null && current != view; current = current.parent)
        {
            if (current is GraphElement || current is PortView || current is TextField || current is Button)
            {
                return true;
            }
        }

        return false;
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Single explicit coordinate boundary for the transition graph editor.</summary>
public static class AnimTransitionCanvasCoordinates244
{
    public static Vector2 PanelToCanvas(VisualElement canvas, Vector2 panelPosition)
    {
        return canvas == null ? panelPosition : canvas.WorldToLocal(panelPosition);
    }

    public static Vector2 CanvasToGraph(GraphView view, Vector2 canvasPosition)
    {
        return view == null || view.contentViewContainer == null
            ? canvasPosition
            : view.ChangeCoordinatesTo(view.contentViewContainer, canvasPosition);
    }

    public static Vector2 GraphToCanvas(GraphView view, Vector2 graphPosition)
    {
        return view == null || view.contentViewContainer == null
            ? graphPosition
            : view.contentViewContainer.ChangeCoordinatesTo(view, graphPosition);
    }

    public static Rect CanvasToGraph(GraphView view, Rect canvasRect)
    {
        var first = CanvasToGraph(view, canvasRect.min);
        var second = CanvasToGraph(view, canvasRect.max);
        return NormalizeRect(first, second);
    }

    public static Vector2 CanvasToGraph(Vector2 canvasPosition, Vector2 pan, Vector2 scale)
    {
        return new Vector2(
            SafeDivide(canvasPosition.x - pan.x, scale.x),
            SafeDivide(canvasPosition.y - pan.y, scale.y));
    }

    public static Vector2 GraphToCanvas(Vector2 graphPosition, Vector2 pan, Vector2 scale)
    {
        return new Vector2(graphPosition.x * scale.x + pan.x, graphPosition.y * scale.y + pan.y);
    }

    public static Rect NormalizeRect(Vector2 first, Vector2 second)
    {
        var min = Vector2.Min(first, second);
        var max = Vector2.Max(first, second);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    static float SafeDivide(float value, float divisor)
    {
        return Mathf.Abs(divisor) < 0.0001f ? value : value / divisor;
    }
}
#endif

#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

public enum AnimTransitionEdgeProjection244
{
    Hidden = 0,
    Orthogonal = 1,
    LiveStraight = 2,
}

/// <summary>Transient straight-line projection. The native EdgeControl remains transparent for hit-testing.</summary>
public sealed class AnimTransitionLiveEdgePreview244 : VisualElement
{
    static readonly Color Stroke = new Color(0.66f, 0.86f, 1f, 0.98f);

    Vector2 from;
    Vector2 to;
    bool emphasized;

    public AnimTransitionLiveEdgePreview244()
    {
        pickingMode = PickingMode.Ignore;
        generateVisualContent += OnGenerateVisualContent;
        style.position = Position.Absolute;
        style.left = 0;
        style.top = 0;
        style.right = 0;
        style.bottom = 0;
        style.display = DisplayStyle.None;
    }

    public void SetEndpoints(Vector2 start, Vector2 end, bool isEmphasized)
    {
        from = start;
        to = end;
        emphasized = isEmphasized;
        MarkDirtyRepaint();
    }

    public void SetVisible(bool visible)
    {
        style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public static AnimTransitionEdgeProjection244 ResolveProjection(
        AnimTransitionGestureKind244 gesture,
        bool isTransientEdge)
    {
        return ResolveProjection(gesture, isTransientEdge, true);
    }

    public static AnimTransitionEdgeProjection244 ResolveProjection(
        AnimTransitionGestureKind244 gesture,
        bool isTransientEdge,
        bool isAffected)
    {
        if (isAffected && (gesture == AnimTransitionGestureKind244.NodeDrag
            || gesture == AnimTransitionGestureKind244.Reconnect))
        {
            return AnimTransitionEdgeProjection244.LiveStraight;
        }

        if (isAffected && gesture == AnimTransitionGestureKind244.Connect && isTransientEdge)
        {
            return AnimTransitionEdgeProjection244.LiveStraight;
        }

        return isTransientEdge
            ? AnimTransitionEdgeProjection244.Hidden
            : AnimTransitionEdgeProjection244.Orthogonal;
    }

    void OnGenerateVisualContent(MeshGenerationContext context)
    {
        var painter = context.painter2D;
        painter.strokeColor = Stroke;
        painter.lineWidth = emphasized ? 2.8f : 2.2f;
        painter.lineCap = LineCap.Round;
        painter.BeginPath();
        painter.MoveTo(from);
        var direction = to.x >= from.x ? 1f : -1f;
        var tangent = Mathf.Max(32f, Mathf.Abs(to.x - from.x) * 0.5f);
        painter.BezierCurveTo(
            from + new Vector2(direction * tangent, 0f),
            to - new Vector2(direction * tangent, 0f),
            to);
        painter.Stroke();
    }
}
#endif

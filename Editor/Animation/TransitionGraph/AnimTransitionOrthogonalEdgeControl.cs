#if UNITY_EDITOR
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Low-contrast orthogonal overlay. Hit-testing stays on GraphProcessor EdgeControl.</summary>
public sealed class AnimTransitionOrthogonalEdgeControl : VisualElement
{
    static readonly Color Stroke = new Color(0.42f, 0.52f, 0.60f, 0.88f);
    static readonly Color Hover = new Color(0.72f, 0.82f, 0.90f, 0.95f);

    Vector2 from;
    Vector2 to;
    bool hovered;

    public AnimTransitionOrthogonalEdgeControl()
    {
        pickingMode = PickingMode.Ignore;
        generateVisualContent += OnGenerateVisualContent;
        style.position = Position.Absolute;
        style.left = 0;
        style.top = 0;
        style.right = 0;
        style.bottom = 0;
    }

    public void SetEndpoints(Vector2 start, Vector2 end, bool isHovered)
    {
        from = start;
        to = end;
        hovered = isHovered;
        MarkDirtyRepaint();
    }

    public void SetVisible(bool visible)
    {
        style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    void OnGenerateVisualContent(MeshGenerationContext context)
    {
        var painter = context.painter2D;
        painter.strokeColor = hovered ? Hover : Stroke;
        painter.lineWidth = hovered ? 2.4f : 1.6f;
        painter.lineCap = LineCap.Round;
        painter.lineJoin = LineJoin.Round;
        var midX = (from.x + to.x) * 0.5f;
        painter.BeginPath();
        painter.MoveTo(from);
        painter.LineTo(new Vector2(midX, from.y));
        painter.LineTo(new Vector2(midX, to.y));
        painter.LineTo(to);
        painter.Stroke();
    }
}
#endif

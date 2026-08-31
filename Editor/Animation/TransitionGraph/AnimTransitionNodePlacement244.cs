#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

public readonly struct AnimTransitionNodePlacementResult244
{
    public readonly bool Succeeded;
    public readonly Vector2 Position;
    public readonly int Attempts;
    public readonly string Reason;

    public AnimTransitionNodePlacementResult244(bool succeeded, Vector2 position, int attempts, string reason)
    {
        Succeeded = succeeded;
        Position = position;
        Attempts = attempts;
        Reason = reason ?? string.Empty;
    }
}

/// <summary>Pure, bounded placement core shared by Library and Quick Add callers.</summary>
public static class AnimTransitionNodePlacement244
{
    public const float Gap = 48f;
    public const float Grid = 24f;
    public const int MaxAttempts = 32;

    public static Vector2 EstimateNodeSize(System.Type nodeType)
    {
        if (nodeType == typeof(AnimGraphEntryNode) || nodeType == typeof(AnimGraphOutputNode)) return new Vector2(190f, 96f);
        if (nodeType == typeof(AnimGraphPredicateNode)) return new Vector2(210f, 120f);
        if (nodeType == typeof(AnimGraphSelectorNode)) return new Vector2(220f, 128f);
        if (nodeType == typeof(AnimGraphVariantNode) || nodeType == typeof(AnimGraphSubGraphNode)) return new Vector2(230f, 112f);
        if (nodeType == typeof(AnimGraphTransitionFamilyNode244) || nodeType == typeof(AnimGraphExceptionRuleNode244)) return new Vector2(280f, 160f);
        if (nodeType == typeof(AnimGraphPresentationResolveNode244)) return new Vector2(280f, 176f);
        if (nodeType == typeof(AnimGraphDomainEntryNode244) || nodeType == typeof(AnimGraphPolicyProfileNode244) || nodeType == typeof(AnimGraphDefaultFallbackNode244)) return new Vector2(250f, 128f);
        return new Vector2(240f, 112f);
    }

    public static AnimTransitionNodePlacementResult244 TryPlace(
        in AnimTransitionNodeCreateIntent244 intent,
        IList<Rect> existing)
    {
        var size = intent.RequestedSize;
        var anchor = intent.Anchor;
        var hasAnchor = anchor.width > 0f && anchor.height > 0f;
        var center = hasAnchor
            ? new Vector2(anchor.xMax + Gap, anchor.center.y - size.y * 0.5f)
            : intent.Viewport.center - size * 0.5f;
        if (intent.Viewport.width <= 0f || intent.Viewport.height <= 0f)
        {
            return TryCandidate(center, size, existing, default, false, 1);
        }

        var viewport = intent.Viewport;
        for (var i = 0; i < MaxAttempts; i++)
        {
            var offset = SpiralOffset(i) * Grid;
            var candidate = Clamp(new Vector2(center.x + offset.x, center.y + offset.y), size, viewport);
            if (!Overlaps(new Rect(candidate, size), existing))
            {
                return new AnimTransitionNodePlacementResult244(true, candidate, i + 1, string.Empty);
            }
        }

        return new AnimTransitionNodePlacementResult244(
            false,
            Clamp(center, size, viewport),
            MaxAttempts,
            "No safe placement slot in the visible graph viewport.");
    }

    static AnimTransitionNodePlacementResult244 TryCandidate(
        Vector2 position,
        Vector2 size,
        IList<Rect> existing,
        Rect viewport,
        bool clamp,
        int attempts)
    {
        var candidate = clamp ? Clamp(position, size, viewport) : position;
        return Overlaps(new Rect(candidate, size), existing)
            ? new AnimTransitionNodePlacementResult244(false, candidate, attempts, "Placement overlaps an existing node.")
            : new AnimTransitionNodePlacementResult244(true, candidate, attempts, string.Empty);
    }

    static bool Overlaps(Rect candidate, IList<Rect> existing)
    {
        if (existing == null) return false;
        for (var i = 0; i < existing.Count; i++)
        {
            if (candidate.Overlaps(existing[i], true)) return true;
        }

        return false;
    }

    static Vector2 Clamp(Vector2 position, Vector2 size, Rect viewport)
    {
        var min = viewport.min;
        var max = viewport.max - size;
        return new Vector2(Mathf.Clamp(position.x, min.x, Mathf.Max(min.x, max.x)),
            Mathf.Clamp(position.y, min.y, Mathf.Max(min.y, max.y)));
    }

    static Vector2 SpiralOffset(int index)
    {
        if (index == 0) return Vector2.zero;
        var layer = Mathf.CeilToInt((Mathf.Sqrt(index + 1f) - 1f) * 0.5f);
        var side = layer * 2;
        var leg = index - (2 * layer - 1) * (2 * layer - 1);
        var edge = Mathf.Clamp(leg / Mathf.Max(1, side), 0, 3);
        var along = leg % Mathf.Max(1, side) - layer;
        switch (edge)
        {
            case 0: return new Vector2(along, -layer);
            case 1: return new Vector2(layer, along);
            case 2: return new Vector2(-along, layer);
            default: return new Vector2(-layer, -along);
        }
    }
}
#endif

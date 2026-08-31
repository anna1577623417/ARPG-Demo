#if UNITY_EDITOR
using System;
using UnityEngine;

public enum AnimTransitionNodeCreateSource244 : byte
{
    Library = 0,
    QuickAdd = 1,
    Contextual = 2,
}

/// <summary>Editor-only intent. It contains no GraphElement or serialized node reference.</summary>
public readonly struct AnimTransitionNodeCreateIntent244
{
    public readonly Type NodeType;
    public readonly AnimTransitionNodeCreateSource244 Source;
    public readonly Rect Anchor;
    public readonly Rect Viewport;
    public readonly Vector2 RequestedSize;

    public AnimTransitionNodeCreateIntent244(
        Type nodeType,
        AnimTransitionNodeCreateSource244 source,
        Rect anchor,
        Rect viewport,
        Vector2 requestedSize)
    {
        NodeType = nodeType;
        Source = source;
        Anchor = anchor;
        Viewport = viewport;
        RequestedSize = new Vector2(Mathf.Max(1f, requestedSize.x), Mathf.Max(1f, requestedSize.y));
    }
}
#endif

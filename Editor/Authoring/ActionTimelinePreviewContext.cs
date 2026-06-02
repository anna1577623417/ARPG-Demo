#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Action 时间轴 Scene 预览上下文（141.1 SceneBridge 单点输入）。
/// </summary>
internal readonly struct ActionTimelinePreviewContext
{
    public ActionDataSO Action { get; }
    public float NormalizedTime { get; }
    public Transform Anchor { get; }
    public Vector3 Position { get; }
    public Vector3 PlanarForward { get; }
    public ulong ActiveStateBits { get; }
    public bool HasAnchor { get; }

    ActionTimelinePreviewContext(
        ActionDataSO action,
        float normalizedTime,
        Transform anchor,
        Vector3 position,
        Vector3 planarForward,
        ulong activeStateBits,
        bool hasAnchor)
    {
        Action = action;
        NormalizedTime = normalizedTime;
        Anchor = anchor;
        Position = position;
        PlanarForward = planarForward;
        ActiveStateBits = activeStateBits;
        HasAnchor = hasAnchor;
    }

    public static ActionTimelinePreviewContext Build(
        ActionDataSO action,
        float normalizedTime,
        Transform anchorOverride)
    {
        if (action == null)
        {
            return default;
        }

        var anchor = ResolveAnchor(anchorOverride);
        var t = Mathf.Clamp01(normalizedTime);
        if (anchor == null)
        {
            return new ActionTimelinePreviewContext(action, t, null, default, default, 0, false);
        }

        var fwd = anchor.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f)
        {
            fwd = Vector3.forward;
        }
        else
        {
            fwd.Normalize();
        }

        var mask = default(GameplayTagMask);
        action.EvaluatePhaseTags(t, ref mask);

        return new ActionTimelinePreviewContext(
            action,
            t,
            anchor,
            anchor.position,
            fwd,
            mask.Value,
            true);
    }

    static Transform ResolveAnchor(Transform anchorOverride)
    {
        if (anchorOverride != null)
        {
            return anchorOverride;
        }

        if (Selection.activeTransform != null)
        {
            return Selection.activeTransform;
        }

        var player = Object.FindFirstObjectByType<Player>();
        return player != null ? player.transform : null;
    }
}
#endif

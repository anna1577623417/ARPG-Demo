using UnityEngine;

/// <summary>
/// 178.1 — AnimationTransitionService.Resolve 返回值。
/// 调用方按 Mode 决定走 CrossFade / Snap / PhaseMatch 哪条路径。
/// </summary>
public readonly struct TransitionParams
{
    public readonly float BlendDuration;
    public readonly TransitionMode Mode;
    public readonly AnimationCurve EaseCurve;
    public readonly TransitionResolveLayer Layer;

    public TransitionParams(
        float blendDuration,
        TransitionMode mode,
        AnimationCurve easeCurve,
        TransitionResolveLayer layer)
    {
        BlendDuration = blendDuration;
        Mode = mode;
        EaseCurve = easeCurve;
        Layer = layer;
    }

    public static TransitionParams DefaultFallback(float dur)
        => new TransitionParams(dur, TransitionMode.CrossFade, null, TransitionResolveLayer.Default);
}

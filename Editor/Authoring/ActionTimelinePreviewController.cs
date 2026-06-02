#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 时间轴 → 场景角色 Pose 采样（141.1 PreviewController）。
/// 使用 AnimationMode，不写入运行时 Animator 状态。
/// </summary>
internal sealed class ActionTimelinePreviewController
{
    Transform _lastSampleRoot;
    AnimationClip _lastClip;
    bool _ownsAnimationMode;

    public bool Enabled { get; set; } = true;

    public void SamplePose(in ActionTimelinePreviewContext ctx)
    {
        if (!Enabled || !ctx.HasAnchor || ctx.Action == null)
        {
            return;
        }

        var clip = ctx.Action.MainClip;
        if (clip == null)
        {
            return;
        }

        var sampleRoot = ResolveSampleRoot(ctx.Anchor);
        if (sampleRoot == null)
        {
            return;
        }

        var sampleSeconds = ResolveSampleSeconds(ctx.Action, ctx.NormalizedTime, clip);
        EnsureAnimationMode();
        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(sampleRoot.gameObject, clip, sampleSeconds);
        AnimationMode.EndSampling();

        _lastSampleRoot = sampleRoot;
        _lastClip = clip;
    }

    public void Stop()
    {
        if (_ownsAnimationMode && AnimationMode.InAnimationMode())
        {
            AnimationMode.StopAnimationMode();
        }

        _ownsAnimationMode = false;
        _lastSampleRoot = null;
        _lastClip = null;
    }

    static Transform ResolveSampleRoot(Transform anchor)
    {
        if (anchor == null)
        {
            return null;
        }

        var animator = anchor.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            return animator.transform;
        }

        return anchor;
    }

    static float ResolveSampleSeconds(ActionDataSO action, float normalizedTime, AnimationClip clip)
    {
        var logicDuration = action.ResolveLogicalDurationSeconds();
        if (logicDuration > 0.001f)
        {
            return Mathf.Clamp01(normalizedTime) * logicDuration;
        }

        return Mathf.Clamp01(normalizedTime) * clip.length;
    }

    void EnsureAnimationMode()
    {
        if (!AnimationMode.InAnimationMode())
        {
            AnimationMode.StartAnimationMode();
            _ownsAnimationMode = true;
        }
    }
}
#endif

using UnityEngine;

/// <summary>
/// Action 时间权威（144.1 + 145.1）：三条轴 — Action(nt)、Motion(nt)、Clip(nt×Ratio)。
/// AnimSpeed = (Clip×Ratio)÷Duration；Ratio 只裁动画，不裁 Motion 位移。
/// </summary>
public static class ActionTimeAuthority
{
    const float MinDuration = 0.0001f;
    const float MinScale = 0.01f;

    public static float ResolveAnimationEndRatio(ActionDataSO action) =>
        action != null ? Mathf.Clamp(action.AnimationEndRatio, 0.05f, 2f) : 1f;

    /// <summary>未应用属性前的逻辑时长：直接读 <see cref="ActionDataSO.Duration"/>，无 Duration 时用 Clip÷AnimSpeed。</summary>
    public static float ResolveAuthoredLogicDurationSeconds(ActionDataSO action)
    {
        if (action == null)
        {
            return 0.4f;
        }

        if (action.Duration > MinDuration)
        {
            return action.Duration;
        }

        if (action.MainClip != null && action.AnimSpeed > MinScale)
        {
            return action.MainClip.length / action.AnimSpeed;
        }

        return 0.4f;
    }

    /// <summary>属性缩放后的逻辑时长：Authored / DurationScale。</summary>
    public static float ResolveLogicDurationSeconds(
        ActionDataSO action,
        IStatsProvider stats = null,
        float editorDurationScalePreview = 1f)
    {
        var authored = ResolveAuthoredLogicDurationSeconds(action);
        if (action == null || action.DurationStatScaling == MotionScaleType.None)
        {
            return authored;
        }

        var scale = ResolveDurationScale(action, stats, editorDurationScalePreview);
        return authored / Mathf.Max(MinScale, scale);
    }

    public static float ResolveDurationScale(
        ActionDataSO action,
        IStatsProvider stats = null,
        float editorDurationScalePreview = 1f)
    {
        if (action == null || action.DurationStatScaling == MotionScaleType.None)
        {
            return 1f;
        }

        if (stats != null)
        {
            return Mathf.Max(MinScale, stats.GetDurationScale(action.DurationStatScaling));
        }

        return Mathf.Max(MinScale, editorDurationScalePreview);
    }

    /// <summary>Motion 曲线采样时间 = NormalizedTime（100% 位移在 Duration 结束，不受 Ratio 影响）。</summary>
    public static float MapNormalizedTimeToMotionTime(float normalizedTime) =>
        Mathf.Clamp01(normalizedTime);

    /// <summary>Clip 表现进度 = NormalizedTime × AnimationEndRatio（仅动画，不影响 Motion）。</summary>
    public static float MapNormalizedTimeToClipProgress(float normalizedTime, ActionDataSO action) =>
        MapNormalizedTimeToClipProgress(normalizedTime, ResolveAnimationEndRatio(action));

    public static float MapNormalizedTimeToClipProgress(float normalizedTime, float animationEndRatio) =>
        Mathf.Clamp01(normalizedTime) * Mathf.Max(0.05f, animationEndRatio);

    [System.Obsolete("Motion 使用 MapNormalizedTimeToMotionTime；Clip 使用 MapNormalizedTimeToClipProgress。")]
    public static float MapNormalizedTimeToMotionProgress(float normalizedTime, ActionDataSO action) =>
        MapNormalizedTimeToClipProgress(normalizedTime, action);

    [System.Obsolete("Motion 使用 MapNormalizedTimeToMotionTime；Clip 使用 MapNormalizedTimeToClipProgress。")]
    public static float MapNormalizedTimeToMotionProgress(float normalizedTime, float animationEndRatio) =>
        MapNormalizedTimeToClipProgress(normalizedTime, animationEndRatio);

    /// <summary>
    /// AnimSpeed = (ClipLength × Ratio) / Duration — 使 Clip 在 Duration 内到达 Ratio%，不影响 Motion。
    /// </summary>
    public static float ComputeAnimSpeed(ActionDataSO action)
    {
        if (action == null)
        {
            return 1f;
        }

        if (action.MainClip == null)
        {
            return Mathf.Max(MinScale, action.AnimSpeed);
        }

        var duration = action.Duration;
        if (duration <= MinDuration)
        {
            return Mathf.Max(MinScale, action.AnimSpeed);
        }

        var ratio = ResolveAnimationEndRatio(action);
        return action.MainClip.length * ratio / duration;
    }

    /// <summary>从旧 AnimSpeed 反推 AnimationEndRatio（迁移）。</summary>
    public static float InferAnimationEndRatioFromAnimSpeed(ActionDataSO action)
    {
        if (action?.MainClip == null)
        {
            return 1f;
        }

        var duration = action.Duration;
        if (duration <= MinDuration)
        {
            return 1f;
        }

        var ratio = action.AnimSpeed * duration / action.MainClip.length;
        return Mathf.Clamp(ratio, 0.05f, 2f);
    }

    /// <summary>挂载 MotionProfile 曲线 t=0→1 的主轴总位移（米）。</summary>
    public static float MeasurePrincipalAxisDisplacementMeters(ActionDataSO action)
    {
        if (action?.MotionProfile == null)
        {
            return 0f;
        }

        return action.MotionProfile.MeasurePrincipalAxisDisplacementMeters(action.PrincipalAxis);
    }

    /// <summary>Action 结束时 Motion 主轴总位移（恒为曲线 t=0→1，与 Ratio 无关）。</summary>
    public static float MeasurePrincipalAxisDisplacementAtActionEnd(ActionDataSO action) =>
        MeasurePrincipalAxisDisplacementMeters(action);

    public static float ComputeSuggestedDurationFromRatio(ActionDataSO action)
    {
        if (action?.MainClip == null)
        {
            return action != null ? action.Duration : 0.4f;
        }

        return action.MainClip.length * ResolveAnimationEndRatio(action);
    }

    public static float ComputeSuggestedRatioFromDuration(ActionDataSO action)
    {
        if (action?.MainClip == null || action.MainClip.length <= MinDuration)
        {
            return 1f;
        }

        if (action.Duration <= MinDuration)
        {
            return 1f;
        }

        return Mathf.Clamp(action.Duration / action.MainClip.length, 0.05f, 2f);
    }

#if UNITY_EDITOR
    public static void SyncAnimSpeedFromAuthority(ActionDataSO action)
    {
        if (action == null)
        {
            return;
        }

        action.AnimSpeed = ComputeAnimSpeed(action);
        // 调用方负责 Undo / ApplyModifiedProperties；勿在此 SetDirty 以免 Inspector 重入。
    }
#endif
}

using UnityEngine;

/// <summary>
/// Action 时间权威（144.1 + 145.1 + 172.1 Segment）：
/// Action(nt) | Motion t=nt | Clip = Lerp(SegmentStart, SegmentEnd, nt)。
/// AnimSpeed = Clip.length × SegmentLength ÷ Duration。
/// </summary>
public static class ActionTimeAuthority
{
    const float MinDuration = 0.0001f;
    const float MinScale = 0.01f;
    const float MinSegmentSpan = 0.001f;

    public static float ResolveSegmentStart(ActionDataSO action) =>
        action != null ? Mathf.Clamp01(action.SegmentStart) : 0f;

    public static float ResolveSegmentEnd(ActionDataSO action)
    {
        if (action == null)
        {
            return 1f;
        }

        var start = ResolveSegmentStart(action);
        return Mathf.Clamp(action.SegmentEnd, start + MinSegmentSpan, 1f);
    }

    public static float ResolveSegmentLength(ActionDataSO action) =>
        ResolveSegmentEnd(action) - ResolveSegmentStart(action);

    /// <summary>Action 归一化时间 → Clip 归一化进度 [SegmentStart, SegmentEnd]。</summary>
    public static float MapActionTimeToClipNormalized(float actionNormalizedTime, ActionDataSO action)
    {
        if (action == null)
        {
            return Mathf.Clamp01(actionNormalizedTime);
        }

        return MapActionTimeToClipNormalized(
            actionNormalizedTime,
            ResolveSegmentStart(action),
            ResolveSegmentEnd(action));
    }

    public static float MapActionTimeToClipNormalized(
        float actionNormalizedTime,
        float segmentStart,
        float segmentEnd)
    {
        var t = Mathf.Clamp01(actionNormalizedTime);
        var start = Mathf.Clamp01(segmentStart);
        var end = Mathf.Clamp(segmentEnd, start + MinSegmentSpan, 1f);
        return Mathf.Lerp(start, end, t);
    }

    /// <summary>
    /// Clip 归一化进度 → Action nt。Stop 点按尾段按 Clip 空间 authoring，
    /// Presentation 仍走 MapActionTimeToClipNormalized，所以这里先反解。
    /// </summary>
    public static float MapClipNormalizedToActionTime(float clipNormalized, ActionDataSO action)
    {
        if (action == null)
        {
            return Mathf.Clamp01(clipNormalized);
        }

        var start = ResolveSegmentStart(action);
        var end = ResolveSegmentEnd(action);
        var span = end - start;
        if (span < MinSegmentSpan)
        {
            return 0f;
        }

        return Mathf.Clamp01((Mathf.Clamp01(clipNormalized) - start) / span);
    }

    public static float MapActionTimeToClipSeconds(float actionNormalizedTime, ActionDataSO action)
    {
        if (action?.MainClip == null)
        {
            return 0f;
        }

        return MapActionTimeToClipNormalized(actionNormalizedTime, action) * action.MainClip.length;
    }

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
            return action.MainClip.length * ResolveSegmentLength(action) / action.AnimSpeed;
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

    /// <summary>Motion 曲线采样时间 = NormalizedTime（100% 位移在 Duration 结束，不受 Segment 影响）。</summary>
    public static float MapNormalizedTimeToMotionTime(float normalizedTime) =>
        Mathf.Clamp01(normalizedTime);

    /// <summary>Clip 表现进度；172.1 起等价于 <see cref="MapActionTimeToClipNormalized"/>。</summary>
    public static float MapNormalizedTimeToClipProgress(float normalizedTime, ActionDataSO action) =>
        MapActionTimeToClipNormalized(normalizedTime, action);

    [System.Obsolete("Use MapActionTimeToClipNormalized with segmentStart/segmentEnd.")]
    public static float MapNormalizedTimeToClipProgress(float normalizedTime, float legacyEndRatio) =>
        MapActionTimeToClipNormalized(normalizedTime, 0f, legacyEndRatio);

    [System.Obsolete("Motion 使用 MapNormalizedTimeToMotionTime；Clip 使用 MapActionTimeToClipNormalized。")]
    public static float MapNormalizedTimeToMotionProgress(float normalizedTime, ActionDataSO action) =>
        MapActionTimeToClipNormalized(normalizedTime, action);

    [System.Obsolete("Motion 使用 MapNormalizedTimeToMotionTime；Clip 使用 MapActionTimeToClipNormalized。")]
    public static float MapNormalizedTimeToMotionProgress(float normalizedTime, float legacyEndRatio) =>
        MapActionTimeToClipNormalized(normalizedTime, 0f, legacyEndRatio);

    /// <summary>
    /// AnimSpeed = Clip.length × SegmentLength / Duration — Segment 在 Duration 内播完，不影响 Motion。
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

        var segmentLen = ResolveSegmentLength(action);
        return action.MainClip.length * segmentLen / duration;
    }

    /// <summary>从手调 AnimSpeed 反推 SegmentEnd（SegmentStart 保持不变）。</summary>
    public static float InferSegmentEndFromAnimSpeed(ActionDataSO action)
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

        var segmentLen = action.AnimSpeed * duration / action.MainClip.length;
        return Mathf.Clamp(
            ResolveSegmentStart(action) + segmentLen,
            ResolveSegmentStart(action) + MinSegmentSpan,
            1f);
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

    /// <summary>Action 结束时 Motion 主轴总位移（恒为曲线 t=0→1，与 Segment 无关）。</summary>
    public static float MeasurePrincipalAxisDisplacementAtActionEnd(ActionDataSO action) =>
        MeasurePrincipalAxisDisplacementMeters(action);

    public static float ComputeSuggestedDurationFromSegment(ActionDataSO action)
    {
        if (action?.MainClip == null)
        {
            return action != null ? action.Duration : 0.4f;
        }

        return action.MainClip.length * ResolveSegmentLength(action);
    }

    public static float ComputeSuggestedSegmentEndFromDuration(ActionDataSO action)
    {
        if (action?.MainClip == null || action.MainClip.length <= MinDuration)
        {
            return 1f;
        }

        if (action.Duration <= MinDuration)
        {
            return 1f;
        }

        var segmentLen = action.Duration / action.MainClip.length;
        return Mathf.Clamp(
            ResolveSegmentStart(action) + segmentLen,
            ResolveSegmentStart(action) + MinSegmentSpan,
            1f);
    }

    public static void NormalizeSegmentRange(ActionDataSO action)
    {
        if (action == null)
        {
            return;
        }

        action.SegmentStart = Mathf.Clamp01(action.SegmentStart);
        action.SegmentEnd = Mathf.Clamp(action.SegmentEnd, action.SegmentStart + MinSegmentSpan, 1f);
    }

    /// <summary>Clip 片段墙钟秒长。</summary>
    public static float ResolveSegmentWallSeconds(ActionDataSO action)
    {
        if (action?.MainClip == null)
        {
            return 0f;
        }

        return action.MainClip.length * ResolveSegmentLength(action);
    }

    /// <summary>离线 Motion Retiming 预览/烘焙结果（运行时只读已写入的 Duration / AnimSpeed）。</summary>
    public readonly struct MotionRetimingResult
    {
        public readonly bool IsValid;
        public readonly float MainDistanceMeters;
        public readonly float ReferenceSpeed;
        public readonly float Duration;
        public readonly float AnimSpeed;
        public readonly float UnclampedAnimSpeed;
        public readonly bool AnimSpeedWasClamped;
        public readonly string Warning;

        public MotionRetimingResult(
            bool isValid,
            float mainDistanceMeters,
            float referenceSpeed,
            float duration,
            float animSpeed,
            float unclampedAnimSpeed,
            bool animSpeedWasClamped,
            string warning)
        {
            IsValid = isValid;
            MainDistanceMeters = mainDistanceMeters;
            ReferenceSpeed = referenceSpeed;
            Duration = duration;
            AnimSpeed = animSpeed;
            UnclampedAnimSpeed = unclampedAnimSpeed;
            AnimSpeedWasClamped = animSpeedWasClamped;
            Warning = warning;
        }
    }

    /// <summary>
    /// Duration = MainDistance / ReferenceSpeed；AnimSpeed = SegmentWall / Duration；
    /// Clamp 后 Duration = SegmentWall / AnimSpeed。
    /// </summary>
    public static MotionRetimingResult ComputeMotionRetiming(
        ActionDataSO action,
        float referenceSpeed,
        float minAnimSpeed = 0.85f,
        float maxAnimSpeed = 1.15f)
    {
        if (action == null)
        {
            return InvalidRetiming(referenceSpeed, "Action 为空。");
        }

        if (referenceSpeed <= MinScale)
        {
            return InvalidRetiming(referenceSpeed, "Reference Speed 须 > 0。");
        }

        if (action.MotionProfile == null || !action.MotionProfile.UsesAxisCurves)
        {
            return InvalidRetiming(referenceSpeed, "需要 MotionProfile 且已提取 AxisCurves。");
        }

        if (action.MainClip == null)
        {
            return InvalidRetiming(referenceSpeed, "需要 MainClip。");
        }

        var segWall = ResolveSegmentWallSeconds(action);
        if (segWall <= MinDuration)
        {
            return InvalidRetiming(referenceSpeed, "Segment 墙钟时长无效。");
        }

        var mainDistance = MeasurePrincipalAxisDisplacementMeters(action);
        if (mainDistance <= MinScale)
        {
            return InvalidRetiming(referenceSpeed, $"主轴 {action.PrincipalAxis} 位移过小。");
        }

        minAnimSpeed = Mathf.Max(MinScale, minAnimSpeed);
        maxAnimSpeed = Mathf.Max(minAnimSpeed, maxAnimSpeed);

        var duration = mainDistance / referenceSpeed;
        var unclampedAnimSpeed = segWall / duration;
        var animSpeed = Mathf.Clamp(unclampedAnimSpeed, minAnimSpeed, maxAnimSpeed);
        var clamped = !Mathf.Approximately(animSpeed, unclampedAnimSpeed);
        if (clamped)
        {
            duration = segWall / animSpeed;
        }

        string warning = null;
        if (clamped)
        {
            warning =
                $"AnimSpeed {unclampedAnimSpeed:F3} 超出 [{minAnimSpeed:F2}, {maxAnimSpeed:F2}]，" +
                "已 Clamp 并重算 Duration。";
        }

        return new MotionRetimingResult(
            true,
            mainDistance,
            referenceSpeed,
            duration,
            animSpeed,
            unclampedAnimSpeed,
            clamped,
            warning);
    }

    static MotionRetimingResult InvalidRetiming(float referenceSpeed, string warning) =>
        new MotionRetimingResult(false, 0f, referenceSpeed, 0f, 0f, 0f, false, warning);

#if UNITY_EDITOR
    /// <summary>将离线 Retiming 写入 Action；关闭 AutoSync，运行时不再推导 AnimSpeed。</summary>
    public static void ApplyMotionRetiming(ActionDataSO action, MotionRetimingResult result)
    {
        if (action == null || !result.IsValid)
        {
            return;
        }

        action.ReferenceMotionSpeed = result.ReferenceSpeed;
        action.Duration = result.Duration;
        action.AnimSpeed = result.AnimSpeed;
        action.ClipAnimSpeedMode = ActionAnimSpeedMode.Free;
    }

    public static void SyncAnimSpeedFromAuthority(ActionDataSO action)
    {
        if (action == null)
        {
            return;
        }

        action.AnimSpeed = ComputeAnimSpeed(action);
    }
#endif
}

using UnityEngine;

/// <summary>
/// Action Clip 速率单一入口（171.7 → 226）：
/// Free / AutoFitDuration 提供基准 S；MotionProfile SpeedOverTime 在积分合法时始终可叠加。
/// </summary>
public static class ActionAnimSpeedAuthority
{
    const float MinSpeed = 0.01f;
    const float AutoFitMin = 0.05f;
    const float AutoFitMax = 4f;
    const int IntegralSamples = 64;
    const int ClipDoneSearchIterations = 24;

    static int s_lastRejectProfileId;
    static float s_lastRejectLogTime = -999f;

    /// <summary>Action 层 Clip 倍率（不含 MotionProfile 局部节奏）。</summary>
    public static float ResolveClipAnimSpeed(ActionDataSO action, float motionT = 0f)
    {
        _ = motionT;
        if (action == null)
        {
            return 1f;
        }

        return action.ClipAnimSpeedMode switch
        {
            ActionAnimSpeedMode.AutoFitDuration => ResolveAutoFitClipAnimSpeed(action),
            ActionAnimSpeedMode.Free => Mathf.Max(MinSpeed, action.AnimSpeed),
            _ => 1f,
        };
    }

    /// <summary>AutoFit：Segment 在 Duration 内播完的基准倍率 S（曲线叠加前）。</summary>
    public static float ResolveAutoFitClipAnimSpeed(ActionDataSO action)
    {
        if (action == null)
        {
            return 1f;
        }

        var computed = ActionTimeAuthority.ComputeAnimSpeed(action);
        return Mathf.Clamp(computed, AutoFitMin, AutoFitMax);
    }

    /// <summary>
    /// 238.1 — 已确定运行期时长后求 Clip 窗口基准倍率。
    /// Clip.length 只作为倍率分子，不参与反推 Stop 物理时长，也不做 AutoFit 上限 Clamp。
    /// </summary>
    public static float ResolveClipAnimSpeedForDuration(
        ActionDataSO action,
        float effectiveDuration,
        float clipWindowWallSeconds = -1f)
    {
        if (action == null)
        {
            return 1f;
        }

        var duration = Mathf.Max(0.001f, effectiveDuration);
        var wall = clipWindowWallSeconds >= 0f
            ? clipWindowWallSeconds
            : ActionTimeAuthority.ResolveSegmentWallSeconds(action);
        return wall > 0.0001f ? Mathf.Max(MinSpeed, wall / duration) : 1f;
    }

    /// <summary>
    /// MotionProfile SpeedOverTime 因子。
    /// 【226】Free/AutoFit 均可叠加；Curve 且 ∫≠1 时策略1拒绝曲线（返回 1）并打 OPEN Log。
    /// </summary>
    public static float ResolveProfileAnimSpeedFactor(
        ActionDataSO action,
        MotionProfileSO profile,
        float motionT)
    {
        _ = action;
        if (profile == null || profile.AnimSpeedMode != AnimSpeedMode.Curve)
        {
            return 1f;
        }

        if (!profile.IsAnimSpeedCurveIntegralValid())
        {
            LogRejectInvalidIntegral(profile);
            return 1f;
        }

        return profile.SampleAnimSpeed(motionT);
    }

    /// <summary>Clip × Profile 合成倍率（MotionExecutor / 预览推演）。</summary>
    public static float ResolveCombinedAnimSpeed(ActionDataSO action, float motionT)
    {
        var clipSpeed = ResolveClipAnimSpeed(action, motionT);
        var profileFactor = ResolveProfileAnimSpeedFactor(action, action?.MotionProfile, motionT);
        return clipSpeed * Mathf.Max(0f, profileFactor);
    }

    /// <summary>
    /// 编辑器预览：按积分推演 Clip 墙钟秒，Segment 内播完后定格（可见后摇）。
    /// played = S × Duration × ∫₀^{nt} f
    /// </summary>
    public static float ResolvePreviewClipSeconds(ActionDataSO action, float normalizedTime)
    {
        if (action?.MainClip == null)
        {
            return 0f;
        }

        var clip = action.MainClip;
        var t = Mathf.Clamp01(normalizedTime);
        var dur = action.ResolveLogicalDurationSeconds();
        var clipSpeed = ResolveClipAnimSpeed(action);
        var integral = IntegrateProfileFactor(action, t);
        var segWall = ActionTimeAuthority.ResolveSegmentWallSeconds(action);
        var rawPlayed = clipSpeed * dur * integral;
        var played = Mathf.Min(rawPlayed, segWall);

        if (segWall <= 0.0001f)
        {
            return 0f;
        }

        var segStart = ActionTimeAuthority.ResolveSegmentStart(action);
        var segEnd = ActionTimeAuthority.ResolveSegmentEnd(action);
        var segProgress = played / segWall;
        var clipNorm = Mathf.Lerp(segStart, segEnd, segProgress);
        return clipNorm * clip.length;
    }

    /// <summary>
    /// Action 时间轴上 Clip/Segment 播完点（0~1）。
    /// 求解最小 nt：S × Duration × ∫₀^{nt} f ≥ SegmentWall。
    /// </summary>
    public static float ResolveClipDoneNormalizedTime(ActionDataSO action)
    {
        if (action?.MainClip == null)
        {
            return 1f;
        }

        var dur = action.ResolveLogicalDurationSeconds();
        if (dur <= 0.001f)
        {
            return 1f;
        }

        var animSpeed = ResolveClipAnimSpeed(action);
        if (animSpeed <= 0.001f)
        {
            return 1f;
        }

        var segWall = ActionTimeAuthority.ResolveSegmentWallSeconds(action);
        if (segWall <= 0.0001f)
        {
            return 1f;
        }

        var targetIntegral = segWall / (animSpeed * dur);
        if (targetIntegral <= 0f)
        {
            return 0f;
        }

        if (targetIntegral >= IntegrateProfileFactor(action, 1f) - 1e-5f)
        {
            return 1f;
        }

        var lo = 0f;
        var hi = 1f;
        for (var i = 0; i < ClipDoneSearchIterations; i++)
        {
            var mid = (lo + hi) * 0.5f;
            if (IntegrateProfileFactor(action, mid) < targetIntegral)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        return Mathf.Clamp01(hi);
    }

    /// <summary>∫₀^{toNt} f；非法/Constant 时 f≡1 ⇒ 返回 toNt。</summary>
    public static float IntegrateProfileFactor(ActionDataSO action, float toNt)
    {
        var end = Mathf.Clamp01(toNt);
        var profile = action != null ? action.MotionProfile : null;
        if (profile == null
            || profile.AnimSpeedMode != AnimSpeedMode.Curve
            || !profile.IsAnimSpeedCurveIntegralValid())
        {
            return end;
        }

        return AnimSpeedIntegralMath.IntegrateCurveRange(profile.SpeedOverTime, end, IntegralSamples);
    }

    static void LogRejectInvalidIntegral(MotionProfileSO profile)
    {
        if (profile == null)
        {
            return;
        }

        var id = profile.GetInstanceID();
        var now = Time.unscaledTime;
        if (id == s_lastRejectProfileId && now - s_lastRejectLogTime < 1f)
        {
            return;
        }

        s_lastRejectProfileId = id;
        s_lastRejectLogTime = now;
        var integral = profile.EvaluateAnimSpeedIntegral();
        var epsilon = profile.ResolveAnimSpeedIntegralEpsilon();
        var prefix = profile.AnimSpeedAuthoringMode == AnimSpeedCurveAuthoringMode.FreeFrontAutoTail
            ? "[AnimSpeed228]"
            : "[AnimSpeed226]";
        var hint = profile.AnimSpeedAuthoringMode == AnimSpeedCurveAuthoringMode.FreeFrontAutoTail
            ? "Fix FreeFrontAutoTail knots / AutoTail Bake in MotionProfile Inspector."
            : "Fix ThreePointConserve or Freehand curve in MotionProfile Inspector.";
        Debug.LogWarning(
            $"{prefix} REJECT invalid integral profile={profile.name} I={integral:F4} " +
            $"ε={epsilon:F4} → factor=1 (Constant). {hint}");
    }
}

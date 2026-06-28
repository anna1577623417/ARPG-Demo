using UnityEngine;

/// <summary>
/// Action Clip 速率单一入口（171.7）：Free / AutoFitDuration + MotionProfile 曲线门控。
/// </summary>
public static class ActionAnimSpeedAuthority
{
    const float MinSpeed = 0.01f;
    const float AutoFitMin = 0.05f;
    const float AutoFitMax = 4f;

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

    /// <summary>AutoFit：Segment 在 Duration 内播完（与 ActionTimeAuthority 一致）。</summary>
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
    /// MotionProfile SpeedOverTime 因子；Action 非 Free 模式恒为 1（忽略曲线）。
    /// </summary>
    public static float ResolveProfileAnimSpeedFactor(
        ActionDataSO action,
        MotionProfileSO profile,
        float motionT)
    {
        if (action == null
            || action.ClipAnimSpeedMode != ActionAnimSpeedMode.Free
            || profile == null)
        {
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
    /// 编辑器预览：按 AnimSpeed 推演 Clip 墙钟秒，Segment 内播完后定格（可见后摇）。
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
        var effectiveSpeed = ResolveCombinedAnimSpeed(action, t);
        var segWall = ActionTimeAuthority.ResolveSegmentWallSeconds(action);
        var rawPlayed = t * dur * effectiveSpeed;
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

    /// <summary>Action 时间轴上 Clip/Segment 播完点（0~1）；之后为后摇 / 末帧定格。</summary>
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
        return Mathf.Clamp01(segWall / (dur * animSpeed));
    }
}

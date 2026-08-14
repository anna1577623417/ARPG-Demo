using UnityEngine;

/// <summary>
/// 234.6.2 — 松开瞬间分类。Tap 用 heldSeconds≤TapWindow，不再用 3 tick。
/// </summary>
public static class StopTierResolver
{
    public const int MicroTapMaxHeldTicks = 3;
    public const float DefaultTapWindowSeconds = 0.15f;
    public const float DefaultTapPresentationSeconds = 0.15f;
    public const float DefaultTapStopDistance = 0.1f;
    public const float StartAbortGaitRatio = 0.5f;

    public static float ResolveTapWindow(in InheritPhysicsSettings settings) =>
        settings.TapWindowSeconds > 0.0001f ? settings.TapWindowSeconds : DefaultTapWindowSeconds;

    public static float ResolveTapPresentation(in InheritPhysicsSettings settings) =>
        settings.TapPresentationSeconds > 0.0001f
            ? settings.TapPresentationSeconds
            : DefaultTapPresentationSeconds;

    public static float ResolveTapStopDistance(in InheritPhysicsSettings settings) =>
        settings.TapStopDistance > 0.0001f
            ? settings.TapStopDistance
            : DefaultTapStopDistance;

    public static float ResolveTapTailSeconds(in InheritPhysicsSettings settings)
    {
        if (settings.TapTailSeconds > 0.0001f)
        {
            return settings.TapTailSeconds;
        }

        return ResolveTapPresentation(in settings);
    }

    /// <summary>
    /// Author = 拖过点按尾段 Segment（start 或 seconds 任一 &gt; 0）。
    /// 两端字段都为 0 才是 Auto，这样既有资产 YAML=0 仍走最后 T_tap 秒。
    /// </summary>
    public static bool IsAuthorTapTail(in InheritPhysicsSettings settings) =>
        settings.TapTailSeconds > 0.0001f || settings.TapTailStartNormalized > 0.0001f;

    public static bool TryResolveAuthorTailStart(in InheritPhysicsSettings settings, out float startNormalized)
    {
        startNormalized = Mathf.Clamp01(settings.TapTailStartNormalized);
        return IsAuthorTapTail(in settings);
    }

    public static int ResolveTapChainMax(in InheritPhysicsSettings settings) =>
        Mathf.Max(0, settings.TapChainMax);

    /// <summary>
    /// Toggle 开，或旧资产 <c>TapChainMax&lt;=0</c>：无限连点。
    /// Toggle 关且 max≥1：采用最大发。关且 max=0 仍走本兼容（仅用户在 Inspector 关 Toggle 时写入 max=1）。
    /// </summary>
    public static bool IsTapChainUnlimited(in InheritPhysicsSettings settings) =>
        settings.TapChainUnlimited || settings.TapChainMax <= 0;

    public static bool ShouldPromoteForChainMax(in InheritPhysicsSettings settings, int nextIndex)
    {
        if (IsTapChainUnlimited(in settings))
        {
            return false;
        }

        return nextIndex >= ResolveTapChainMax(in settings);
    }

    public static bool IsTapTier(StopSessionTier tier) =>
        tier == StopSessionTier.MicroTap || tier == StopSessionTier.TapChain;

    public static StopSessionTier Resolve(in StopSessionSnapshot snapshot, float entrySpeed) =>
        Resolve(in snapshot, entrySpeed, DefaultTapWindowSeconds);

    public static StopSessionTier Resolve(
        in StopSessionSnapshot snapshot,
        float entrySpeed,
        float tapWindowSeconds)
    {
        var speed = snapshot.IsValid ? snapshot.PlanarSpeedAtRelease : entrySpeed;
        var gait = snapshot.IsValid ? snapshot.GaitTargetSpeed : 0f;
        var heldSeconds = snapshot.IsValid ? snapshot.HeldSeconds : 0f;
        var reachedLoop = snapshot.IsValid && snapshot.ReachedLoop;
        var window = Mathf.Max(0.0001f, tapWindowSeconds);

        if ((snapshot.IsValid && heldSeconds <= window) || speed < StopIntegrator.DefaultMicroStopSpeed)
        {
            return StopSessionTier.MicroTap;
        }

        if (!reachedLoop && gait > 0.01f && speed < StartAbortGaitRatio * gait)
        {
            return StopSessionTier.StartAbort;
        }

        if (reachedLoop || (gait > 0.01f && speed >= StartAbortGaitRatio * gait))
        {
            return StopSessionTier.LoopStop;
        }

        return StopSessionTier.StartAbort;
    }
}

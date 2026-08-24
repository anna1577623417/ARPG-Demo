using UnityEngine;

/// <summary>
/// 182.1 / 234.6 — Stop Authoring 运行时：三策略 Build；InheritPhysics 走恒定减速度积分。
/// 动画层不受本类控制；AnimSpeedCurve（MotionProfile.SpeedOverTime）仍由 MotionExecutor 叠加。
/// </summary>
public static class StopMotionRuntime
{
    static int s_lastSyncRejectActionId;
    static StopDurationAuthority s_lastSyncRejectDurationAuthority;
    static StopAnimSpeedAuthority s_lastSyncRejectAnimSpeedAuthority;
    static bool s_lastSyncRejectHasClipWindow;

    /// <summary>
    /// 234.6.3 — Clip 尾时钟。短租约默认从尾段起播且倍率 1；
    /// authorSpecified 时用 Segment 拖条写入的 Clip nt（可为 0，表示从 Clip 头起播）。
    /// Action 寿命仍从 0 走满 T_lease，不得把 startNt 乘进 elapsed。
    /// </summary>
    public static void ResolvePresentationClock(
        float leaseSeconds,
        float segmentWallSeconds,
        out float startNormalized,
        out float animSpeed,
        float authorStartNormalized = 0f,
        bool authorSpecified = false,
        bool fitWholeWindow = false)
    {
        var lease = Mathf.Max(0.001f, leaseSeconds);
        var wall = Mathf.Max(0f, segmentWallSeconds);
        if (fitWholeWindow)
        {
            startNormalized = authorSpecified ? Mathf.Clamp01(authorStartNormalized) : 0f;
            animSpeed = wall > 0.0001f ? wall / lease : 1f;
            return;
        }

        if (authorSpecified)
        {
            startNormalized = Mathf.Clamp01(authorStartNormalized);
            animSpeed = 1f;
            return;
        }

        if (wall > 0.0001f && lease + 0.001f < wall)
        {
            startNormalized = Mathf.Clamp01(1f - lease / wall);
            animSpeed = 1f;
            return;
        }

        startNormalized = 0f;
        animSpeed = wall > 0.0001f ? wall / lease : 1f;
    }

    /// <summary>Stop 激活时的 Playable 首帧倍率（含 MotionProfile 曲线门控）。</summary>
    public static float ResolvePresentationAnimSpeed(
        ActionDataSO action,
        in StopRuntimeContext ctx,
        float motionNormalizedTime)
    {
        if (!ctx.IsActive || action == null)
        {
            return action != null ? action.ResolveEffectiveAnimSpeed() : 1f;
        }

        var profileFactor = action.MotionProfile != null
            ? action.MotionProfile.SampleAnimSpeed(action, motionNormalizedTime)
            : 1f;
        return ctx.BaseAnimSpeed * Mathf.Max(0f, profileFactor);
    }

    public static StopRuntimeContext Build(
        ActionDataSO action,
        MotionProfileSO profile,
        float entrySpeed,
        float referenceGaitSpeed = -1f,
        Vector3 stopDirection = default,
        StopSessionTier sessionTier = StopSessionTier.None,
        int chainIndex = 0,
        bool chained = false)
    {
        if (action == null || !action.EnableStopFeature)
        {
            return StopRuntimeContext.Disabled;
        }

        var mfReady = profile != null && profile.EnableStopAuthoring;

        switch (action.StopStrategy)
        {
            case StopStrategy.Snap:
                return new StopRuntimeContext(
                    isActive: true,
                    strategy: StopStrategy.Snap,
                    disableStopMotion: true,
                    useAuthorFixed: false,
                    useRuntimeDuration: false,
                    runtimeDuration: action.ResolveLogicalDurationSeconds(),
                    runtimeDistance: 0f,
                    baseAnimSpeed: action.ResolveEffectiveAnimSpeed(),
                    entrySpeed: entrySpeed,
                    applyMask: Vector3.zero);

            case StopStrategy.InheritPhysics:
                if (!mfReady)
                {
                    return StopRuntimeContext.Disabled;
                }

                return BuildInheritPhysicsIntegrated(
                    action,
                    entrySpeed,
                    referenceGaitSpeed,
                    stopDirection,
                    sessionTier,
                    chainIndex,
                    chained);

            case StopStrategy.MotionProfile:
                if (!mfReady || !profile.UsesAxisCurves)
                {
                    return StopRuntimeContext.Disabled;
                }

                return new StopRuntimeContext(
                    isActive: true,
                    strategy: StopStrategy.MotionProfile,
                    disableStopMotion: false,
                    useAuthorFixed: true,
                    useRuntimeDuration: false,
                    runtimeDuration: action.ResolveLogicalDurationSeconds(),
                    runtimeDistance: 0f,
                    baseAnimSpeed: action.ResolveEffectiveAnimSpeed(),
                    entrySpeed: entrySpeed,
                    applyMask: Vector3.one);

            default:
                return StopRuntimeContext.Disabled;
        }
    }

    static StopRuntimeContext BuildInheritPhysicsIntegrated(
        ActionDataSO action,
        float entrySpeed,
        float referenceGaitSpeed,
        Vector3 stopDirection,
        StopSessionTier sessionTier,
        int chainIndex,
        bool chained)
    {
        var s = action.InheritPhysics;
        var presentation = action.StopPresentation;
        var derivedFromLegacy = false;
        var dRef = s.FullSpeedStopDistance;
        var vRef = referenceGaitSpeed > 0.0001f ? referenceGaitSpeed : s.MaxSpeed;
        if (s.ContinuousTuningMode == ContinuousStopTuningMode.FullSpeedDuration
            && s.FullSpeedStopDuration > 0.0001f
            && vRef > 0.0001f)
        {
            dRef = 0.5f * vRef * s.FullSpeedStopDuration;
        }

        if (dRef <= 0.0001f)
        {
            dRef = s.MaxDistance;
            derivedFromLegacy = dRef > 0.0001f;
        }

        if (vRef <= 0.0001f)
        {
            return StopRuntimeContext.Disabled;
        }

        if (chainIndex > 0)
        {
            sessionTier = StopSessionTier.TapChain;
            chained = true;
        }

        var vEntry = Mathf.Max(0f, entrySpeed);
        var vUsed = Mathf.Min(vEntry, vRef);
        var isTap = StopTierResolver.IsTapTier(sessionTier);
        var shortLease = isTap || sessionTier == StopSessionTier.StartAbort;
        float a;
        float physicsDistance;
        float physicsDuration;
        var physicsDurationCapped = false;
        float remainingSpeed;
        var segmentWall = ActionTimeAuthority.ResolveSegmentWallSeconds(action);
        var logical = action.ResolveLogicalDurationSeconds();
        var tailSec = StopTierResolver.ResolveTapTailSeconds(s);
        var authorTail = StopTierResolver.TryResolveAuthorTailStart(s, out var authorStartNt);

        if (isTap)
        {
            var dTap = StopTierResolver.ResolveTapStopDistance(s);
            var tStep = Mathf.Max(0.001f, tailSec);
            if (!StopIntegrator.TryDeriveTapCreep(dTap, tStep, out remainingSpeed, out a))
            {
                return StopRuntimeContext.Disabled;
            }

            physicsDistance = dTap;
            physicsDuration = tStep;
        }
        else
        {
            if (!StopIntegrator.TryDeriveDeceleration(vRef, dRef, out a))
            {
                return StopRuntimeContext.Disabled;
            }

            var tUncap = StopIntegrator.PredictDuration(vUsed, a);
            var tMax = s.MaxBrakeSeconds > 0.0001f
                ? s.MaxBrakeSeconds
                : (2f * dRef / vRef);
            if (tUncap > tMax && tMax > 0.0001f && vUsed > StopIntegrator.DefaultSpeedEpsilon)
            {
                physicsDurationCapped = true;
                physicsDuration = tMax;
                a = vUsed / tMax;
                physicsDistance = 0.5f * vUsed * tMax;
            }
            else
            {
                physicsDuration = tUncap;
                physicsDistance = StopIntegrator.PredictDistance(vUsed, a);
            }

            remainingSpeed = vUsed;
        }

        var legacyDuration = shortLease
            ? Mathf.Max(0.001f, physicsDuration, tailSec)
            : Mathf.Max(0.001f, physicsDuration, segmentWall, logical);
        var durationAuthority = presentation.DurationAuthority;
        var animSpeedAuthority = presentation.AnimSpeedAuthority;
        var dur = durationAuthority switch
        {
            StopDurationAuthority.PhysicsStop => Mathf.Max(0.001f, physicsDuration),
            StopDurationAuthority.ActionDefault => Mathf.Max(0.001f, logical),
            _ => legacyDuration,
        };

        var clockAuthorNt = shortLease && authorTail ? authorStartNt : 0f;
        var authorClock = shortLease && authorTail;
        var clockWall = authorClock ? tailSec : segmentWall;
        float startNt;
        float animSpeed;
        var useLegacyPresentationClock = durationAuthority == StopDurationAuthority.LegacyLease
            && animSpeedAuthority == StopAnimSpeedAuthority.InheritAction;
        if (useLegacyPresentationClock)
        {
            ResolvePresentationClock(
                dur,
                segmentWall,
                out startNt,
                out animSpeed,
                clockAuthorNt,
                authorClock);
        }
        else
        {
            startNt = authorClock ? clockAuthorNt : 0f;
            animSpeed = animSpeedAuthority switch
            {
                StopAnimSpeedAuthority.AutoFitEffectiveDuration =>
                    ActionAnimSpeedAuthority.ResolveClipAnimSpeedForDuration(action, dur, clockWall),
                StopAnimSpeedAuthority.FixedOverride => presentation.ResolveFixedAnimSpeed(),
                _ when action.ClipAnimSpeedMode == ActionAnimSpeedMode.AutoFitDuration =>
                    ActionAnimSpeedAuthority.ResolveClipAnimSpeedForDuration(action, dur, clockWall),
                _ => action.ResolveEffectiveAnimSpeed(),
            };

            if (animSpeedAuthority == StopAnimSpeedAuthority.AutoFitEffectiveDuration)
            {
                // AutoFit must play the selected window rather than silently trim to its tail.
                ResolvePresentationClock(
                    dur,
                    clockWall,
                    out startNt,
                    out _,
                    clockAuthorNt,
                    authorClock,
                    fitWholeWindow: true);
                animSpeed = ActionAnimSpeedAuthority.ResolveClipAnimSpeedForDuration(action, dur, clockWall);
            }
        }

        var hasClipWindow = action.MainClip != null && clockWall > 0.0001f;
        var actualClipDuration = hasClipWindow && animSpeed > 0.0001f
            ? clockWall / animSpeed
            : dur;
        var syncDelta = actualClipDuration - dur;
        var syncResult = StopSyncResult.NotRequested;
        var strictEligible = durationAuthority == StopDurationAuthority.PhysicsStop
            && hasClipWindow
            && (animSpeedAuthority == StopAnimSpeedAuthority.AutoFitEffectiveDuration
                || animSpeedAuthority == StopAnimSpeedAuthority.FixedOverride);
        if (presentation.RequireSynchronization)
        {
            if (strictEligible && Mathf.Abs(syncDelta) <= 0.005f)
            {
                syncResult = StopSyncResult.Synchronized;
            }
            else
            {
                var requestedSyncDelta = syncDelta;
                syncResult = StopSyncResult.Rejected;
                animSpeed = ActionAnimSpeedAuthority.ResolveClipAnimSpeedForDuration(action, dur, clockWall);
                syncDelta = (hasClipWindow && animSpeed > 0.0001f
                    ? clockWall / animSpeed
                    : dur) - dur;
                LogSyncRejected(
                    action,
                    durationAuthority,
                    animSpeedAuthority,
                    requestedSyncDelta,
                    syncDelta,
                    hasClipWindow);
            }
        }

        var mask = new Vector3(s.AffectX ? 1f : 0f, s.AffectY ? 1f : 0f, s.AffectZ ? 1f : 0f);
        var dir = stopDirection.sqrMagnitude > 0.0001f ? stopDirection : Vector3.forward;
        return new StopRuntimeContext(
            isActive: true,
            strategy: StopStrategy.InheritPhysics,
            disableStopMotion: false,
            useAuthorFixed: false,
            useRuntimeDuration: true,
            runtimeDuration: dur,
            physicsDuration: physicsDuration,
            effectiveActionDuration: dur,
            clipWindowWallSeconds: clockWall,
            runtimeDistance: physicsDistance,
            baseAnimSpeed: animSpeed,
            entrySpeed: vEntry,
            applyMask: mask,
            useIntegratedBrake: true,
            brakeDeceleration: a,
            referenceGaitSpeed: vRef,
            sessionTier: sessionTier,
            derivedFromLegacyMaxDistance: derivedFromLegacy,
            physicsComplete: remainingSpeed <= StopIntegrator.DefaultSpeedEpsilon,
            remainingSpeed: remainingSpeed,
            stopDirection: dir,
            presentationStartNormalized: startNt,
            chainIndex: chainIndex,
            chained: chained,
            authorTail: authorClock,
            durationAuthority: durationAuthority,
            animSpeedAuthority: animSpeedAuthority,
            syncResult: syncResult,
            syncDeltaSeconds: syncDelta,
            physicsDurationCapped: physicsDurationCapped);
    }

    static void LogSyncRejected(
        ActionDataSO action,
        StopDurationAuthority durationAuthority,
        StopAnimSpeedAuthority animSpeedAuthority,
        float requestedSyncDelta,
        float fallbackSyncDelta,
        bool hasClipWindow)
    {
        var actionId = action != null ? action.GetInstanceID() : 0;
        if (actionId == s_lastSyncRejectActionId
            && durationAuthority == s_lastSyncRejectDurationAuthority
            && animSpeedAuthority == s_lastSyncRejectAnimSpeedAuthority
            && hasClipWindow == s_lastSyncRejectHasClipWindow)
        {
            return;
        }

        s_lastSyncRejectActionId = actionId;
        s_lastSyncRejectDurationAuthority = durationAuthority;
        s_lastSyncRejectAnimSpeedAuthority = animSpeedAuthority;
        s_lastSyncRejectHasClipWindow = hasClipWindow;
        Debug.LogFormat(
            LogType.Warning,
            LogOption.NoStacktrace,
            action,
            "[StopSync238] REJECT action={0} durationAuthority={1} animSpeedAuthority={2} requestedDelta={3:F4} fallbackDelta={4:F4} hasClipWindow={5} fallback=AutoFit",
            action != null ? action.name : "null",
            durationAuthority,
            animSpeedAuthority,
            requestedSyncDelta,
            fallbackSyncDelta,
            hasClipWindow);
    }

    /// <summary>InheritPhysics：曲线归一化节奏 × runtimeDistance。</summary>
    public static float ResolveInheritPhysicsRhythmSpan(
        MotionProfileSO profile,
        float startNt,
        float endNt,
        Vector3 applyMask)
    {
        if (profile == null || !profile.UsesAxisCurves)
        {
            return Mathf.Clamp01(endNt - startNt);
        }

        var a = SampleRhythmUnitPosition(profile.AxisCurves, Mathf.Clamp01(startNt));
        var b = SampleRhythmUnitPosition(profile.AxisCurves, Mathf.Clamp01(endNt));
        var delta = Vector3.Scale(b - a, applyMask);
        if (applyMask.z > 0.5f)
        {
            return Mathf.Clamp01(delta.z);
        }

        if (applyMask.x > 0.5f)
        {
            return Mathf.Clamp01(Mathf.Abs(delta.x));
        }

        if (applyMask.y > 0.5f)
        {
            return Mathf.Clamp01(Mathf.Abs(delta.y));
        }

        return Mathf.Clamp01(endNt - startNt);
    }

    /// <summary>182.6 — Stop 探针期望（wall 时长 + 有效位移）。198.x：Tail Segment 退役后简化为整段。</summary>
    public static void ResolveExitExpectations(
        in StopRuntimeContext ctx,
        ActionDataSO action,
        out float expectedWallDuration,
        out float expectedDistance)
    {
        expectedWallDuration = ctx.RuntimeDuration;
        expectedDistance = ctx.RuntimeDistance;
    }

    /// <summary>InheritPhysics：曲线归一化节奏 × runtimeDistance。</summary>
    public static Vector3 SampleInheritPhysicsLocalPosition(
        in StopRuntimeContext ctx,
        MotionAxisCurves curves,
        float normalizedTime,
        float motionScale = 1f)
    {
        if (!ctx.IsActive || ctx.DisableStopMotion || curves.HasAnyCurve == false)
        {
            return Vector3.zero;
        }

        var rhythm = SampleRhythmUnitPosition(curves, normalizedTime);
        return Vector3.Scale(rhythm * ctx.RuntimeDistance * motionScale, ctx.ApplyMask);
    }

    public static Vector3 SampleInheritPhysicsLocalDelta(
        in StopRuntimeContext ctx,
        MotionAxisCurves curves,
        float prevT,
        float currT,
        float motionScale = 1f)
    {
        var a = SampleInheritPhysicsLocalPosition(in ctx, curves, prevT, motionScale);
        var b = SampleInheritPhysicsLocalPosition(in ctx, curves, currT, motionScale);
        return b - a;
    }

    static Vector3 SampleRhythmUnitPosition(MotionAxisCurves curves, float t)
    {
        t = Mathf.Clamp01(t);
        return new Vector3(
            SampleRhythmAxis(curves.XCurve, t),
            SampleRhythmAxis(curves.YCurve, t),
            SampleRhythmAxis(curves.ZCurve, t));
    }

    /// <summary>将任意作者曲线归一化为 [0,1] 节奏，兼容旧 ZXY 米数曲线。</summary>
    static float SampleRhythmAxis(AnimationCurve curve, float t)
    {
        if (curve == null || curve.length == 0)
        {
            return 0f;
        }

        var start = curve.Evaluate(0f);
        var end = curve.Evaluate(1f);
        var value = curve.Evaluate(t);
        var span = end - start;
        if (Mathf.Abs(span) < 0.0001f)
        {
            return 0f;
        }

        return Mathf.Clamp01((value - start) / span);
    }
}

using UnityEngine;

/// <summary>
/// 182.1 — Stop Authoring 运行时：三策略 Build + InheritPhysics 位移采样。
/// 动画层不受本类控制；AnimSpeedCurve（MotionProfile.SpeedOverTime）仍由 MotionExecutor 叠加。
/// </summary>
public static class StopMotionRuntime
{
    /// <summary>
    /// InheritPhysics 动态 AnimSpeed：segment 墙钟 ÷ runtimeDuration，使 Clip 与 Action 时钟同步结束。
    /// 无 Clip 时回退 ReferenceDuration / runtimeDuration。
    /// </summary>
    public static float ResolveInheritPhysicsBaseAnimSpeed(ActionDataSO action, float runtimeDuration)
    {
        var dur = Mathf.Max(0.001f, runtimeDuration);
        var segWall = ActionTimeAuthority.ResolveSegmentWallSeconds(action);
        if (segWall > 0.0001f)
        {
            return segWall / dur;
        }

        var reference = action != null ? action.ReferenceDuration : 0.25f;
        return Mathf.Max(0.01f, reference / dur);
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

    public static StopRuntimeContext Build(ActionDataSO action, MotionProfileSO profile, float entrySpeed)
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

                var s = action.InheritPhysics;
                var maxSpeed = Mathf.Max(s.MinSpeed + 0.01f, s.MaxSpeed);
                var t = Mathf.InverseLerp(s.MinSpeed, maxSpeed, entrySpeed);
                var dist = Mathf.Lerp(s.MinDistance, s.MaxDistance, t);
                var dur = Mathf.Lerp(s.MinDuration, s.MaxDuration, t);
                var mask = new Vector3(s.AffectX ? 1f : 0f, s.AffectY ? 1f : 0f, s.AffectZ ? 1f : 0f);
                return new StopRuntimeContext(
                    isActive: true,
                    strategy: StopStrategy.InheritPhysics,
                    disableStopMotion: false,
                    useAuthorFixed: false,
                    useRuntimeDuration: true,
                    runtimeDuration: dur,
                    runtimeDistance: dist,
                    baseAnimSpeed: ResolveInheritPhysicsBaseAnimSpeed(action, dur),
                    entrySpeed: entrySpeed,
                    applyMask: mask);

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

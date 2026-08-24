using NUnit.Framework;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>238.1 — Stop 物理时钟、End AnimSpeed 与 Clip 窗口合同的编辑器回归测试。</summary>
public sealed class StopSync238Tests
{
    [Test]
    public void LegacyPresentationClock_UsesTailWhenLeaseIsShorterThanSegment()
    {
        StopMotionRuntime.ResolvePresentationClock(
            leaseSeconds: 0.3f,
            segmentWallSeconds: 1f,
            out var startNormalized,
            out var animSpeed);

        Assert.AreEqual(0.7f, startNormalized, 0.001f);
        Assert.AreEqual(1f, animSpeed, 0.001f);
    }

    [Test]
    public void LegacyPresentationClock_AuthorTailKeepsExplicitStartAndSpeed()
    {
        StopMotionRuntime.ResolvePresentationClock(
            leaseSeconds: 0.3f,
            segmentWallSeconds: 1f,
            out var startNormalized,
            out var animSpeed,
            authorStartNormalized: 0.65f,
            authorSpecified: true);

        Assert.AreEqual(0.65f, startNormalized, 0.001f);
        Assert.AreEqual(1f, animSpeed, 0.001f);
    }

    [Test]
    public void StopIntegrator_ReferenceDistanceAndSpeedProduceExpectedPhysicalClock()
    {
        Assert.IsTrue(StopIntegrator.TryDeriveDeceleration(4f, 2f, out var deceleration));

        Assert.AreEqual(4f, deceleration, 0.001f);
        Assert.AreEqual(2f, StopIntegrator.PredictDistance(4f, deceleration), 0.001f);
        Assert.AreEqual(1f, StopIntegrator.PredictDuration(4f, deceleration), 0.001f);
    }

    [Test]
    public void AutoFitDuration_UsesClipWindowAndSuppliedDuration()
    {
        var action = CreateAction(1f);
        action.Duration = 0.5f;

        Assert.AreEqual(2f, ActionAnimSpeedAuthority.ResolveClipAnimSpeedForDuration(action, 0.5f), 0.001f);

        Destroy(action, null);
    }

    [Test]
    public void FullSpeedDuration_DerivesPhysicsStopFromReferenceSpeed()
    {
        var action = CreateAction(1f);
        var profile = ScriptableObject.CreateInstance<MotionProfileSO>();
        profile.EnableStopAuthoring = true;
        action.MotionProfile = profile;
        action.InheritPhysics = InheritPhysicsSettings.Default;
        action.InheritPhysics.ContinuousTuningMode = ContinuousStopTuningMode.FullSpeedDuration;
        action.InheritPhysics.FullSpeedStopDuration = 1f;
        action.StopPresentation = StopPresentationSettings.Default;
        action.StopPresentation.DurationAuthority = StopDurationAuthority.PhysicsStop;
        action.StopPresentation.AnimSpeedAuthority = StopAnimSpeedAuthority.AutoFitEffectiveDuration;
        action.StopPresentation.RequireSynchronization = true;

        var ctx = StopMotionRuntime.Build(action, profile, 4f, 4f);

        Assert.IsTrue(ctx.IsActive);
        Assert.AreEqual(1f, ctx.PhysicsDuration, 0.001f);
        Assert.AreEqual(ctx.PhysicsDuration, ctx.EffectiveActionDuration, 0.001f);
        Assert.AreEqual(StopSyncResult.Synchronized, ctx.SyncResult);
        Assert.AreEqual(0f, ctx.SyncDeltaSeconds, 0.001f);
        Assert.AreEqual(1f, ctx.BaseAnimSpeed, 0.001f);

        Destroy(action, profile);
    }

    [Test]
    public void FixedOverride_UsesExplicitEndAnimSpeedWhenSynchronizationIsOptional()
    {
        var action = CreateAction(2f);
        var profile = ScriptableObject.CreateInstance<MotionProfileSO>();
        profile.EnableStopAuthoring = true;
        action.MotionProfile = profile;
        action.InheritPhysics = InheritPhysicsSettings.Default;
        action.InheritPhysics.FullSpeedStopDistance = 2f;
        action.StopPresentation = StopPresentationSettings.Default;
        action.StopPresentation.DurationAuthority = StopDurationAuthority.PhysicsStop;
        action.StopPresentation.AnimSpeedAuthority = StopAnimSpeedAuthority.FixedOverride;
        action.StopPresentation.FixedAnimSpeed = 1.75f;

        var ctx = StopMotionRuntime.Build(action, profile, 4f, 4f);

        Assert.IsTrue(ctx.IsActive);
        Assert.AreEqual(1.75f, ctx.BaseAnimSpeed, 0.001f);
        Assert.AreEqual(StopSyncResult.NotRequested, ctx.SyncResult);

        Destroy(action, profile);
    }

    [Test]
    public void FixedOverride_StrictMismatchRejectsAndFallsBackToAutoFit()
    {
        var action = CreateAction(2f);
        var profile = ScriptableObject.CreateInstance<MotionProfileSO>();
        profile.EnableStopAuthoring = true;
        action.MotionProfile = profile;
        action.InheritPhysics = InheritPhysicsSettings.Default;
        action.InheritPhysics.FullSpeedStopDistance = 2f;
        action.StopPresentation = StopPresentationSettings.Default;
        action.StopPresentation.DurationAuthority = StopDurationAuthority.PhysicsStop;
        action.StopPresentation.AnimSpeedAuthority = StopAnimSpeedAuthority.FixedOverride;
        action.StopPresentation.FixedAnimSpeed = 1f;
        action.StopPresentation.RequireSynchronization = true;
        LogAssert.Expect(LogType.Warning, new Regex(@"\[StopSync238\] REJECT.*"));

        var ctx = StopMotionRuntime.Build(action, profile, 4f, 4f);

        Assert.AreEqual(StopSyncResult.Rejected, ctx.SyncResult);
        Assert.AreEqual(2f, ctx.BaseAnimSpeed, 0.001f);
        Assert.AreEqual(0f, ctx.SyncDeltaSeconds, 0.001f);

        Destroy(action, profile);
    }

    static ActionDataSO CreateAction(float clipSeconds)
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.EnableStopFeature = true;
        action.StopStrategy = StopStrategy.InheritPhysics;
        action.Duration = 0.4f;
        action.SegmentStart = 0f;
        action.SegmentEnd = 1f;
        action.MainClip = CreateClip(clipSeconds);
        return action;
    }

    static AnimationClip CreateClip(float seconds)
    {
        var clip = new AnimationClip();
        clip.SetCurve(
            string.Empty,
            typeof(Transform),
            "localPosition.x",
            AnimationCurve.Linear(0f, 0f, Mathf.Max(0.001f, seconds), 0f));
        return clip;
    }

    static void Destroy(ActionDataSO action, MotionProfileSO profile)
    {
        var clip = action != null ? action.MainClip : null;
        Object.DestroyImmediate(action);
        Object.DestroyImmediate(profile);
        Object.DestroyImmediate(clip);
    }
}

using NUnit.Framework;
using UnityEngine;

public sealed class ActionTimeAuthorityTests
{
    [Test]
    public void MeasurePrincipalAxis_ZScale()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        var profile = ScriptableObject.CreateInstance<MotionProfileSO>();
        action.MotionProfile = profile;
        action.PrincipalAxis = MotionPrincipalAxis.Z;
        profile.AxisCurves.ZCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        profile.AxisCurves.ZScale = 2f;

        Assert.AreEqual(2f, profile.MeasurePrincipalAxisDisplacementMeters(MotionPrincipalAxis.Z), 0.001f);
        Assert.AreEqual(2f, ActionTimeAuthority.MeasurePrincipalAxisDisplacementMeters(action), 0.001f);

        Object.DestroyImmediate(action);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void MotionDisplacementAtActionEnd_IgnoresSegment()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        var profile = ScriptableObject.CreateInstance<MotionProfileSO>();
        action.MotionProfile = profile;
        action.PrincipalAxis = MotionPrincipalAxis.Z;
        action.SegmentEnd = 0.8f;
        profile.AxisCurves.ZCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        profile.AxisCurves.ZScale = 5f;

        Assert.AreEqual(5f, ActionTimeAuthority.MeasurePrincipalAxisDisplacementAtActionEnd(action), 0.001f);

        Object.DestroyImmediate(action);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void ClipProgress_ScalesWithSegmentEnd()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.SegmentEnd = 0.8f;
        Assert.AreEqual(0.8f, ActionTimeAuthority.MapActionTimeToClipNormalized(1f, action), 0.001f);
        Assert.AreEqual(1f, ActionTimeAuthority.MapNormalizedTimeToMotionTime(1f), 0.001f);
        Object.DestroyImmediate(action);
    }

    [Test]
    public void ClipProgress_ScalesWithBidirectionalSegment()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.SegmentStart = 0.3f;
        action.SegmentEnd = 0.6f;
        Assert.AreEqual(0.3f, ActionTimeAuthority.MapActionTimeToClipNormalized(0f, action), 0.001f);
        Assert.AreEqual(0.45f, ActionTimeAuthority.MapActionTimeToClipNormalized(0.5f, action), 0.001f);
        Assert.AreEqual(0.6f, ActionTimeAuthority.MapActionTimeToClipNormalized(1f, action), 0.001f);
        Object.DestroyImmediate(action);
    }

    [Test]
    public void ComputeAnimSpeed_UsesSegmentLengthAndDuration()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.Duration = 0.5f;
        action.SegmentEnd = 0.8f;
        action.MainClip = new AnimationClip { name = "Test" };
        var expected = action.MainClip.length * 0.8f / 0.5f;
        Assert.AreEqual(expected, ActionTimeAuthority.ComputeAnimSpeed(action), 0.001f);

        Object.DestroyImmediate(action.MainClip);
        Object.DestroyImmediate(action);
    }

    [Test]
    public void InferSegmentEnd_FromAnimSpeed()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.Duration = 0.5f;
        action.AnimSpeed = 2f;
        action.MainClip = new AnimationClip { name = "Test" };
        var expected = 2f * 0.5f / action.MainClip.length;
        Assert.AreEqual(
            Mathf.Clamp(expected, 0.001f, 1f),
            ActionTimeAuthority.InferSegmentEndFromAnimSpeed(action),
            0.001f);

        Object.DestroyImmediate(action.MainClip);
        Object.DestroyImmediate(action);
    }

    [Test]
    public void ComputeMotionRetiming_DurationAndAnimSpeed_FromReferenceSpeed()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        var profile = ScriptableObject.CreateInstance<MotionProfileSO>();
        action.MotionProfile = profile;
        action.PrincipalAxis = MotionPrincipalAxis.Z;
        profile.AxisCurves.ZCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        profile.AxisCurves.ZScale = 2.7f;
        action.MainClip = new AnimationClip { name = "Test" };
        action.SegmentStart = 0f;
        action.SegmentEnd = 1f;

        var result = ActionTimeAuthority.ComputeMotionRetiming(action, 6f, 0.85f, 1.15f);
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(2.7f, result.MainDistanceMeters, 0.001f);
        Assert.AreEqual(0.45f, result.Duration, 0.001f);
        Assert.AreEqual(
            action.MainClip.length / 0.45f,
            result.AnimSpeed,
            0.001f);

        Object.DestroyImmediate(action.MainClip);
        Object.DestroyImmediate(action);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void ComputeMotionRetiming_ClampAnimSpeed_RecalculatesDuration()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        var profile = ScriptableObject.CreateInstance<MotionProfileSO>();
        action.MotionProfile = profile;
        action.PrincipalAxis = MotionPrincipalAxis.Z;
        profile.AxisCurves.ZCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        profile.AxisCurves.ZScale = 1f;
        action.MainClip = new AnimationClip { name = "Test" };
        action.SegmentEnd = 1f;

        var result = ActionTimeAuthority.ComputeMotionRetiming(action, 100f, 0.85f, 1.15f);
        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.AnimSpeedWasClamped);
        Assert.AreEqual(1.15f, result.AnimSpeed, 0.001f);
        Assert.AreEqual(action.MainClip.length / 1.15f, result.Duration, 0.001f);

        Object.DestroyImmediate(action.MainClip);
        Object.DestroyImmediate(action);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void ApplyMotionRetiming_WritesDurationAndDisablesAutoSync()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        var profile = ScriptableObject.CreateInstance<MotionProfileSO>();
        action.MotionProfile = profile;
        action.PrincipalAxis = MotionPrincipalAxis.Z;
        profile.AxisCurves.ZCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        profile.AxisCurves.ZScale = 2f;
        action.MainClip = new AnimationClip { name = "Test" };
        action.ClipAnimSpeedMode = ActionAnimSpeedMode.AutoFitDuration;

        var result = ActionTimeAuthority.ComputeMotionRetiming(action, 5f);
        ActionTimeAuthority.ApplyMotionRetiming(action, result);

        Assert.AreEqual(0.4f, action.Duration, 0.001f);
        Assert.AreEqual(ActionAnimSpeedMode.Free, action.ClipAnimSpeedMode);
        Assert.AreEqual(5f, action.ReferenceMotionSpeed, 0.001f);

        Object.DestroyImmediate(action.MainClip);
        Object.DestroyImmediate(action);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void ResolveLogicDuration_AppliesDurationScale()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.Duration = 1f;
        action.DurationStatScaling = MotionScaleType.AttackSpeed;

        var stats = new FakeDurationStats(2f);
        Assert.AreEqual(0.5f, ActionTimeAuthority.ResolveLogicDurationSeconds(action, stats), 0.001f);

        Object.DestroyImmediate(action);
    }

    [Test]
    public void ResolveClipAnimSpeed_AutoFit_UsesSegmentAndDuration()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.ClipAnimSpeedMode = ActionAnimSpeedMode.AutoFitDuration;
        action.Duration = 1f;
        action.MainClip = new AnimationClip { name = "Test" };

        var expected = ActionTimeAuthority.ComputeAnimSpeed(action);
        Assert.AreEqual(expected, ActionAnimSpeedAuthority.ResolveClipAnimSpeed(action), 0.001f);

        Object.DestroyImmediate(action.MainClip);
        Object.DestroyImmediate(action);
    }

    [Test]
    public void ResolveClipAnimSpeed_Free_UsesStoredValue()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.ClipAnimSpeedMode = ActionAnimSpeedMode.Free;
        action.AnimSpeed = 1.25f;

        Assert.AreEqual(1.25f, ActionAnimSpeedAuthority.ResolveClipAnimSpeed(action), 0.001f);

        Object.DestroyImmediate(action);
    }

    [Test]
    public void ProfileAnimSpeedFactor_IgnoredWhenNotFree()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        var profile = ScriptableObject.CreateInstance<MotionProfileSO>();
        action.ClipAnimSpeedMode = ActionAnimSpeedMode.AutoFitDuration;
        profile.AnimSpeedMode = AnimSpeedMode.Curve;
        profile.SpeedOverTime = AnimationCurve.Linear(0f, 2f, 1f, 2f);

        Assert.AreEqual(1f, ActionAnimSpeedAuthority.ResolveProfileAnimSpeedFactor(action, profile, 0.5f), 0.001f);

        action.ClipAnimSpeedMode = ActionAnimSpeedMode.Free;
        Assert.AreEqual(2f, ActionAnimSpeedAuthority.ResolveProfileAnimSpeedFactor(action, profile, 0.5f), 0.001f);

        Object.DestroyImmediate(action);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void ResolvePreviewClipSeconds_HoldsAfterClipFinishesInFreeMode()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.ClipAnimSpeedMode = ActionAnimSpeedMode.Free;
        action.AnimSpeed = 2f;
        action.Duration = 1f;
        action.MainClip = new AnimationClip { name = "Test" };

        var beforeDone = ActionAnimSpeedAuthority.ResolvePreviewClipSeconds(action, 0.4f);
        var afterDone = ActionAnimSpeedAuthority.ResolvePreviewClipSeconds(action, 0.9f);
        if (action.MainClip.length > 0.001f)
        {
            Assert.GreaterOrEqual(beforeDone, 0f);
            Assert.AreEqual(afterDone, ActionAnimSpeedAuthority.ResolvePreviewClipSeconds(action, 1f), 0.001f);
        }

        Object.DestroyImmediate(action.MainClip);
        Object.DestroyImmediate(action);
    }

    [Test]
    public void ResolveClipDoneNormalizedTime_AutoFit_ReachesOne()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.ClipAnimSpeedMode = ActionAnimSpeedMode.AutoFitDuration;
        action.Duration = 1f;
        action.MainClip = new AnimationClip { name = "Test" };

        Assert.AreEqual(1f, ActionAnimSpeedAuthority.ResolveClipDoneNormalizedTime(action), 0.001f);

        Object.DestroyImmediate(action.MainClip);
        Object.DestroyImmediate(action);
    }

    [Test]
    public void ResolveClipDoneNormalizedTime_FreeMode_BeforeActionEnd()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.ClipAnimSpeedMode = ActionAnimSpeedMode.Free;
        action.AnimSpeed = 1f;
        action.Duration = 1f;
        action.MainClip = new AnimationClip { name = "Test" };
        action.MainClip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 0.43f, 0f));

        var done = ActionAnimSpeedAuthority.ResolveClipDoneNormalizedTime(action);
        if (action.MainClip.length > 0.001f)
        {
            Assert.Less(done, 1f);
            Assert.AreEqual(0.43f, done, 0.05f);
        }

        Object.DestroyImmediate(action.MainClip);
        Object.DestroyImmediate(action);
    }

    sealed class FakeDurationStats : IStatsProvider
    {
        readonly float _scale;

        public FakeDurationStats(float scale) => _scale = scale;

        public float GetMotionScale(MotionScaleType type) => _scale;
        public float GetDurationScale(MotionScaleType type) => _scale;
    }
}

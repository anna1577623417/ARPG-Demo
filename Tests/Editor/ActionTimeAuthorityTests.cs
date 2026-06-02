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
    public void MotionDisplacementAtActionEnd_IgnoresRatio()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        var profile = ScriptableObject.CreateInstance<MotionProfileSO>();
        action.MotionProfile = profile;
        action.PrincipalAxis = MotionPrincipalAxis.Z;
        action.AnimationEndRatio = 0.8f;
        profile.AxisCurves.ZCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        profile.AxisCurves.ZScale = 5f;

        Assert.AreEqual(5f, ActionTimeAuthority.MeasurePrincipalAxisDisplacementAtActionEnd(action), 0.001f);

        Object.DestroyImmediate(action);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void ClipProgress_ScalesWithRatio()
    {
        Assert.AreEqual(0.8f, ActionTimeAuthority.MapNormalizedTimeToClipProgress(1f, 0.8f), 0.001f);
        Assert.AreEqual(1f, ActionTimeAuthority.MapNormalizedTimeToMotionTime(1f), 0.001f);
    }

    [Test]
    public void ComputeAnimSpeed_UsesRatioAndDuration()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.Duration = 0.5f;
        action.AnimationEndRatio = 0.8f;
        action.MainClip = new AnimationClip { name = "Test" };
        var expected = action.MainClip.length * 0.8f / 0.5f;
        Assert.AreEqual(expected, ActionTimeAuthority.ComputeAnimSpeed(action), 0.001f);

        Object.DestroyImmediate(action.MainClip);
        Object.DestroyImmediate(action);
    }

    [Test]
    public void InferRatio_FromLegacyAnimSpeed()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.Duration = 0.5f;
        action.AnimSpeed = 2f;
        action.MainClip = new AnimationClip { name = "Test" };
        var expected = 2f * 0.5f / action.MainClip.length;
        Assert.AreEqual(
            Mathf.Clamp(expected, 0.05f, 2f),
            ActionTimeAuthority.InferAnimationEndRatioFromAnimSpeed(action),
            0.001f);

        Object.DestroyImmediate(action.MainClip);
        Object.DestroyImmediate(action);
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

    sealed class FakeDurationStats : IStatsProvider
    {
        readonly float _scale;

        public FakeDurationStats(float scale) => _scale = scale;

        public float GetMotionScale(MotionScaleType type) => _scale;
        public float GetDurationScale(MotionScaleType type) => _scale;
    }
}

using NUnit.Framework;
using UnityEngine;

public sealed class MotionTimeSyncTests
{
    [Test]
    public void None_KeepsLogicDurationAndUnitAnimMultiplier()
    {
        var sync = MotionTimeSync.Resolve(0.4f, 0.8f, MotionTimeSyncMode.None);
        Assert.AreEqual(0.4f, sync.MotionDurationSeconds, 0.001f);
        Assert.AreEqual(1f, sync.AnimSpeedMultiplier, 0.001f);
    }

    [Test]
    public void MatchAnimation_StretchesMotionDuration()
    {
        var sync = MotionTimeSync.Resolve(0.4f, 0.8f, MotionTimeSyncMode.MatchAnimation);
        Assert.AreEqual(0.8f, sync.MotionDurationSeconds, 0.001f);
        Assert.AreEqual(1f, sync.AnimSpeedMultiplier, 0.001f);
    }

    [Test]
    public void MatchMotion_ComputesAnimMultiplier()
    {
        var sync = MotionTimeSync.Resolve(0.4f, 0.8f, MotionTimeSyncMode.MatchMotion);
        Assert.AreEqual(0.4f, sync.MotionDurationSeconds, 0.001f);
        Assert.AreEqual(2f, sync.AnimSpeedMultiplier, 0.001f);
    }

    [Test]
    public void ResolveWithTimeSync_MatchAnimation_WithoutGroundTargeted_Stretches()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        var profile = ScriptableObject.CreateInstance<MotionProfileSO>();
        var clip = new AnimationClip();
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 0f));

        action.Duration = 0.4f;
        action.AnimSpeed = 1f;
        action.MainClip = clip;
        action.MotionProfile = profile;
        profile.YMotion = YMotionMode.Curve;
        profile.Gravity = GravityMode.SuspendGravity;
        profile.GroundConstraint = GroundConstraintMode.None;
        profile.SetYAxisV2Configured(true);
        profile.TimeSync = MotionTimeSyncMode.MatchAnimation;

        var sync = MotionDurationResolver.ResolveWithTimeSync(action);
        Assert.AreEqual(clip.length, sync.MotionDurationSeconds, 0.01f);

        Object.DestroyImmediate(action);
        Object.DestroyImmediate(profile);
        Object.DestroyImmediate(clip);
    }
}

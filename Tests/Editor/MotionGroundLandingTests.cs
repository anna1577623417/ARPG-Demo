using NUnit.Framework;
using UnityEngine;

public sealed class MotionGroundLandingTests
{
    [Test]
    public void SampleTargetWorldY_ReachesEndAtT1()
    {
        var curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        var y = MotionGroundLanding.SampleTargetWorldY(8f, 0f, curve, 1f);
        Assert.AreEqual(0f, y, 0.001f);
    }

    [Test]
    public void Composer_GroundTargeted_IgnoresGravity()
    {
        var config = new MotionYAxisConfig(
            YMotionMode.GroundTargeted,
            GravityMode.SuspendGravity,
            GroundConstraintMode.None);
        var motion = new MotionContribution { IsActive = true, YAxisConfig = config };
        var gravity = new GravityContribution { Vy = -9.8f, IsActive = true };
        var final = MotionComposer.ComposeWorldVelocity(in motion, in gravity, new Vector3(0f, -3f, 0f), in config);
        Assert.AreEqual(-3f, final.y, 0.001f);
    }
}

using NUnit.Framework;
using UnityEngine;

public sealed class MotionComposerEditModeTests
{
    [Test]
    public void Composer_UseGravity_UsesGravityVy()
    {
        var config = new MotionYAxisConfig(YMotionMode.None, GravityMode.UseGravity, GroundConstraintMode.None);
        var motion = new MotionContribution { IsActive = true, YAxisConfig = config };
        var gravity = new GravityContribution { Vy = -2f, IsActive = true };
        var final = MotionComposer.ComposeWorldVelocity(in motion, in gravity, Vector3.forward, in config);
        Assert.AreEqual(-2f, final.y, 0.001f);
        Assert.AreEqual(1f, final.z, 0.001f);
    }

    [Test]
    public void Composer_MotionControlled_IgnoresGravity()
    {
        var config = new MotionYAxisConfig(YMotionMode.Curve, GravityMode.SuspendGravity, GroundConstraintMode.None);
        var motion = new MotionContribution { IsActive = true, YAxisConfig = config };
        var gravity = new GravityContribution { Vy = -9.8f, IsActive = true };
        var final = MotionComposer.ComposeWorldVelocity(in motion, in gravity, new Vector3(0f, 2f, 0f), in config);
        Assert.AreEqual(2f, final.y, 0.001f);
    }

    [Test]
    public void Composer_AdditiveGravity_Adds()
    {
        var config = new MotionYAxisConfig(YMotionMode.Curve, GravityMode.AdditiveGravity, GroundConstraintMode.None);
        var motion = new MotionContribution { IsActive = true, YAxisConfig = config };
        var gravity = new GravityContribution { Vy = -0.1f, IsActive = true };
        var final = MotionComposer.ComposeWorldVelocity(in motion, in gravity, new Vector3(0f, 0.3f, 0f), in config);
        Assert.AreEqual(0.2f, final.y, 0.001f);
    }

    [Test]
    public void Motion_SampleDelta_PositionCurve()
    {
        var curves = new MotionAxisCurves
        {
            XCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, 1f),
                new Keyframe(1f, 0f)),
            XScale = 2f,
        };
        var d = curves.SampleLocalDelta(0.4f, 0.5f);
        Assert.Greater(d.x, 0f);
    }
}

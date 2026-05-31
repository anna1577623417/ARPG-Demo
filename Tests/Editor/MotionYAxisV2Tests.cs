using NUnit.Framework;
using UnityEngine;

public sealed class MotionYAxisV2Tests
{
    [Test]
    public void Legacy_UseGravity_MapsToCurveUseGravityClamp()
    {
#pragma warning disable 0618
        var config = MotionYAxisLegacyMapping.FromLegacy(YAxisPolicy.UseGravity);
#pragma warning restore 0618
        Assert.AreEqual(YMotionMode.Curve, config.YMotion);
        Assert.AreEqual(GravityMode.UseGravity, config.Gravity);
        Assert.AreEqual(GroundConstraintMode.ClampToGround, config.GroundConstraint);
    }

    [Test]
    public void Composer_UseGravityWithCurve_AddsMotionYAndGravity()
    {
        var config = new MotionYAxisConfig(YMotionMode.Curve, GravityMode.UseGravity, GroundConstraintMode.None);
        var motion = new MotionContribution { IsActive = true, YAxisConfig = config };
        var gravity = new GravityContribution { Vy = -0.2f, IsActive = true };
        var final = MotionComposer.ComposeWorldVelocity(in motion, in gravity, new Vector3(0f, 0.8f, 0f), in config);
        Assert.AreEqual(0.6f, final.y, 0.001f);
    }

    [Test]
    public void Roll_ClampToGround_ZerosNetUpwardWhenGrounded()
    {
        var config = new MotionYAxisConfig(
            YMotionMode.Curve,
            GravityMode.UseGravity,
            GroundConstraintMode.ClampToGround);
        var motion = new MotionContribution { IsActive = true, YAxisConfig = config };
        var gravity = new GravityContribution { Vy = -0.2f, IsActive = true };
        var final = MotionComposer.ComposeWorldVelocity(in motion, in gravity, new Vector3(0f, 0.8f, 0f), in config);
        MotionGroundConstraint.ApplyClamp(ref final, in config, isGrounded: true);
        Assert.AreEqual(0f, final.y, 0.001f);
    }

    [Test]
    public void Composer_None_UseGravity_UsesGravityOnly()
    {
        var config = new MotionYAxisConfig(YMotionMode.None, GravityMode.UseGravity, GroundConstraintMode.None);
        var motion = new MotionContribution { IsActive = true, YAxisConfig = config };
        var gravity = new GravityContribution { Vy = -2f, IsActive = true };
        var final = MotionComposer.ComposeWorldVelocity(in motion, in gravity, Vector3.forward, in config);
        Assert.AreEqual(-2f, final.y, 0.001f);
    }

    [Test]
    public void Composer_SuspendGravity_IgnoresGravity()
    {
        var config = new MotionYAxisConfig(YMotionMode.Curve, GravityMode.SuspendGravity, GroundConstraintMode.None);
        var motion = new MotionContribution { IsActive = true, YAxisConfig = config };
        var gravity = new GravityContribution { Vy = -9.8f, IsActive = true };
        var final = MotionComposer.ComposeWorldVelocity(in motion, in gravity, new Vector3(0f, 2f, 0f), in config);
        Assert.AreEqual(2f, final.y, 0.001f);
    }
}

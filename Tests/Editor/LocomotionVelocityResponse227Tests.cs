using NUnit.Framework;
using UnityEngine;

public sealed class LocomotionVelocityResponse227Tests
{
    static readonly LocomotionVelocityResponse.Settings Settings =
        new LocomotionVelocityResponse.Settings(
            riseTime: 0.2f,
            releaseTime: 0.12f,
            turnTime: 0.09f,
            reverseTime: 0.16f,
            startSpeedFloorRatio: 0.25f);

    [Test]
    public void Start_FirstTickProvidesConfiguredSpeedFloor()
    {
        var result = LocomotionVelocityResponse.Resolve(
            Vector3.zero,
            Vector3.forward,
            6f,
            1f / 60f,
            in Settings);

        Assert.AreEqual(LocomotionVelocityResponse.Branch.Start, result.ResponseBranch);
        Assert.That(result.Velocity.magnitude, Is.EqualTo(1.5f).Within(0.0001f));
        Assert.Greater(Vector3.Dot(result.Velocity.normalized, Vector3.forward), 0.999f);
    }

    [Test]
    public void NinetyDegreeTurn_DoesNotReplaceFullSpeedDirectionInOneTick()
    {
        var result = LocomotionVelocityResponse.Resolve(
            Vector3.forward * 6f,
            Vector3.right,
            6f,
            1f / 60f,
            in Settings);

        Assert.AreEqual(LocomotionVelocityResponse.Branch.Turn, result.ResponseBranch);
        Assert.Greater(result.Velocity.z, 0f, "旧方向动量应按 TurnTime 衰减，而不是一帧归零");
        Assert.Less(result.Velocity.x, 6f, "新方向不能在单帧直接取得满速");
    }

    [Test]
    public void Reverse_BrakesOldDirectionBeforeChangingSign()
    {
        var result = LocomotionVelocityResponse.Resolve(
            Vector3.forward * 6f,
            Vector3.back,
            6f,
            1f / 60f,
            in Settings);

        Assert.AreEqual(LocomotionVelocityResponse.Branch.ReverseBrake, result.ResponseBranch);
        Assert.Greater(result.Velocity.z, 0f, "首个反向 tick 仍应沿旧方向，仅降低速度");
        Assert.Less(result.Velocity.magnitude, 6f);
    }

    [Test]
    public void Release_ConvergesTowardZero()
    {
        var result = LocomotionVelocityResponse.Resolve(
            Vector3.forward * 2.4f,
            Vector3.zero,
            0f,
            1f / 60f,
            in Settings);

        Assert.AreEqual(LocomotionVelocityResponse.Branch.Release, result.ResponseBranch);
        Assert.That(result.Velocity.magnitude, Is.LessThan(2.4f));
        Assert.That(result.Velocity.magnitude, Is.GreaterThan(0f));
    }
}

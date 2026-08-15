using NUnit.Framework;
using UnityEngine;

/// <summary>213.6 / 237 L3 — Event-Time Snapshot 选槽，删除 live ChordReframe 真值。</summary>
public sealed class DirectionalRouteMotionBindingTests
{
    static readonly DirectionalTimingSnapshot Standard = DirectionalTimingProfileSO.Standard;

    [Test]
    public void BodyFixed_DKey_AlwaysRightSlot()
    {
        var cameraSlot = DirectionalFrameResolver.ResolveInputChord(
            DirectionalInputFrame.BodyFixed,
            new Vector2(1f, 0f),
            worldDir: Vector3.right,
            basisFacing: Vector3.forward);

        Assert.AreEqual(DirectionalRouteType.Right, cameraSlot);
    }

    [Test]
    public void RecentChord_OldBasis_DKey_Right_EvenIfDesiredAlreadyAligned()
    {
        var history = new DirectionInputHistory();
        history.PushDown(
            new Vector2(1f, 0f),
            Vector3.right,
            Vector3.forward,
            cameraYaw: 0f,
            unscaledTime: 1.00f);

        var result = DirectionalContextResolver.Resolve(
            triggerUnscaledTime: 1.042f,
            history,
            Standard,
            DirectionalInputFrame.LogicProjected,
            currentAxis: new Vector2(1f, 0f),
            currentWorldDir: Vector3.right,
            currentDesiredFacing: Vector3.right);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(DirectionalContextMode.RecentChord, result.Mode);
        Assert.AreEqual(DirectionalRouteType.Right, result.Slot);
        Assert.IsFalse(result.UsedLiveLogic);
        Assert.AreEqual(0.042f, result.AgeSec, 0.0001f);
    }

    [Test]
    public void Held_IgnoresExpiredSnapshot_UsesCurrentAxis()
    {
        var history = new DirectionInputHistory();
        history.PushDown(
            new Vector2(0f, 1f),
            Vector3.forward,
            Vector3.forward,
            cameraYaw: 0f,
            unscaledTime: 0f);

        var result = DirectionalContextResolver.Resolve(
            triggerUnscaledTime: 0.20f,
            history,
            Standard,
            DirectionalInputFrame.BodyFixed,
            currentAxis: new Vector2(1f, 0f),
            currentWorldDir: Vector3.right,
            currentDesiredFacing: Vector3.right);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(DirectionalContextMode.HeldMovement, result.Mode);
        Assert.AreEqual(DirectionalRouteType.Right, result.Slot);
        Assert.IsFalse(result.UsedLiveLogic);
    }

    [Test]
    public void Fail_NoSnapshotNoAxis_DoesNotInventLiveLogicSlot()
    {
        var result = DirectionalContextResolver.Resolve(
            triggerUnscaledTime: 1f,
            history: null,
            Standard,
            DirectionalInputFrame.LogicProjected,
            currentAxis: Vector2.zero,
            currentWorldDir: Vector3.zero,
            currentDesiredFacing: Vector3.right);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("no_snapshot", result.FailReason);
        Assert.IsFalse(result.UsedLiveLogic);
    }

    [Test]
    public void SoftBuffer_ReturnsMoveWithinHardPlusGrace()
    {
        var buffer = new InputModifierBuffer();
        buffer.SetBufferSeconds(0.28f);
        buffer.PushMove(new Vector2(0f, 1f), 0f);

        Assert.IsTrue(buffer.TryGetSoftBufferedMove(0.30f, 0.12f, out var move));
        Assert.AreEqual(0f, move.x, 0.01f);
        Assert.AreEqual(1f, move.y, 0.01f);
    }

    [Test]
    public void SoftBuffer_ExpiredBeyondGrace_ReturnsFalse()
    {
        var buffer = new InputModifierBuffer();
        buffer.SetBufferSeconds(0.28f);
        buffer.PushMove(new Vector2(0f, 1f), 0f);

        Assert.IsFalse(buffer.TryGetSoftBufferedMove(0.50f, 0.12f, out _));
    }
}

using NUnit.Framework;
using UnityEngine;

/// <summary>213.6 — ChordReframe 决策真值（BodyFixed 屏感 vs LogicProjected 体轴）。</summary>
public sealed class DirectionalRouteMotionBindingTests
{
    [Test]
    public void BodyFixed_DKey_AlwaysRightSlot()
    {
        var cameraSlot = DirectionalFrameResolver.ResolveInputChord(
            DirectionalInputFrame.BodyFixed,
            new Vector2(1f, 0f),
            owner: null);

        Assert.AreEqual(DirectionalRouteType.Right, cameraSlot);
    }

    [Test]
    public void Reframe_FaceCamera_DKey_CameraRight_LogicLeft()
    {
        var cameraSlot = DirectionalFrameResolver.ResolveInputChord(
            DirectionalInputFrame.BodyFixed,
            new Vector2(1f, 0f),
            owner: null);

        var logicSlot = InputChordResolver.ResolveRelativeToLogicForward(
            Vector3.left,
            Vector3.forward,
            DirectionalRouteType.Forward);

        Assert.AreEqual(DirectionalRouteType.Right, cameraSlot);
        Assert.AreEqual(DirectionalRouteType.Left, logicSlot);
        Assert.AreNotEqual(cameraSlot, logicSlot);
    }

    [Test]
    public void Reframe_Aligned_DKey_NoReframeNeeded()
    {
        var cameraSlot = DirectionalFrameResolver.ResolveInputChord(
            DirectionalInputFrame.BodyFixed,
            new Vector2(1f, 0f),
            owner: null);

        var logicSlot = InputChordResolver.ResolveRelativeToLogicForward(
            Vector3.right,
            Vector3.forward,
            DirectionalRouteType.Forward);

        Assert.AreEqual(DirectionalRouteType.Right, cameraSlot);
        Assert.AreEqual(DirectionalRouteType.Right, logicSlot);
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

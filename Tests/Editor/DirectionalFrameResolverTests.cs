using NUnit.Framework;
using UnityEngine;

/// <summary>209.3 / 237 L3 — SplitFrame 输入分轨。LogicProjected 吃传入 basis，不读 live Logic。</summary>
public sealed class DirectionalFrameResolverTests
{
    [Test]
    public void BodyFixed_DKey_AlwaysRightSlot()
    {
        var dir = DirectionalFrameResolver.ResolveInputChord(
            DirectionalInputFrame.BodyFixed,
            new Vector2(1f, 0f),
            worldDir: Vector3.right,
            basisFacing: Vector3.forward);

        Assert.AreEqual(DirectionalRouteType.Right, dir);
    }

    [Test]
    public void LogicProjected_OldBasis_DWorldRight_RightSlot()
    {
        var dir = DirectionalFrameResolver.ResolveInputChord(
            DirectionalInputFrame.LogicProjected,
            new Vector2(1f, 0f),
            worldDir: Vector3.right,
            basisFacing: Vector3.forward);

        Assert.AreEqual(DirectionalRouteType.Right, dir);
    }

    [Test]
    public void LogicProjected_AlignedBasis_DWorldRight_ForwardSlot()
    {
        var dir = DirectionalFrameResolver.ResolveInputChord(
            DirectionalInputFrame.LogicProjected,
            new Vector2(1f, 0f),
            worldDir: Vector3.right,
            basisFacing: Vector3.right);

        Assert.AreEqual(DirectionalRouteType.Forward, dir);
    }

    [Test]
    public void LogicProjected_CameraFacesCharacter_DKey_LeftSlot()
    {
        var dir = InputChordResolver.ResolveRelativeToLogicForward(
            Vector3.left,
            Vector3.forward,
            DirectionalRouteType.Forward);

        Assert.AreEqual(DirectionalRouteType.Left, dir);
    }

    [Test]
    public void LogicProjected_Aligned_DKey_RightSlot()
    {
        var dir = InputChordResolver.ResolveRelativeToLogicForward(
            Vector3.right,
            Vector3.forward,
            DirectionalRouteType.Forward);

        Assert.AreEqual(DirectionalRouteType.Right, dir);
    }

#pragma warning disable CS0618
    [Test]
    public void CharacterStick_DKey_SameAsBodyFixed()
    {
        var stick = DirectionalFrameResolver.ResolveInputChord(
            DirectionalInputFrame.CharacterStick,
            new Vector2(1f, 0f),
            worldDir: Vector3.right,
            basisFacing: Vector3.forward);
        var body = DirectionalFrameResolver.ResolveInputChord(
            DirectionalInputFrame.BodyFixed,
            new Vector2(1f, 0f),
            worldDir: Vector3.right,
            basisFacing: Vector3.forward);
        Assert.AreEqual(body, stick);
        Assert.AreEqual(DirectionalRouteType.Right, stick);
    }

    [Test]
    public void WorldStick_DKey_SameAsBodyFixed()
    {
        var world = DirectionalFrameResolver.ResolveInputChord(
            DirectionalInputFrame.WorldStick,
            new Vector2(1f, 0f),
            worldDir: Vector3.forward,
            basisFacing: Vector3.forward);
        var body = DirectionalFrameResolver.ResolveInputChord(
            DirectionalInputFrame.BodyFixed,
            new Vector2(1f, 0f),
            worldDir: Vector3.forward,
            basisFacing: Vector3.forward);
        Assert.AreEqual(body, world);
        Assert.AreEqual(DirectionalRouteType.Right, world);
    }
#pragma warning restore CS0618
}

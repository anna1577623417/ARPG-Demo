using NUnit.Framework;
using UnityEngine;

/// <summary>209.3 L1 — SplitFrame 输入分轨真值表（EditMode）。</summary>
public sealed class DirectionalFrameResolverTests
{
    [Test]
    public void BodyFixed_DKey_AlwaysRightSlot()
    {
        var dir = DirectionalFrameResolver.ResolveInputChord(
            DirectionalInputFrame.BodyFixed,
            new Vector2(1f, 0f),
            owner: null);

        Assert.AreEqual(DirectionalRouteType.Right, dir);
    }

    [Test]
    public void LogicProjected_CameraFacesCharacter_DKey_LeftSlot()
    {
        // camFwd ≈ -logicFwd → camera-right in world ≈ -character-right
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
}

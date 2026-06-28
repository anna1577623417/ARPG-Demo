using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 206.2 — 方向输入双模式（Chord vs Motion）规约。
/// </summary>
public sealed class DodgeChordModeSpec
{
    const float DefaultChord = 0.12f;
    const float DefaultMotion = 0.20f;

    static DirectionalRouteType Resolve(
        Vector2 moveBuffered,
        float holdDur,
        out bool isMotionMode,
        float chordWin = DefaultChord,
        float motionWin = DefaultMotion)
    {
        return DirectionalDualModeResolver.Resolve(
            moveBuffered, holdDur, chordWin, motionWin, out isMotionMode, out _);
    }

    [Test]
    public void Tap_W_Then_Space_Returns_Forward_Chord()
    {
        var dir = Resolve(new Vector2(0f, 1f), 0.03f, out var motion);
        Assert.IsFalse(motion);
        Assert.AreEqual(DirectionalRouteType.Forward, dir);
    }

    [Test]
    public void Tap_D_Then_Space_Returns_Right_Chord()
    {
        var dir = Resolve(new Vector2(1f, 0f), 0.03f, out var motion);
        Assert.IsFalse(motion);
        Assert.AreEqual(DirectionalRouteType.Right, dir);
    }

    [Test]
    public void Hold_D_300ms_Then_Space_Returns_Forward_Motion()
    {
        var dir = Resolve(new Vector2(1f, 0f), 0.30f, out var motion);
        Assert.IsTrue(motion);
        Assert.AreEqual(DirectionalRouteType.Forward, dir);
    }

    [Test]
    public void Hold_W_1s_Then_Space_Returns_Forward_Motion()
    {
        var dir = Resolve(new Vector2(0f, 1f), 1f, out var motion);
        Assert.IsTrue(motion);
        Assert.AreEqual(DirectionalRouteType.Forward, dir);
    }

    [Test]
    public void Hold_150ms_Grey_Defaults_To_Motion_Sustained()
    {
        var dir = Resolve(new Vector2(1f, 0f), 0.15f, out var motion);
        Assert.IsTrue(motion);
        Assert.AreEqual(DirectionalRouteType.Forward, dir);
        Assert.AreEqual("Sustained→Motion",
            DirectionalDualModeResolver.ClassifyMode(0.15f, DefaultChord, DefaultMotion));
    }

    [Test]
    public void No_Move_Then_Space_Returns_Default_Chord()
    {
        var dir = Resolve(Vector2.zero, -1f, out var motion);
        Assert.IsFalse(motion);
        Assert.AreEqual(DirectionalRouteType.Forward, dir);
    }

    [Test]
    public void Hold_Exactly_MotionWindow_Returns_Motion()
    {
        var dir = Resolve(new Vector2(1f, 0f), DefaultMotion, out var motion);
        Assert.IsTrue(motion);
        Assert.AreEqual(DirectionalRouteType.Forward, dir);
    }

    [Test]
    public void Hold_Exactly_ChordWindow_Returns_Chord()
    {
        var dir = Resolve(new Vector2(1f, 0f), DefaultChord, out var motion);
        Assert.IsFalse(motion);
        Assert.AreEqual(DirectionalRouteType.Right, dir);
    }

    [Test]
    public void Zero_MotionWindow_Always_Motion()
    {
        var dir = Resolve(new Vector2(1f, 0f), 0f, out var motion, DefaultChord, 0f);
        Assert.IsTrue(motion);
        Assert.AreEqual(DirectionalRouteType.Forward, dir);
    }

    [Test]
    public void Huge_ChordWindow_Always_Chord()
    {
        const float huge = 99f;
        var dir = Resolve(new Vector2(1f, 0f), 0.5f, out var motion, huge, huge);
        Assert.IsFalse(motion);
        Assert.AreEqual(DirectionalRouteType.Right, dir);
    }
}

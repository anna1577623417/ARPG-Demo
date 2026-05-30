#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

public sealed class MotionCurveSegmentPresetTests
{
    [Test]
    public void ApplyEaseOut_FlattensEndTangent()
    {
        var curve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 1f),
            new Keyframe(1f, 1f, 1f, 0f));

        Assert.IsTrue(MotionCurveSegmentPresetUtility.ApplySegmentPreset(curve, 0, MotionCurveSegmentPreset.EaseOut));

        Assert.AreEqual(0f, curve.keys[1].inTangent, 0.0001f);
        Assert.Greater(Mathf.Abs(curve.keys[0].outTangent), 0.0001f);
    }

    [Test]
    public void ApplyEaseIn_FlattensStartTangent()
    {
        var curve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(1f, 1f));

        Assert.IsTrue(MotionCurveSegmentPresetUtility.ApplySegmentPreset(curve, 0, MotionCurveSegmentPreset.EaseIn));
        Assert.AreEqual(0f, curve.keys[0].outTangent, 0.0001f);
    }

    [Test]
    public void CannotApplyOnLastKey()
    {
        var curve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(1f, 1f));

        Assert.IsFalse(MotionCurveSegmentPresetUtility.CanApplySegment(curve, 1));
    }
}
#endif

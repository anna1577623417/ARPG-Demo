using NUnit.Framework;
using UnityEngine;

public sealed class AnimationGameplayTraceComparer232Tests
{
    static AnimationGameplayTraceRecord Record(
        string state = "Locomotion",
        bool grounded = true,
        int commits = 1,
        Vector3? position = null,
        float verticalSpeed = 0f)
    {
        var step = new RuntimeStepStamp(1UL, 10, 4UL, 2UL, 30, RuntimeTracePhase.LogicEnd);
        return new AnimationGameplayTraceRecord(
            in step,
            8UL,
            "walk-forward",
            state,
            2UL,
            3UL,
            grounded,
            commits,
            position ?? Vector3.zero,
            Vector3.forward * 2f,
            verticalSpeed);
    }

    [Test]
    public void EqualRecordsReturnNoDifference()
    {
        var expected = Record();
        var actual = Record(position: new Vector3(0.001f, 0f, 0f));
        var tolerance = new AnimationGameplayTraceTolerance(0.01f, 0.01f, 0.01f);

        Assert.IsFalse(AnimationGameplayTraceComparer.TryFindDifference(
            in expected, in actual, in tolerance, out _));
    }

    [Test]
    public void StrictGameplayDifferenceIsReportedBeforeFloatDifference()
    {
        var expected = Record(state: "Locomotion");
        var actual = Record(state: "Airborne", position: Vector3.one * 100f);
        var tolerance = new AnimationGameplayTraceTolerance(0f, 0f, 0f);

        Assert.IsTrue(AnimationGameplayTraceComparer.TryFindDifference(
            in expected, in actual, in tolerance, out var difference));
        Assert.AreEqual(AnimationGameplayTraceDifferenceKind.StrictGameplay, difference.Kind);
        Assert.AreEqual("StateId", difference.Field);
    }

    [Test]
    public void PositionOutsideBudgetReportsAbsoluteError()
    {
        var expected = Record();
        var actual = Record(position: new Vector3(0.2f, 0f, 0f));
        var tolerance = new AnimationGameplayTraceTolerance(0.05f, 0.01f, 0.01f);

        Assert.IsTrue(AnimationGameplayTraceComparer.TryFindDifference(
            in expected, in actual, in tolerance, out var difference));
        Assert.AreEqual("Position", difference.Field);
        Assert.That(difference.AbsoluteError, Is.EqualTo(0.2f).Within(0.0001f));
    }

    [Test]
    public void NaNIsNeverAcceptedByTolerance()
    {
        var expected = Record();
        var actual = Record(verticalSpeed: float.NaN);
        var tolerance = new AnimationGameplayTraceTolerance(100f, 100f, 100f);

        Assert.IsTrue(AnimationGameplayTraceComparer.TryFindDifference(
            in expected, in actual, in tolerance, out var difference));
        Assert.AreEqual(AnimationGameplayTraceDifferenceKind.InvalidNumber, difference.Kind);
    }
}

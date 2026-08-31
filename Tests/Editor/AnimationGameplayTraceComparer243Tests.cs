using NUnit.Framework;
using UnityEngine;

public sealed class AnimationGameplayTraceComparer243Tests
{
    static AnimationGameplayTraceRecord Record(ulong scenarioStep, string state = "Locomotion")
    {
        var step = new RuntimeStepStamp(2UL, 12, scenarioStep, 0UL, (int)scenarioStep, RuntimeTracePhase.LogicEnd);
        return new AnimationGameplayTraceRecord(
            in step, scenarioStep, "anchor", state, 3UL, 4UL, true, 1,
            Vector3.zero, Vector3.forward, 0f);
    }

    [Test]
    public void ReplayReportsTheFirstExactStepDifference()
    {
        var expected = new AnimationTransitionReplay243(4, 2);
        var actual = new AnimationTransitionReplay243(4, 2);
        var expectedFirst = Record(1UL);
        var expectedSecond = Record(2UL, "Locomotion");
        var actualFirst = Record(1UL);
        var actualSecond = Record(2UL, "Airborne");
        expected.AddGameplay(in expectedFirst);
        expected.AddGameplay(in expectedSecond);
        actual.AddGameplay(in actualFirst);
        actual.AddGameplay(in actualSecond);

        var tolerance = new AnimationGameplayTraceTolerance(0f, 0f, 0f);
        Assert.IsTrue(expected.TryFindFirstGameplayDifference(actual, in tolerance, out var expectedIndex, out var actualIndex, out var difference));
        Assert.AreEqual(1, expectedIndex);
        Assert.AreEqual(1, actualIndex);
        Assert.AreEqual("StateId", difference.Field);
    }

    [Test]
    public void ReplayRingBufferKeepsFixedCapacityAndLatestRecords()
    {
        var replay = new AnimationTransitionReplay243(2, 1);
        var first = Record(1UL);
        var second = Record(2UL);
        var third = Record(3UL);
        replay.AddGameplay(in first);
        replay.AddGameplay(in second);
        replay.AddGameplay(in third);

        Assert.AreEqual(2, replay.GameplayCount);
        Assert.AreEqual(2UL, replay.GetGameplayAt(0).ScenarioStepId);
        Assert.AreEqual(3UL, replay.GetGameplayAt(1).ScenarioStepId);
    }
}

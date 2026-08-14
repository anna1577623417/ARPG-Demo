using NUnit.Framework;

public sealed class RuntimeStepCursor232Tests
{
    [Test]
    public void LogicAndPhysicsCountersAreIndependentAndMonotonic()
    {
        var cursor = new RuntimeStepCursor();
        cursor.Bind(7UL, 42);

        var logic1 = cursor.BeginLogic(10);
        var physics1 = cursor.BeginPhysics(10);
        var logic2 = cursor.BeginLogic(11);

        Assert.AreEqual(1UL, logic1.EntityLogicStepId);
        Assert.AreEqual(0UL, logic1.EntityPhysicsStepId);
        Assert.AreEqual(1UL, physics1.EntityPhysicsStepId);
        Assert.AreEqual(2UL, logic2.EntityLogicStepId);
        Assert.AreEqual(1UL, logic2.EntityPhysicsStepId);
    }

    [Test]
    public void RebindDoesNotResetCountersForSameCursorLifetime()
    {
        var cursor = new RuntimeStepCursor();
        cursor.Bind(3UL, 9);
        cursor.BeginLogic(1);
        cursor.Bind(3UL, 9);

        Assert.AreEqual(2UL, cursor.BeginLogic(2).EntityLogicStepId);
    }

    [Test]
    public void CaptureChangesPhaseWithoutAdvancingCounters()
    {
        var cursor = new RuntimeStepCursor();
        cursor.Bind(1UL, 5);
        cursor.BeginLogic(2);

        var commit = cursor.Capture(RuntimeTracePhase.MotorCommit, 2);

        Assert.AreEqual(1UL, commit.EntityLogicStepId);
        Assert.AreEqual(RuntimeTracePhase.MotorCommit, commit.Phase);
    }
}

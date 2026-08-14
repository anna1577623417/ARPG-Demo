using NUnit.Framework;

public sealed class TurnPresentationInterrupt232Tests
{
    [Test]
    public void EventCarriesGameplayFactWithoutPresenterReference()
    {
        var step = new RuntimeStepStamp(1UL, 12, 8UL, 3UL, 20, RuntimeTracePhase.StateLogicEnd);
        var evt = new TurnPresentationInterruptedEvent(
            12, step, 4U, TurnType.Turn180, TurnInterruptReason.Jump);

        Assert.AreEqual(12, evt.EntityInstanceId);
        Assert.AreEqual(4U, evt.TurnGeneration);
        Assert.AreEqual(TurnType.Turn180, evt.PreviousTurnType);
        Assert.AreEqual(TurnInterruptReason.Jump, evt.Reason);
        Assert.AreEqual(step, evt.Step);
    }
}

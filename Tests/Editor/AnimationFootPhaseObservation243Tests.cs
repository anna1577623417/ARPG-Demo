using NUnit.Framework;

public sealed class AnimationFootPhaseObservation243Tests
{
    [Test]
    public void UnknownNeverPretendsToBeAContactAndCompatibilityRequiresFacts()
    {
        var unknown = AnimationFootPhaseObservation243.Unknown;
        var left = new AnimationFootPhaseObservation243(1.2f, AnimationFootContact243.Left, true);
        var doubleSupport = new AnimationFootPhaseObservation243(0.5f, AnimationFootContact243.Double, true);

        Assert.IsFalse(unknown.IsValid);
        Assert.IsFalse(unknown.IsCompatibleWith(in left));
        Assert.IsTrue(left.IsCompatibleWith(in doubleSupport));
        Assert.AreEqual(0.2f, left.Phase, 0.0001f);
    }
}

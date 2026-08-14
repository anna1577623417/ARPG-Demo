using NUnit.Framework;

public sealed class LegacyAnimatorRootMotionPolicy232Tests
{
    [Test]
    public void NotRequestedDoesNotEnterLegacyPath()
    {
        Assert.AreEqual(
            LegacyAnimatorRootMotionDecision.NotRequested,
            LegacyAnimatorRootMotionPolicy.Resolve(false));
    }

    [Test]
    public void RequestedDirectRootMotionIsDeniedWithEmptyAllowlist()
    {
        Assert.AreEqual(
            LegacyAnimatorRootMotionDecision.Denied,
            LegacyAnimatorRootMotionPolicy.Resolve(true));
    }
}

using NUnit.Framework;

public sealed class AnimationPresentationProfile243Tests
{
    sealed class EnemyProfile : IAnimationPresentationProfile243
    {
        public int EntityInstanceId => 77;
        public AnimationRequestDomain SupportedDomains => AnimationRequestDomain.Action;
        public string VariantKey => "enemy.basic";
        public TransitionChannelCapabilities243 ChannelCapabilities => TransitionChannelCapabilities243.TwoPortFallback;
        public bool TryGetPresentationFact(string key, out string value) { value = string.Empty; return false; }
    }

    [Test]
    public void MissingEnemyFactRemainsExplicitlyUnavailable()
    {
        IAnimationPresentationProfile243 profile = new EnemyProfile();
        Assert.IsFalse(profile.TryGetPresentationFact("player-lease", out var value));
        Assert.AreEqual(string.Empty, value);
        Assert.AreEqual("enemy.basic", profile.VariantKey);
    }
}

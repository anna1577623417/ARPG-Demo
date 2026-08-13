using NUnit.Framework;
using UnityEngine;

public sealed class EntityAnimController227Tests
{
    [Test]
    public void SameClip_IdempotentRequest_IsSuppressed()
    {
        var clip = new AnimationClip();

        Assert.That(
            EntityAnimController.ShouldSuppressSameClipReplay(clip, clip, true, restartIfSameClip: false),
            Is.True);
    }

    [Test]
    public void SameClip_ExplicitRestart_RemainsAllowed()
    {
        var clip = new AnimationClip();

        Assert.That(
            EntityAnimController.ShouldSuppressSameClipReplay(clip, clip, true, restartIfSameClip: true),
            Is.False);
    }

    [Test]
    public void DifferentClip_IdempotentRequest_RemainsAllowed()
    {
        var current = new AnimationClip();
        var requested = new AnimationClip();

        Assert.That(
            EntityAnimController.ShouldSuppressSameClipReplay(current, requested, true, restartIfSameClip: false),
            Is.False);
    }

    [Test]
    public void FinitePlayable_ContinuousReplay_RequiresLoopUpgrade()
    {
        Assert.That(
            EntityAnimController.ShouldUpgradeLoopContract(
                currentCodeLooping: false,
                requestedLoop: true),
            Is.True);
    }

    [Test]
    public void LoopingPlayable_ContinuousReplay_DoesNotReapplyUpgrade()
    {
        Assert.That(
            EntityAnimController.ShouldUpgradeLoopContract(
                currentCodeLooping: true,
                requestedLoop: true),
            Is.False);
    }
}

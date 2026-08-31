using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class AnimationPlayRequest243Tests
{
    static AnimationPlayRequest Request(bool explicitRestart = false, string clipKey = "walk")
    {
        return new AnimationPlayRequest(
            7UL, 42, 10UL, 2UL, AnimationRequestDomain.Locomotion, "walk-loop", clipKey, null,
            AnimationLoopPolicy.Loop, 1f, 0f, AnimationRequestPriority.Normal,
            AnimationInterruptPolicy.Interruptible, 99UL, "default", AnimationRequestSourceKind.Observation,
            3U, 4UL, 5UL, explicitRestart);
    }

    [Test]
    public void ExplicitRestartIsDistinctFromIdempotentReplay()
    {
        Assert.IsFalse(Request().ExplicitRestart);
        Assert.IsTrue(Request(explicitRestart: true).ExplicitRestart);
    }

    [Test]
    public void ClipIdentityCanBeResolvedLaterByKey()
    {
        Assert.IsTrue(Request().HasClipIdentity);
        Assert.IsFalse(Request(clipKey: string.Empty).HasClipIdentity);
    }

    [Test]
    public void RequestContractHasNoGameplayCommandFields()
    {
        var path = Path.Combine(Application.dataPath, "GameMain/Scripts/2_Framework/Animation/Runtime/AnimationPlayRequest.cs");
        var source = File.ReadAllText(path);

        Assert.That(source, Does.Not.Contain("TargetGameplayState"));
        Assert.That(source, Does.Not.Contain("CompleteAction"));
        Assert.That(source, Does.Not.Contain("GroundedOverride"));
        Assert.That(source, Does.Not.Contain("LogicForwardOverride"));
        Assert.That(source, Does.Not.Contain("CameraHeadingCommand"));
    }
}

using NUnit.Framework;
using UnityEngine;

public sealed class MotionDurationResolverTests
{
    [Test]
    public void Resolve_PrefersActionDurationWhenUseActionDuration()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        var profile = ScriptableObject.CreateInstance<MotionProfileSO>();
        action.Duration = 0.55f;
        action.MotionProfile = profile;
        profile.UseActionDuration = true;

        Assert.AreEqual(0.55f, MotionDurationResolver.Resolve(action), 0.001f);

        Object.DestroyImmediate(action);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void Resolve_IgnoresObsoleteBurstDurationOnProfile()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        var profile = ScriptableObject.CreateInstance<MotionProfileSO>();
        action.Duration = 0.55f;
        action.MotionProfile = profile;
#pragma warning disable 0618
        profile.BurstDurationSeconds = 1.2f;
#pragma warning restore 0618

        Assert.AreEqual(0.55f, MotionDurationResolver.Resolve(action), 0.001f);

        Object.DestroyImmediate(action);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void ResolveWithTimeSync_MatchAnimation_WithoutClip_Unchanged()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        var profile = ScriptableObject.CreateInstance<MotionProfileSO>();
        action.Duration = 0.4f;
        action.MotionProfile = profile;
        profile.TimeSync = MotionTimeSyncMode.MatchAnimation;

        var sync = MotionDurationResolver.ResolveWithTimeSync(action);
        Assert.AreEqual(0.4f, sync.MotionDurationSeconds, 0.001f);

        Object.DestroyImmediate(action);
        Object.DestroyImmediate(profile);
    }
}

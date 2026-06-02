using NUnit.Framework;
using UnityEngine;

public sealed class MotionDurationResolverTests
{
    [Test]
    public void Resolve_UsesActionDuration()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        var profile = ScriptableObject.CreateInstance<MotionProfileSO>();
        action.Duration = 0.55f;
        action.MotionProfile = profile;

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
    public void ResolveClipWallClockSeconds_UsesActionAnimSpeed()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        var clip = new AnimationClip();
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 0f));
        action.MainClip = clip;
        action.AnimSpeed = 2f;

        Assert.AreEqual(clip.length / 2f, MotionDurationResolver.ResolveClipWallClockSeconds(action), 0.001f);

        Object.DestroyImmediate(action);
        Object.DestroyImmediate(clip);
    }
}

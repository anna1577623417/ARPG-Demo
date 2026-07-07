using NUnit.Framework;
using UnityEngine;

/// <summary>210.5 Landing 1 — ActionYawResolver / SampleActionYawDegrees 真值表。</summary>
public sealed class ActionYawResolverTests
{
    [Test]
    public void DefaultProfile_ActionYaw_IsZero()
    {
        var profile = ScriptableObject.CreateInstance<MotionProfileSO>();
        try
        {
            Assert.AreEqual(YawPolicyMode.None, profile.YawPolicy);
            Assert.IsFalse(profile.UsesActionYaw);
            Assert.AreEqual(0f, profile.SampleActionYawDegrees(0f), 0.001f);
            Assert.AreEqual(0f, profile.SampleActionYawDegrees(1f), 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void ConstantPolicy_AnyTime_ReturnsStart()
    {
        var profile = ScriptableObject.CreateInstance<MotionProfileSO>();
        try
        {
            profile.YawPolicy = YawPolicyMode.Constant;
            profile.YawStartDegrees = 30f;
            profile.YawEndDegrees = 99f;

            Assert.AreEqual(30f, profile.SampleActionYawDegrees(0f), 0.001f);
            Assert.AreEqual(30f, profile.SampleActionYawDegrees(0.5f), 0.001f);
            Assert.AreEqual(30f, profile.SampleActionYawDegrees(1f), 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void CurvePolicy_LinearBlend_InterpolatesStartToEnd()
    {
        var profile = ScriptableObject.CreateInstance<MotionProfileSO>();
        try
        {
            profile.YawPolicy = YawPolicyMode.Curve;
            profile.YawStartDegrees = 0f;
            profile.YawEndDegrees = 90f;
            profile.YawBlendOverTime = AnimationCurve.Linear(0f, 0f, 1f, 1f);

            Assert.AreEqual(0f, profile.SampleActionYawDegrees(0f), 0.001f);
            Assert.AreEqual(45f, profile.SampleActionYawDegrees(0.5f), 0.001f);
            Assert.AreEqual(90f, profile.SampleActionYawDegrees(1f), 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void ResolveForwardFromBurstYaw_AddsOffset()
    {
        var forward = ActionYawResolver.ResolveForwardFromBurstYaw(0f, 90f);
        Assert.AreEqual(90f, Vector3.Angle(Vector3.forward, forward), 0.5f);
    }

    [Test]
    public void ResolveForwardFromBurstForward_PreservesBurstBasis()
    {
        var burst = Quaternion.Euler(0f, 45f, 0f) * Vector3.forward;
        var resolved = ActionYawResolver.ResolveForwardFromBurstForward(burst, 0f);
        Assert.AreEqual(45f, Vector3.Angle(Vector3.forward, resolved), 0.5f);
    }
}

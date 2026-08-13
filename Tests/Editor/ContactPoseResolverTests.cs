using NUnit.Framework;
using UnityEngine;

/// <summary>224.1 L3 — ContactPoseResolver 纯逻辑（ACP-*）。</summary>
public sealed class ContactPoseResolverTests
{
    [Test]
    public void ACP01_Static_SameFrozenPose_IndependentOfTickTime()
    {
        var anchor = new ContactAnchorPose(Vector3.zero, Quaternion.identity, Vector3.one, "root");
        var spec = MakeSpec(ContactAnchorBindingMode.StaticAtWindowStart, ContactSweepPolicy.None);
        var begin = ContactPoseResolver.ResolveForBegin(in spec, in anchor, 0.2f);
        Assert.IsTrue(begin.IsFrozen);

        var movedAnchor = new ContactAnchorPose(Vector3.right * 5f, Quaternion.identity, Vector3.one, "root");
        var tick = ContactPoseResolver.ResolveForTick(in spec, begin, in movedAnchor, 0.8f);
        Assert.AreEqual(begin.Position, tick.Position);
        Assert.IsTrue(tick.IsFrozen);
    }

    [Test]
    public void ACP02_Follow_ChangesWithAnchor()
    {
        var spec = MakeSpec(ContactAnchorBindingMode.FollowAnchor, ContactSweepPolicy.None);
        var a0 = new ContactAnchorPose(Vector3.zero, Quaternion.identity, Vector3.one, "hand");
        var a1 = new ContactAnchorPose(Vector3.up, Quaternion.identity, Vector3.one, "hand");
        var p0 = ContactPoseResolver.ResolveForTick(in spec, null, in a0, 0.1f);
        var p1 = ContactPoseResolver.ResolveForTick(in spec, null, in a1, 0.2f);
        Assert.AreNotEqual(p0.Position, p1.Position);
        Assert.IsFalse(p1.IsFrozen);
    }

    [Test]
    public void ACP03_PreviewStatic_SourceTimeIsWindowStart()
    {
        var spec = MakeSpec(ContactAnchorBindingMode.StaticAtWindowStart, ContactSweepPolicy.None);
        var anchor = new ContactAnchorPose(Vector3.one, Quaternion.identity, Vector3.one, "hips");
        var pose = ContactPoseResolver.ResolveForPreview(in spec, in anchor, 0.15f, 0.77f);
        Assert.AreEqual(0.15f, pose.SourceNormalizedTime);
        Assert.IsTrue(pose.IsFrozen);
    }

    [Test]
    public void ACP04_LegacyMotionMapping_StillProducesSweepPolicy()
    {
        ContactAuthoringAdapter.MapLegacyMotion(
            ContactMotionKind.SweepBetweenFrames, out var binding, out var sweep);
        Assert.AreEqual(ContactAnchorBindingMode.FollowAnchor, binding);
        Assert.AreEqual(ContactSweepPolicy.BetweenSamples, sweep);
    }

    static ResolvedContactSpec MakeSpec(
        ContactAnchorBindingMode binding,
        ContactSweepPolicy sweep)
    {
        return new ResolvedContactSpec(
            null,
            null,
            HitShapeMode.Volume,
            null,
            null,
            null,
            SpawnSource.SelfRootBone,
            Vector3.forward,
            Quaternion.identity,
            ContactAuthoringAdapter.ToLegacyMotion(binding, sweep),
            binding,
            sweep,
            ContactAnchorScalePolicy.IgnoreAnchorScale,
            usesLegacyAuthoring: false,
            ContactQueryPolicy.Default,
            HitPolicyParams.Default,
            CombatAttackProfile.Default,
            1);
    }
}

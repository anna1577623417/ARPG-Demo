using NUnit.Framework;
using UnityEngine;

public sealed class MotionCandidateSelector243Tests
{
    static PoseSample Pose(float velocity) => new PoseSample { LocalRot = new[] { Quaternion.identity }, HipVel = Vector3.forward * velocity };
    [Test] public void BoundedMatchingCandidatesSelectDeterministically()
    {
        var source = Pose(1f); var a = Pose(1f); var b = Pose(4f);
        var candidates = new[] { new MotionCandidate243("B", AnimationRequestDomain.Locomotion, "", "", 0, "logic", in b), new MotionCandidate243("A", AnimationRequestDomain.Locomotion, "", "", 0, "logic", in a) };
        Assert.IsTrue(MotionCandidateSelector243.TrySelect(in source, AnimationRequestDomain.Locomotion, "", "", 0, "logic", candidates, out var selected, out _)); Assert.AreEqual("A", selected.ClipKey);
        var overflow = new MotionCandidate243[MotionCandidateSelector243.MaximumCandidates + 1]; Assert.IsFalse(MotionCandidateSelector243.TrySelect(in source, AnimationRequestDomain.Locomotion, "", "", 0, "logic", overflow, out _, out _));
    }
}

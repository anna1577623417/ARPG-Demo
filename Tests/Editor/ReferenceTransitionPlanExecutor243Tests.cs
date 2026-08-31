using NUnit.Framework;
using UnityEngine;

public sealed class ReferenceTransitionPlanExecutor243Tests
{
    static AnimationPlayRequest Request(AnimationClip clip, ulong requestId = 10UL)
    {
        return new AnimationPlayRequest(
            requestId, 42, 100UL, 8UL, AnimationRequestDomain.Action,
            "action.skill_entry_01", clip != null ? clip.name : "Action_Attack", clip,
            AnimationLoopPolicy.Finite, 1.25f, 0.2f, AnimationRequestPriority.Elevated,
            AnimationInterruptPolicy.Interruptible, requestId, "action",
            AnimationRequestSourceKind.Graph, 7U, 3UL, 7UL, true);
    }

    static TransitionContext Context(in AnimationPlayRequest request, TransitionMode mode = TransitionMode.CrossFade, bool phaseValid = false)
    {
        var source = new AnimationPresentationState243("idle", "logic-root", 0.1f, 0.2f, phaseValid, 0, "base");
        return new TransitionContext(
            in request, in source, "logic-root", 0.4f, phaseValid,
            mode, RootTranslationChannelMode.Preserve, 0.15f, 0f,
            phaseValid ? AnimationPhaseMatchMode.IfValid : AnimationPhaseMatchMode.Off,
            false, false, "action/skill_entry_01", "hash");
    }

    [Test]
    public void ServicePlanAndExecutorPreflightPreserveCrossFadeRequestFields()
    {
        var clip = new AnimationClip { name = "Attack" };
        try
        {
            var request = Request(clip);
            var context = Context(in request);
            var service = new AnimationTransitionService(null, null, null);
            var plan = service.Resolve(in context);

            var built = ReferenceTransitionPlanExecutor243.TryBuildPlaybackCommand(
                42, in plan, in request, out var command, out var disposition);

            Assert.IsTrue(built);
            Assert.AreEqual(ReferenceTransitionPlanExecutionDisposition243.None, disposition);
            Assert.AreSame(clip, command.Clip);
            Assert.AreEqual(0.15f, command.BlendDuration);
            Assert.AreEqual(1.25f, command.Speed);
            Assert.AreEqual(0.2f, command.NormalizedStart);
            Assert.IsTrue(command.RestartIfSameClip);
        }
        finally
        {
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void MissingResolvedClipFailsOpenWithoutMixerCommand()
    {
        var request = Request(null);
        var context = Context(in request);
        var plan = new AnimationTransitionService(null, null, null).Resolve(in context);

        var built = ReferenceTransitionPlanExecutor243.TryBuildPlaybackCommand(
            42, in plan, in request, out _, out var disposition);

        Assert.IsFalse(built);
        Assert.AreEqual(ReferenceTransitionPlanExecutionDisposition243.MissingResolvedClip, disposition);
    }

    [Test]
    public void SameClipSuppressionAndRejectedPlanNeverReachExecutor()
    {
        var clip = new AnimationClip { name = "Run" };
        try
        {
            var request = Request(clip);
            var sameSource = new AnimationPresentationState243("Run", "logic-root", 0f, 0f, false, 0, "base");
            var sameContext = new TransitionContext(
                in request, in sameSource, "logic-root", 0f, false,
                TransitionMode.CrossFade, RootTranslationChannelMode.Preserve, 0.1f, 0f,
                AnimationPhaseMatchMode.Off, false, false, "path", "hash");
            var suppressedPlan = new AnimationTransitionService(null, null, null).Resolve(in sameContext);
            Assert.IsFalse(ReferenceTransitionPlanExecutor243.TryBuildPlaybackCommand(
                42, in suppressedPlan, in request, out _, out var suppressed));
            Assert.AreEqual(ReferenceTransitionPlanExecutionDisposition243.PlanDoesNotSubmit, suppressed);

            var rejected = new TransitionPlan(
                request.RequestId, 42, request.SourceTick, 1, "idle", "Run", "Run", 0f,
                TransitionMode.Snap, SpatialHandoffMode.Atomic, RootYawChannelMode.SnapToTarget,
                RootTranslationChannelMode.Atomic, PoseChannelMode.Snap, string.Empty, 0f, 0f,
                AnimationPhaseMatchMode.Off, 0f, 0f, 0, "base", 1f, request.ActionLeaseVersion,
                request.InterruptPolicy, AnimationTransitionFallbackReason.CrossSpaceRootBlend,
                "path", "hash", false, true);
            Assert.IsFalse(ReferenceTransitionPlanExecutor243.TryBuildPlaybackCommand(
                42, in rejected, in request, out _, out var rejectedDisposition));
            Assert.AreEqual(ReferenceTransitionPlanExecutionDisposition243.RejectedPlan, rejectedDisposition);
        }
        finally
        {
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void PhaseMatchIsNotSilentlyDowngradedToCrossFade()
    {
        var clip = new AnimationClip { name = "Run" };
        try
        {
            var request = Request(clip);
            var context = Context(in request, TransitionMode.CrossFade, phaseValid: true);
            var plan = new AnimationTransitionService(null, null, null).Resolve(in context);

            Assert.AreEqual(TransitionMode.PhaseMatch, plan.TransitionMode);
            Assert.IsFalse(ReferenceTransitionPlanExecutor243.TryBuildPlaybackCommand(
                42, in plan, in request, out _, out var disposition));
            Assert.AreEqual(ReferenceTransitionPlanExecutionDisposition243.UnsupportedTransitionMode, disposition);
        }
        finally
        {
            Object.DestroyImmediate(clip);
        }
    }
}

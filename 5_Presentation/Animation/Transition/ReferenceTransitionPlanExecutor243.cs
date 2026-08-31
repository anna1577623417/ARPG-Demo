using UnityEngine;

/// <summary>Result of one attempt to hand a validated TransitionPlan to the existing mixer writer.</summary>
public enum ReferenceTransitionPlanExecutionDisposition243 : byte
{
    None = 0,
    Submitted = 1,
    WriterUnavailable = 2,
    EntityMismatch = 3,
    RequestMismatch = 4,
    PlanDoesNotSubmit = 5,
    RejectedPlan = 6,
    MissingResolvedClip = 7,
    UnsupportedTransitionMode = 8,
    UnsupportedChannelCapability = 9,
    DuplicateRequest = 10,
}

/// <summary>Immutable execution evidence for one request. The writer is invoked at most once per request id.</summary>
public readonly struct ReferenceTransitionPlanExecutionResult243
{
    public readonly ulong RequestId;
    public readonly int EntityInstanceId;
    public readonly ReferenceTransitionPlanExecutionDisposition243 Disposition;
    public readonly bool WriterInvoked;

    public ReferenceTransitionPlanExecutionResult243(
        ulong requestId,
        int entityInstanceId,
        ReferenceTransitionPlanExecutionDisposition243 disposition,
        bool writerInvoked)
    {
        RequestId = requestId;
        EntityInstanceId = entityInstanceId;
        Disposition = disposition;
        WriterInvoked = writerInvoked;
    }
}

/// <summary>Concrete mixer invocation derived from a Plan and its request, before any Unity side effect.</summary>
public readonly struct ReferenceTransitionPlanPlaybackCommand243
{
    public readonly AnimationClip Clip;
    public readonly float BlendDuration;
    public readonly float Speed;
    public readonly bool IsLooping;
    public readonly float NormalizedStart;
    public readonly bool RestartIfSameClip;
    public readonly string RequestSource;

    public ReferenceTransitionPlanPlaybackCommand243(
        AnimationClip clip,
        float blendDuration,
        float speed,
        bool isLooping,
        float normalizedStart,
        bool restartIfSameClip,
        string requestSource)
    {
        Clip = clip;
        BlendDuration = blendDuration;
        Speed = speed;
        IsLooping = isLooping;
        NormalizedStart = normalizedStart;
        RestartIfSameClip = restartIfSameClip;
        RequestSource = requestSource ?? string.Empty;
    }
}

/// <summary>
/// 243.9 L6 — The sole Plan-to-mixer adapter. It owns no Graph or Gameplay state and only invokes
/// <see cref="EntityAnimController.Play"/> after a Plan has been accepted and reduced to capabilities
/// already present in the current two-port mixer.
/// </summary>
public sealed class ReferenceTransitionPlanExecutor243
{
    static readonly TransitionChannelCapabilities243 TwoPortCapabilities =
        TransitionChannelCapabilities243.TwoPortFallback;

    readonly EntityAnimController writer;
    readonly int entityInstanceId;
    ulong lastSubmittedRequestId;
    int submittedCount;

    public ReferenceTransitionPlanExecutor243(EntityAnimController writer, int entityInstanceId)
    {
        this.writer = writer;
        this.entityInstanceId = entityInstanceId;
    }

    public int SubmittedCount => submittedCount;
    public ulong LastSubmittedRequestId => lastSubmittedRequestId;

    public bool TryExecute(
        in TransitionPlan plan,
        in AnimationPlayRequest request,
        out ReferenceTransitionPlanExecutionDisposition243 disposition)
    {
        ReferenceTransitionPlanExecutionResult243 executionResult;
        var executed = TryExecute(in plan, in request, out executionResult);
        disposition = executionResult.Disposition;
        return executed;
    }

    public bool TryExecute(
        in TransitionPlan plan,
        in AnimationPlayRequest request,
        out ReferenceTransitionPlanExecutionResult243 result)
    {
        if (writer == null || !writer.IsGraphValid)
        {
            result = Result(in request, ReferenceTransitionPlanExecutionDisposition243.WriterUnavailable, false);
            return false;
        }

        if (!TryBuildPlaybackCommand(
                entityInstanceId, in plan, in request,
                out var command, out var disposition))
        {
            result = Result(in request, disposition, false);
            return false;
        }

        if (request.RequestId == 0UL || request.RequestId == lastSubmittedRequestId)
        {
            result = Result(in request, ReferenceTransitionPlanExecutionDisposition243.DuplicateRequest, false);
            return false;
        }

        writer.Play(
            command.Clip,
            command.BlendDuration,
            command.Speed,
            command.IsLooping,
            command.NormalizedStart,
            command.RestartIfSameClip,
            command.RequestSource);
        lastSubmittedRequestId = request.RequestId;
        submittedCount++;
        result = Result(in request, ReferenceTransitionPlanExecutionDisposition243.Submitted, true);
        return true;
    }

    static ReferenceTransitionPlanExecutionResult243 Result(
        in AnimationPlayRequest request,
        ReferenceTransitionPlanExecutionDisposition243 disposition,
        bool writerInvoked) =>
        new ReferenceTransitionPlanExecutionResult243(
            request.RequestId,
            request.EntityInstanceId,
            disposition,
            writerInvoked);

    /// <summary>Pure preflight used by tests and by TryExecute. It never resolves library keys or falls back to Gameplay.</summary>
    public static bool TryBuildPlaybackCommand(
        int expectedEntityInstanceId,
        in TransitionPlan plan,
        in AnimationPlayRequest request,
        out ReferenceTransitionPlanPlaybackCommand243 command,
        out ReferenceTransitionPlanExecutionDisposition243 disposition)
    {
        command = default;
        if (expectedEntityInstanceId == 0 || plan.EntityInstanceId != expectedEntityInstanceId)
        {
            disposition = ReferenceTransitionPlanExecutionDisposition243.EntityMismatch;
            return false;
        }

        if (plan.RequestId == 0UL || plan.RequestId != request.RequestId
            || plan.EntityInstanceId != request.EntityInstanceId)
        {
            disposition = ReferenceTransitionPlanExecutionDisposition243.RequestMismatch;
            return false;
        }

        if (plan.IsRejected)
        {
            disposition = ReferenceTransitionPlanExecutionDisposition243.RejectedPlan;
            return false;
        }

        if (!plan.ShouldSubmitPlayback)
        {
            disposition = ReferenceTransitionPlanExecutionDisposition243.PlanDoesNotSubmit;
            return false;
        }

        if (request.ResolvedClip == null)
        {
            disposition = ReferenceTransitionPlanExecutionDisposition243.MissingResolvedClip;
            return false;
        }

        if (!TwoPortCapabilities.Supports(in plan))
        {
            disposition = ReferenceTransitionPlanExecutionDisposition243.UnsupportedChannelCapability;
            return false;
        }

        // The existing two-port mixer can safely execute only Snap/CrossFade. Phase alignment,
        // inertialization and a library-key resolver remain explicit future capabilities, rather than
        // silently pretending a normal crossfade fulfilled their Plan.
        if (plan.TransitionMode != TransitionMode.Snap && plan.TransitionMode != TransitionMode.CrossFade)
        {
            disposition = ReferenceTransitionPlanExecutionDisposition243.UnsupportedTransitionMode;
            return false;
        }

        var isLooping = request.LoopPolicy == AnimationLoopPolicy.Loop
            || (request.LoopPolicy == AnimationLoopPolicy.UseClipDefault && request.ResolvedClip.isLooping);
        var blendDuration = plan.TransitionMode == TransitionMode.Snap ? 0f : plan.BlendDuration;
        command = new ReferenceTransitionPlanPlaybackCommand243(
            request.ResolvedClip,
            Mathf.Max(0f, blendDuration),
            plan.PlaybackSpeed,
            isLooping,
            plan.TargetEntryTime,
            request.ExplicitRestart,
            "TransitionPlan243." + plan.GraphNodePath);
        disposition = ReferenceTransitionPlanExecutionDisposition243.None;
        return true;
    }
}

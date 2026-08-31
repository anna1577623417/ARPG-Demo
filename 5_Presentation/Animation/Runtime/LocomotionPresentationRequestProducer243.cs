using System;

/// <summary>Result of projecting one resolved Locomotion snapshot into Shadow request candidates.</summary>
public enum LocomotionPresentationRequestDisposition243 : byte
{
    None = 0,
    Produced = 1,
    InvalidObservation = 2,
    UnsupportedState = 3,
}

/// <summary>Immutable legacy/Graph candidates for one Gameplay-resolved Locomotion state.</summary>
public readonly struct LocomotionPresentationRequestPair243
{
    public readonly AnimationPlayRequest LegacyRequest;
    public readonly AnimationPlayRequest GraphRequest;
    public readonly LocomotionStateId State;
    public readonly string GraphNodePath;

    public LocomotionPresentationRequestPair243(
        in AnimationPlayRequest legacyRequest,
        in AnimationPlayRequest graphRequest,
        LocomotionStateId state,
        string graphNodePath)
    {
        LegacyRequest = legacyRequest;
        GraphRequest = graphRequest;
        State = state;
        GraphNodePath = graphNodePath ?? string.Empty;
    }
}

/// <summary>
/// 243.9 L4 — Pure Locomotion candidate producer. It reads an already-resolved snapshot and never
/// changes locomotion state, Stop duration, motion, Action routing, or actual playback.
/// </summary>
public static class LocomotionPresentationRequestProducer243
{
    public static bool TryBuild(
        in AnimationObservation observation,
        in LocomotionPresentationSnapshot snapshot,
        out LocomotionPresentationRequestPair243 pair,
        out LocomotionPresentationRequestDisposition243 disposition)
    {
        pair = default;
        if (!observation.IsSchemaSupported
            || !observation.IsKnown(AnimationObservationKnownMask.Entity)
            || observation.EntityInstanceId == 0)
        {
            disposition = LocomotionPresentationRequestDisposition243.InvalidObservation;
            return false;
        }

        if (!TryResolveIdentity(
                snapshot.ResolvedState,
                snapshot.ContinuousClip != null ? snapshot.ContinuousClip.name : string.Empty,
                out var semantic,
                out var fallbackClipKey,
                out var graphNodePath,
                out var loopPolicy))
        {
            disposition = LocomotionPresentationRequestDisposition243.UnsupportedState;
            return false;
        }

        var clipKey = snapshot.ContinuousClip != null ? snapshot.ContinuousClip.name : fallbackClipKey;
        var idempotencyKey = ComposeIdempotencyKey(
            observation.EntityInstanceId, snapshot.ResolvedState, clipKey, loopPolicy);
        var requestId = ComposeRequestId(observation.EntityInstanceId, observation.ObservationSequence);
        var legacyRequest = BuildRequest(
            requestId, idempotencyKey, in observation, in snapshot,
            semantic, clipKey, loopPolicy, AnimationRequestSourceKind.Observation);
        var graphRequest = BuildRequest(
            requestId, idempotencyKey, in observation, in snapshot,
            semantic, clipKey, loopPolicy, AnimationRequestSourceKind.Graph);
        pair = new LocomotionPresentationRequestPair243(
            in legacyRequest, in graphRequest, snapshot.ResolvedState, graphNodePath);
        disposition = LocomotionPresentationRequestDisposition243.Produced;
        return true;
    }

    static AnimationPlayRequest BuildRequest(
        ulong requestId,
        ulong idempotencyKey,
        in AnimationObservation observation,
        in LocomotionPresentationSnapshot snapshot,
        string semantic,
        string clipKey,
        AnimationLoopPolicy loopPolicy,
        AnimationRequestSourceKind sourceKind) =>
        new AnimationPlayRequest(
            requestId,
            observation.EntityInstanceId,
            observation.GameplayTick,
            observation.ObservationSequence,
            AnimationRequestDomain.Locomotion,
            semantic,
            clipKey,
            snapshot.ContinuousClip,
            loopPolicy,
            snapshot.ClipSpeed > 0.001f ? snapshot.ClipSpeed : 1f,
            0f,
            AnimationRequestPriority.Normal,
            AnimationInterruptPolicy.Interruptible,
            idempotencyKey,
            "locomotion",
            sourceKind,
            observation.ActionLeaseVersion,
            observation.AirCycleId,
            0UL);

    static ulong ComposeRequestId(int entityInstanceId, ulong observationSequence) =>
        ((ulong)(uint)entityInstanceId << 32) | (uint)observationSequence;

    static ulong ComposeIdempotencyKey(
        int entityInstanceId,
        LocomotionStateId state,
        string clipKey,
        AnimationLoopPolicy loopPolicy)
    {
        var hash = 14695981039346656037UL;
        hash = Mix(hash, (uint)entityInstanceId);
        hash = Mix(hash, (byte)state);
        hash = Mix(hash, (byte)loopPolicy);
        if (!string.IsNullOrEmpty(clipKey))
        {
            for (var i = 0; i < clipKey.Length; i++)
            {
                hash = Mix(hash, clipKey[i]);
            }
        }
        return hash;
    }

    static ulong Mix(ulong hash, uint value) => (hash ^ value) * 1099511628211UL;

    static bool TryResolveIdentity(
        LocomotionStateId state,
        string resolvedClipName,
        out string semantic,
        out string fallbackClipKey,
        out string graphNodePath,
        out AnimationLoopPolicy loopPolicy)
    {
        semantic = string.Empty;
        fallbackClipKey = string.Empty;
        graphNodePath = string.Empty;
        loopPolicy = AnimationLoopPolicy.UseClipDefault;
        switch (state)
        {
            case LocomotionStateId.Idle:
                semantic = "locomotion.idle.loop";
                fallbackClipKey = "Locomotion_Idle";
                graphNodePath = "locomotion/idle/loop";
                loopPolicy = AnimationLoopPolicy.Loop;
                return true;
            case LocomotionStateId.Walk:
                semantic = "locomotion.walk.loop";
                fallbackClipKey = "Locomotion_Walk";
                graphNodePath = "locomotion/walk/loop";
                loopPolicy = AnimationLoopPolicy.Loop;
                return true;
            case LocomotionStateId.Run:
                semantic = "locomotion.run.loop";
                fallbackClipKey = "Locomotion_Run";
                graphNodePath = "locomotion/run/loop";
                loopPolicy = AnimationLoopPolicy.Loop;
                return true;
            case LocomotionStateId.StrafeLocomotion:
                if (string.IsNullOrEmpty(resolvedClipName))
                {
                    return false;
                }
                semantic = "locomotion.strafe.loop";
                fallbackClipKey = string.Empty;
                graphNodePath = "locomotion/strafe/loop";
                loopPolicy = AnimationLoopPolicy.Loop;
                return true;
            case LocomotionStateId.WalkStart:
                semantic = "locomotion.walk.start";
                graphNodePath = "locomotion/walk/start";
                loopPolicy = AnimationLoopPolicy.Finite;
                return true;
            case LocomotionStateId.RunStart:
                semantic = "locomotion.run.start";
                graphNodePath = "locomotion/run/start";
                loopPolicy = AnimationLoopPolicy.Finite;
                return true;
            case LocomotionStateId.WalkEnd:
                semantic = "locomotion.walk.stop";
                graphNodePath = "locomotion/walk/stop";
                loopPolicy = AnimationLoopPolicy.Finite;
                return true;
            case LocomotionStateId.RunEnd:
                semantic = "locomotion.run.stop";
                graphNodePath = "locomotion/run/stop";
                loopPolicy = AnimationLoopPolicy.Finite;
                return true;
            default:
                return false;
        }
    }
}

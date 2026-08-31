using System.Collections.Generic;

/// <summary>243.6 — Deterministic, pure Presentation request arbitration.</summary>
public static class AnimationRequestArbiter
{
    public static void Evaluate(
        in AnimationArbitrationState current,
        in AnimationPlayRequest candidate,
        in AnimationObservation observation,
        out AnimationArbitrationDecision decision)
    {
        if (candidate.EntityInstanceId == 0
            || (current.HasAccepted && current.EntityInstanceId != candidate.EntityInstanceId)
            || (observation.IsKnown(AnimationObservationKnownMask.Entity)
                && observation.EntityInstanceId != candidate.EntityInstanceId))
        {
            decision = Reject(current, AnimationArbitrationReason.InvalidEntity);
            return;
        }

        if (!observation.IsSchemaSupported)
        {
            decision = Reject(current, AnimationArbitrationReason.UnsupportedObservationSchema);
            return;
        }

        if (current.HasAccepted && IsOlderThanCurrent(in current, in candidate))
        {
            decision = Reject(current, AnimationArbitrationReason.StaleSource);
            return;
        }

        if (observation.IsKnown(AnimationObservationKnownMask.ActionLease)
            && candidate.ActionLeaseVersion != 0U
            && candidate.ActionLeaseVersion < observation.ActionLeaseVersion)
        {
            decision = Reject(current, AnimationArbitrationReason.StaleLease);
            return;
        }

        if (observation.IsKnown(AnimationObservationKnownMask.AirCycle)
            && candidate.AirCycleId != 0UL
            && candidate.AirCycleId < observation.AirCycleId)
        {
            decision = Reject(current, AnimationArbitrationReason.StaleAirCycle);
            return;
        }

        if (current.HasAccepted
            && candidate.IdempotencyKey != 0UL
            && candidate.IdempotencyKey == current.LastIdempotencyKey
            && !candidate.ExplicitRestart)
        {
            decision = Suppress(current, AnimationArbitrationReason.DuplicateIdempotency);
            return;
        }

        if (current.HasAccepted
            && candidate.Domain == AnimationRequestDomain.Turn
            && candidate.Generation != 0UL
            && candidate.Generation == current.LastTurnGeneration)
        {
            decision = Suppress(current, AnimationArbitrationReason.TurnGenerationAlreadyAccepted);
            return;
        }

        if (current.HasAccepted && current.LastInterruptPolicy == AnimationInterruptPolicy.NonInterruptible
            && candidate.InterruptPolicy != AnimationInterruptPolicy.Force
            && candidate.Priority < AnimationRequestPriority.Critical)
        {
            decision = Reject(current, AnimationArbitrationReason.NonInterruptible);
            return;
        }

        if (current.HasAccepted && candidate.Priority < current.LastPriority)
        {
            decision = Reject(current, AnimationArbitrationReason.LowerPriority);
            return;
        }

        if (current.HasAccepted && candidate.Priority == current.LastPriority
            && !IsCandidatePreferred(in candidate, in current))
        {
            decision = Reject(current, AnimationArbitrationReason.StableTieBreaker);
            return;
        }

        var next = current.Accept(in candidate);
        if (!candidate.HasClipIdentity)
        {
            decision = new AnimationArbitrationDecision(
                AnimationArbitrationDecisionKind.Fallback,
                AnimationArbitrationReason.MissingClip,
                in next);
            return;
        }

        decision = new AnimationArbitrationDecision(
            current.HasAccepted ? AnimationArbitrationDecisionKind.Superseded : AnimationArbitrationDecisionKind.Accepted,
            AnimationArbitrationReason.None,
            in next);
    }

    /// <summary>Order-independent helper for a candidate batch. The winner is selected before state mutation.</summary>
    public static bool TrySelectBest(
        in AnimationArbitrationState current,
        IList<AnimationPlayRequest> candidates,
        in AnimationObservation observation,
        out AnimationPlayRequest selected,
        out AnimationArbitrationDecision decision)
    {
        selected = default;
        decision = default;
        if (candidates == null || candidates.Count == 0)
        {
            return false;
        }

        var hasSelection = false;
        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            Evaluate(in current, in candidate, in observation, out var candidateDecision);
            if (!candidateDecision.IsAccepted)
            {
                continue;
            }

            if (!hasSelection || IsBetter(in candidate, in selected))
            {
                selected = candidate;
                decision = candidateDecision;
                hasSelection = true;
            }
        }

        return hasSelection;
    }

    static bool IsOlderThanCurrent(in AnimationArbitrationState current, in AnimationPlayRequest candidate)
    {
        if (candidate.SourceTick != current.LastSourceTick)
        {
            return candidate.SourceTick < current.LastSourceTick;
        }

        return candidate.SourceSequence < current.LastSourceSequence;
    }

    static bool IsCandidatePreferred(in AnimationPlayRequest candidate, in AnimationArbitrationState current)
    {
        if (candidate.SourceTick != current.LastSourceTick)
        {
            return candidate.SourceTick > current.LastSourceTick;
        }
        if (candidate.SourceSequence != current.LastSourceSequence)
        {
            return candidate.SourceSequence > current.LastSourceSequence;
        }
        return candidate.RequestId > current.LastRequestId;
    }

    static bool IsBetter(in AnimationPlayRequest candidate, in AnimationPlayRequest incumbent)
    {
        if (candidate.Priority != incumbent.Priority)
        {
            return candidate.Priority > incumbent.Priority;
        }
        if (candidate.SourceTick != incumbent.SourceTick)
        {
            return candidate.SourceTick > incumbent.SourceTick;
        }
        if (candidate.SourceSequence != incumbent.SourceSequence)
        {
            return candidate.SourceSequence > incumbent.SourceSequence;
        }
        return candidate.RequestId > incumbent.RequestId;
    }

    static AnimationArbitrationDecision Reject(
        in AnimationArbitrationState current,
        AnimationArbitrationReason reason) =>
        new AnimationArbitrationDecision(AnimationArbitrationDecisionKind.Rejected, reason, in current);

    static AnimationArbitrationDecision Suppress(
        in AnimationArbitrationState current,
        AnimationArbitrationReason reason) =>
        new AnimationArbitrationDecision(AnimationArbitrationDecisionKind.Suppressed, reason, in current);
}

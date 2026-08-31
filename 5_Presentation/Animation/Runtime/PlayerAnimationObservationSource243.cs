using UnityEngine;

/// <summary>
/// 243.9 L1 — Read-only Player-to-Presentation adapter. It captures existing Gameplay facts only and
/// deliberately leaves unavailable stance/weapon facts unknown instead of manufacturing defaults.
/// </summary>
public sealed class PlayerAnimationObservationSource243 : IAnimationObservationSource
{
    readonly Player player;
    ulong sequence;

    public PlayerAnimationObservationSource243(Player player)
    {
        this.player = player;
    }

    public bool TryCapture(out AnimationObservation observation)
    {
        observation = default;
        if (player == null) return false;

        var known = AnimationObservationKnownMask.Entity
                    | AnimationObservationKnownMask.ActionLease
                    | AnimationObservationKnownMask.Grounded;
        var states = player.States;
        var gameplayState = states != null && states.Current != null
            ? states.Current.StateId
            : string.Empty;
        if (!string.IsNullOrEmpty(gameplayState)) known |= AnimationObservationKnownMask.GameplayState;

        var locomotion = player.LocomotionPresentation.ResolvedState;
        var locomotionState = locomotion != LocomotionStateId.None ? locomotion.ToString() : string.Empty;
        if (!string.IsNullOrEmpty(locomotionState)) known |= AnimationObservationKnownMask.LocomotionState;

        var airCycle = player.CurrentAirCycle;
        if (airCycle.IsKnown) known |= AnimationObservationKnownMask.AirCycle;

        var verticalSpeed = player.VerticalSpeed;
        if (IsFinite(verticalSpeed)) known |= AnimationObservationKnownMask.VerticalSpeed;
        else verticalSpeed = 0f;

        var intent = player.MovementIntent;
        var movementIntent = new Vector2(intent.x, intent.z);
        if (IsFinite(movementIntent)) known |= AnimationObservationKnownMask.MovementIntent;
        else movementIntent = Vector2.zero;

        var planarVelocity = player.PlanarVelocity;
        if (IsFinite(planarVelocity)) known |= AnimationObservationKnownMask.PlanarVelocity;
        else planarVelocity = Vector3.zero;

        var logicForward = player.LogicForward;
        if (IsFinite(logicForward)) known |= AnimationObservationKnownMask.LogicForward;
        else logicForward = Vector3.zero;

        var presentationFacing = player.PresentationFacing;
        if (IsFinite(presentationFacing)) known |= AnimationObservationKnownMask.PresentationFacing;
        else presentationFacing = Vector3.zero;

        observation = new AnimationObservation(
            player.GetInstanceID(),
            states != null ? states.CurrentLogicStepId : 0UL,
            ++sequence,
            gameplayState,
            locomotionState,
            player.ActiveActionLeaseVersion,
            airCycle.AirCycleId,
            player.IsGrounded,
            verticalSpeed,
            movementIntent,
            planarVelocity,
            logicForward,
            presentationFacing,
            string.Empty,
            string.Empty,
            known);
        return true;
    }

    static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
    static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
}

using System.Collections.Generic;

public readonly struct AnimationPipelineGateState243
{
    public readonly AnimationPipelineMode Mode;
    public readonly int SchemaVersion;
    public readonly string GraphHash;
    public readonly string Reason;

    public AnimationPipelineGateState243(AnimationPipelineMode mode, int schemaVersion, string graphHash, string reason)
    {
        Mode = mode;
        SchemaVersion = schemaVersion;
        GraphHash = graphHash ?? string.Empty;
        Reason = reason ?? string.Empty;
    }
}

/// <summary>Presentation-only migration gate. It selects no executor and cannot create a second writer.</summary>
public sealed class AnimationPipelineGate243
{
    readonly Dictionary<AnimationRequestDomain, AnimationPipelineGateState243> _states =
        new Dictionary<AnimationRequestDomain, AnimationPipelineGateState243>();

    readonly AnimationPipelineMode _globalDefault;

    public AnimationPipelineGate243(AnimationPipelineMode globalDefault = AnimationPipelineMode.Disabled)
    {
        _globalDefault = globalDefault;
    }

    public AnimationPipelineGateState243 GetState(AnimationRequestDomain domain)
    {
        if (_states.TryGetValue(domain, out var state))
        {
            return state;
        }
        return new AnimationPipelineGateState243(AnimationPipelineMode.DomainDefault, 0, string.Empty, "domain-default");
    }

    public AnimationPipelineMode ResolveMode(AnimationRequestDomain domain)
    {
        var state = GetState(domain);
        return state.Mode == AnimationPipelineMode.DomainDefault || state.Mode == AnimationPipelineMode.GlobalDefault
            ? _globalDefault
            : state.Mode;
    }

    public bool TrySetMode(
        AnimationRequestDomain domain,
        AnimationPipelineMode nextMode,
        string reason,
        int schemaVersion,
        string graphHash,
        bool shadowDiffClear,
        bool singlePresentationWriter)
    {
        if (domain == AnimationRequestDomain.Unknown || schemaVersion != AnimationObservation.CurrentSchemaVersion)
        {
            return false;
        }

        if ((nextMode == AnimationPipelineMode.Shadow || nextMode == AnimationPipelineMode.Canary)
            && string.IsNullOrEmpty(graphHash))
        {
            return false;
        }

        var current = GetState(domain);
        if (nextMode == AnimationPipelineMode.Canary)
        {
            if (current.Mode != AnimationPipelineMode.Shadow || !shadowDiffClear || !singlePresentationWriter)
            {
                return false;
            }
        }
        else if (nextMode == AnimationPipelineMode.Shadow && current.Mode == AnimationPipelineMode.Canary)
        {
            // Canary may return to Shadow for diagnosis; no writer is selected by this data object.
        }
        else if (nextMode != AnimationPipelineMode.Disabled
            && nextMode != AnimationPipelineMode.DomainDefault
            && nextMode != AnimationPipelineMode.GlobalDefault
            && current.Mode != AnimationPipelineMode.Disabled)
        {
            return false;
        }

        _states[domain] = new AnimationPipelineGateState243(nextMode, schemaVersion, graphHash, reason);
        return true;
    }

    public void Disable(AnimationRequestDomain domain, string reason)
    {
        _states[domain] = new AnimationPipelineGateState243(
            AnimationPipelineMode.Disabled,
            AnimationObservation.CurrentSchemaVersion,
            string.Empty,
            reason);
    }
}

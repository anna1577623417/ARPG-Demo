using System;
using System.Collections.Generic;

/// <summary>Finite, read-only status exposed to the 243.8 PlayMode overlay. It is not an authoring or Gameplay state.</summary>
public readonly struct AnimationTransitionCanaryStatus243
{
    public readonly AnimationRequestDomain Domain;
    public readonly int EntityInstanceId;
    public readonly AnimationPipelineMode EffectiveMode;
    public readonly bool CanEvaluateShadow;
    public readonly bool CanSubmitPlan;
    public readonly int SchemaVersion;
    public readonly string GraphHash;
    public readonly string Reason;

    public AnimationTransitionCanaryStatus243(
        AnimationRequestDomain domain,
        int entityInstanceId,
        AnimationPipelineMode effectiveMode,
        bool canEvaluateShadow,
        bool canSubmitPlan,
        int schemaVersion,
        string graphHash,
        string reason)
    {
        Domain = domain;
        EntityInstanceId = entityInstanceId;
        EffectiveMode = effectiveMode;
        CanEvaluateShadow = canEvaluateShadow;
        CanSubmitPlan = canSubmitPlan;
        SchemaVersion = schemaVersion;
        GraphHash = graphHash ?? string.Empty;
        Reason = reason ?? string.Empty;
    }
}

/// <summary>Process-local P3 projection. Producers publish a bounded last-known fact; the Editor only reads it.</summary>
public static class AnimationTransitionCanaryStatusRegistry243
{
    static readonly Dictionary<AnimationRequestDomain, AnimationTransitionCanaryStatus243> States =
        new Dictionary<AnimationRequestDomain, AnimationTransitionCanaryStatus243>();

    public static void Publish(in AnimationTransitionCanaryStatus243 status)
    {
        if (status.Domain != AnimationRequestDomain.Unknown)
        {
            States[status.Domain] = status;
        }
    }

    public static bool TryGet(AnimationRequestDomain domain, out AnimationTransitionCanaryStatus243 status) =>
        States.TryGetValue(domain, out status);

    public static void Clear() => States.Clear();
}

/// <summary>
/// Migration-only gate coordinator. It is the sole place that can elevate a domain/actor into Shadow or Canary,
/// validate compiled provenance, emit bounded diagnostics, and publish P3 status. It never selects or writes playback.
/// </summary>
public sealed class AnimationTransitionCanaryCoordinator243
{
    readonly struct ActorDomainKey : IEquatable<ActorDomainKey>
    {
        public readonly int EntityInstanceId;
        public readonly AnimationRequestDomain Domain;

        public ActorDomainKey(int entityInstanceId, AnimationRequestDomain domain)
        {
            EntityInstanceId = entityInstanceId;
            Domain = domain;
        }

        public bool Equals(ActorDomainKey other) => EntityInstanceId == other.EntityInstanceId && Domain == other.Domain;
        public override bool Equals(object obj) => obj is ActorDomainKey other && Equals(other);
        public override int GetHashCode() => (EntityInstanceId * 397) ^ (int)Domain;
    }

    readonly AnimationPipelineGate243 gate;
    readonly CompiledAnimTransitionGraphReader reader;
    readonly AnimationTransitionTraceLimiter243 limiter;
    readonly Dictionary<ActorDomainKey, AnimationPipelineGateState243> actorStates =
        new Dictionary<ActorDomainKey, AnimationPipelineGateState243>();

    public AnimationTransitionCanaryCoordinator243(
        AnimationPipelineGate243 pipelineGate,
        CompiledAnimTransitionGraphReader compiledReader,
        int traceCapacity = 32)
    {
        gate = pipelineGate ?? throw new ArgumentNullException(nameof(pipelineGate));
        reader = compiledReader;
        limiter = new AnimationTransitionTraceLimiter243(traceCapacity);
    }

    public bool TrySetDomainMode(
        AnimationRequestDomain domain,
        AnimationPipelineMode nextMode,
        string reason,
        bool shadowDiffClear,
        bool singlePresentationWriter)
    {
        if (nextMode == AnimationPipelineMode.Disabled)
        {
            if (domain == AnimationRequestDomain.Unknown)
            {
                return false;
            }

            gate.Disable(domain, reason);
            return true;
        }

        if (!TryGetCompiledProvenance(out var schemaVersion, out var graphHash))
        {
            return false;
        }

        return gate.TrySetMode(
            domain, nextMode, reason, schemaVersion, graphHash, shadowDiffClear, singlePresentationWriter);
    }

    public bool TrySetActorMode(
        int entityInstanceId,
        AnimationRequestDomain domain,
        AnimationPipelineMode nextMode,
        string reason)
    {
        if (entityInstanceId == 0 || domain == AnimationRequestDomain.Unknown || !IsExplicitMode(nextMode))
        {
            return false;
        }

        if (nextMode == AnimationPipelineMode.Disabled)
        {
            actorStates[new ActorDomainKey(entityInstanceId, domain)] = new AnimationPipelineGateState243(
                AnimationPipelineMode.Disabled, AnimationObservation.CurrentSchemaVersion, string.Empty, reason);
            return true;
        }

        if (!TryGetCompiledProvenance(out var schemaVersion, out var graphHash)
            || ModeRank(nextMode) > ModeRank(gate.ResolveMode(domain)))
        {
            return false;
        }

        actorStates[new ActorDomainKey(entityInstanceId, domain)] = new AnimationPipelineGateState243(
            nextMode, schemaVersion, graphHash, reason);
        return true;
    }

    public void DisableActor(int entityInstanceId, AnimationRequestDomain domain, string reason)
    {
        TrySetActorMode(entityInstanceId, domain, AnimationPipelineMode.Disabled, reason);
    }

    public bool ClearActorOverride(int entityInstanceId, AnimationRequestDomain domain) =>
        actorStates.Remove(new ActorDomainKey(entityInstanceId, domain));

    public AnimationTransitionCanaryStatus243 Observe(
        in RuntimeStepStamp step,
        int entityInstanceId,
        AnimationRequestDomain domain,
        ulong requestId = 0UL,
        ulong generation = 0UL)
    {
        var status = Resolve(entityInstanceId, domain);
        AnimationTransitionCanaryStatusRegistry243.Publish(in status);

        var kind = status.CanEvaluateShadow
            ? AnimationTransitionTraceEventKind.Observe
            : AnimationTransitionTraceEventKind.Fallback;
        var traceEvent = new AnimationTransitionTraceEvent243(
            kind, in step, entityInstanceId, requestId, generation, domain, status.Reason);
        AnimationTransitionGraphTrace243.TryLog(in traceEvent, limiter);
        return status;
    }

    public AnimationTransitionCanaryStatus243 Resolve(int entityInstanceId, AnimationRequestDomain domain)
    {
        var domainState = gate.GetState(domain);
        var effectiveMode = gate.ResolveMode(domain);
        var expectedSchema = domainState.SchemaVersion;
        var expectedHash = domainState.GraphHash;
        var reason = domainState.Reason;

        if (actorStates.TryGetValue(new ActorDomainKey(entityInstanceId, domain), out var actorState))
        {
            effectiveMode = actorState.Mode;
            expectedSchema = actorState.SchemaVersion;
            expectedHash = actorState.GraphHash;
            reason = actorState.Reason;
        }

        if (domain == AnimationRequestDomain.Unknown || effectiveMode == AnimationPipelineMode.Disabled)
        {
            return Status(domain, entityInstanceId, AnimationPipelineMode.Disabled, false, false, expectedSchema, expectedHash, reason);
        }

        if (reader == null || !reader.IsAvailable)
        {
            return Status(domain, entityInstanceId, AnimationPipelineMode.Disabled, false, false, 0, string.Empty, "compiled-unavailable");
        }

        var actualSchema = reader.SchemaVersion;
        var actualHash = reader.GraphHash;
        if (actualSchema != AnimationObservation.CurrentSchemaVersion || string.IsNullOrEmpty(actualHash))
        {
            return Status(domain, entityInstanceId, AnimationPipelineMode.Disabled, false, false,
                actualSchema, actualHash, "compiled-schema-mismatch");
        }

        if (expectedSchema != actualSchema)
        {
            return Status(domain, entityInstanceId, AnimationPipelineMode.Disabled, false, false, actualSchema, actualHash, "compiled-schema-mismatch");
        }

        if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
        {
            return Status(domain, entityInstanceId, AnimationPipelineMode.Disabled, false, false, actualSchema, actualHash, "compiled-hash-mismatch");
        }

        var shadow = effectiveMode == AnimationPipelineMode.Shadow || effectiveMode == AnimationPipelineMode.Canary;
        return Status(domain, entityInstanceId, effectiveMode, shadow, effectiveMode == AnimationPipelineMode.Canary,
            actualSchema, actualHash, reason);
    }

    bool TryGetCompiledProvenance(out int schemaVersion, out string graphHash)
    {
        schemaVersion = reader != null ? reader.SchemaVersion : 0;
        graphHash = reader != null ? reader.GraphHash : string.Empty;
        return reader != null
            && reader.IsAvailable
            && schemaVersion == AnimationObservation.CurrentSchemaVersion
            && !string.IsNullOrEmpty(graphHash);
    }

    static bool IsExplicitMode(AnimationPipelineMode mode) =>
        mode == AnimationPipelineMode.Disabled || mode == AnimationPipelineMode.Shadow || mode == AnimationPipelineMode.Canary;

    static int ModeRank(AnimationPipelineMode mode) => mode == AnimationPipelineMode.Canary ? 2
        : mode == AnimationPipelineMode.Shadow ? 1
        : 0;

    static AnimationTransitionCanaryStatus243 Status(
        AnimationRequestDomain domain,
        int entityInstanceId,
        AnimationPipelineMode effectiveMode,
        bool canEvaluateShadow,
        bool canSubmitPlan,
        int schemaVersion,
        string graphHash,
        string reason) =>
        new AnimationTransitionCanaryStatus243(
            domain, entityInstanceId, effectiveMode, canEvaluateShadow, canSubmitPlan, schemaVersion, graphHash, reason);
}

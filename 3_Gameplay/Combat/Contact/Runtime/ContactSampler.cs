using UnityEngine;

public interface IContactSampler
{
    void Reset();

    void Sample(
        in ResolvedContactSpec spec,
        Entity source,
        Vector3 worldPosition,
        Quaternion worldRotation,
        ContactCandidateBuffer output);
}

/// <summary>
/// Volume Query/Sweep。不再内部冻结 Static；冻结 Pose 由 ContactRuntimeState / PoseResolver 提供。
/// </summary>
public sealed class VolumeContactSampler : IContactSampler
{
    readonly Collider[] _overlaps;
    readonly RaycastHit[] _sweeps;

    public bool PhysicsBufferSaturated { get; private set; }

    public VolumeContactSampler(int physicsCapacity = 64)
    {
        var capacity = Mathf.Max(8, physicsCapacity);
        _overlaps = new Collider[capacity];
        _sweeps = new RaycastHit[capacity];
    }

    public void Reset()
    {
        PhysicsBufferSaturated = false;
    }

    public void Sample(
        in ResolvedContactSpec spec,
        Entity source,
        Vector3 worldPosition,
        Quaternion worldRotation,
        ContactCandidateBuffer output)
    {
        var current = new ResolvedContactPose(
            worldPosition,
            worldRotation,
            isFrozen: false,
            sourceNormalizedTime: 0f,
            ContactAnchorPose.Identity);
        Sample(in spec, source, in current, in current, output);
    }

    public void Sample(
        in ResolvedContactSpec spec,
        Entity source,
        in ResolvedContactPose currentPose,
        in ResolvedContactPose previousPose,
        ContactCandidateBuffer output)
    {
        var geometry = spec.Geometry;
        if (geometry == null || output == null)
        {
            return;
        }

        var queryPosition = currentPose.Position;
        var queryRotation = currentPose.Rotation;

        var overlapCount = geometry.Overlap(
            queryPosition,
            queryRotation,
            _overlaps,
            spec.Query.LayerMask.value,
            spec.Query.TriggerInteraction);
        PhysicsBufferSaturated |= overlapCount >= _overlaps.Length;

        for (var i = 0; i < overlapCount; i++)
        {
            var collider = _overlaps[i];
            if (collider == null)
            {
                continue;
            }

            var point = collider.ClosestPoint(queryPosition);
            var towardOrigin = queryPosition - point;
            output.TryAdd(
                collider,
                source,
                in spec.Query,
                point,
                towardOrigin.sqrMagnitude > 1e-6f ? towardOrigin.normalized : Vector3.up);
        }

        var allowSweep = spec.SweepPolicy == ContactSweepPolicy.BetweenSamples
            || (spec.UsesLegacyAuthoring && spec.Motion == ContactMotionKind.SweepBetweenFrames);
        if (allowSweep
            && (previousPose.Position - currentPose.Position).sqrMagnitude > 1e-8f)
        {
            var sweepCount = geometry.Sweep(
                previousPose.Position,
                currentPose.Position,
                currentPose.Rotation,
                _sweeps,
                spec.Query.LayerMask.value,
                spec.Query.TriggerInteraction);
            PhysicsBufferSaturated |= sweepCount >= _sweeps.Length;

            for (var i = 0; i < sweepCount; i++)
            {
                ref var hit = ref _sweeps[i];
                output.TryAdd(
                    hit.collider,
                    source,
                    in spec.Query,
                    hit.point,
                    hit.normal);
            }
        }
    }
}

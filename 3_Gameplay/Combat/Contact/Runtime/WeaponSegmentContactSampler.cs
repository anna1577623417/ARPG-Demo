using UnityEngine;

/// <summary>
/// 烘焙 Socket Layout 的相邻帧段采样。当前段用 Capsule 粗筛，各 Socket 用 SphereCast 防高速穿透。
/// </summary>
public sealed class WeaponSegmentContactSampler : IContactSampler
{
    const int MaxSockets = 16;

    readonly Vector3[] _current = new Vector3[MaxSockets];
    readonly Vector3[] _previous = new Vector3[MaxSockets];
    readonly float[] _radii = new float[MaxSockets];
    readonly bool[] _valid = new bool[MaxSockets];
    readonly bool[] _previousValid = new bool[MaxSockets];
    readonly Collider[] _overlaps;
    readonly RaycastHit[] _sweeps;
    readonly WeaponTraceProvider.SocketSample[] _traceSamples =
        new WeaponTraceProvider.SocketSample[MaxSockets];

    bool _hasPrevious;

    public int SampleCount { get; private set; }
    public bool PhysicsBufferSaturated { get; private set; }
    public WeaponTraceProvider.SocketSample[] TraceSamples => _traceSamples;

    public WeaponSegmentContactSampler(int physicsCapacity = 64)
    {
        var capacity = Mathf.Max(8, physicsCapacity);
        _overlaps = new Collider[capacity];
        _sweeps = new RaycastHit[capacity];
    }

    public void Reset()
    {
        _hasPrevious = false;
        SampleCount = 0;
        PhysicsBufferSaturated = false;
    }

    public void Sample(
        in ResolvedContactSpec spec,
        Entity source,
        Vector3 worldPosition,
        Quaternion worldRotation,
        ContactCandidateBuffer output)
    {
        var layout = spec.WeaponSocketLayout;
        var bindings = layout != null ? layout.Bindings : null;
        if (source == null || bindings == null || bindings.Length == 0 || output == null)
        {
            SampleCount = 0;
            return;
        }

        SampleCount = Mathf.Min(bindings.Length, MaxSockets);
        for (var i = 0; i < SampleCount; i++)
        {
            ref var binding = ref bindings[i];
            var socket = ResolveSocket(source.transform, binding.TransformPath);
            var position = socket != null
                ? socket.position
                : source.transform.TransformPoint(binding.RootLocalPosition);
            var radius = Mathf.Max(0.001f, binding.Radius);
            _current[i] = position;
            _radii[i] = radius;
            _valid[i] = true;
            _traceSamples[i] = new WeaponTraceProvider.SocketSample(
                binding.Key,
                position,
                radius,
                true);
        }

        SampleCurrentSegments(source, in spec.Query, output);
        if (_hasPrevious
            && (spec.SweepPolicy == ContactSweepPolicy.BetweenSamples
                || (spec.UsesLegacyAuthoring && spec.Motion == ContactMotionKind.SweepBetweenFrames)))
        {
            SweepSockets(source, in spec.Query, output);
        }

        StorePrevious();
        _hasPrevious = true;
    }

    void SampleCurrentSegments(
        Entity source,
        in ContactQueryPolicy query,
        ContactCandidateBuffer output)
    {
        if (SampleCount == 1)
        {
            AddOverlapSphere(_current[0], _radii[0], source, in query, output);
            return;
        }

        for (var i = 1; i < SampleCount; i++)
        {
            var radius = Mathf.Max(_radii[i - 1], _radii[i]);
            var count = Physics.OverlapCapsuleNonAlloc(
                _current[i - 1],
                _current[i],
                radius,
                _overlaps,
                query.LayerMask.value,
                query.TriggerInteraction);
            PhysicsBufferSaturated |= count >= _overlaps.Length;
            for (var h = 0; h < count; h++)
            {
                var collider = _overlaps[h];
                if (collider == null)
                {
                    continue;
                }

                var center = (_current[i - 1] + _current[i]) * 0.5f;
                var point = collider.ClosestPoint(center);
                output.TryAdd(
                    collider,
                    source,
                    in query,
                    point,
                    center - point);
            }
        }
    }

    void SweepSockets(
        Entity source,
        in ContactQueryPolicy query,
        ContactCandidateBuffer output)
    {
        for (var i = 0; i < SampleCount; i++)
        {
            if (!_valid[i] || !_previousValid[i])
            {
                continue;
            }

            var delta = _current[i] - _previous[i];
            var distance = delta.magnitude;
            if (distance < 1e-4f)
            {
                continue;
            }

            var count = Physics.SphereCastNonAlloc(
                _previous[i],
                _radii[i],
                delta / distance,
                _sweeps,
                distance,
                query.LayerMask.value,
                query.TriggerInteraction);
            PhysicsBufferSaturated |= count >= _sweeps.Length;

            for (var h = 0; h < count; h++)
            {
                ref var hit = ref _sweeps[h];
                output.TryAdd(
                    hit.collider,
                    source,
                    in query,
                    hit.point,
                    hit.normal);
            }
        }
    }

    void AddOverlapSphere(
        Vector3 center,
        float radius,
        Entity source,
        in ContactQueryPolicy query,
        ContactCandidateBuffer output)
    {
        var count = Physics.OverlapSphereNonAlloc(
            center,
            radius,
            _overlaps,
            query.LayerMask.value,
            query.TriggerInteraction);
        PhysicsBufferSaturated |= count >= _overlaps.Length;
        for (var i = 0; i < count; i++)
        {
            var collider = _overlaps[i];
            if (collider == null)
            {
                continue;
            }

            var point = collider.ClosestPoint(center);
            output.TryAdd(collider, source, in query, point, center - point);
        }
    }

    void StorePrevious()
    {
        for (var i = 0; i < SampleCount; i++)
        {
            _previous[i] = _current[i];
            _previousValid[i] = _valid[i];
        }
    }

    static Transform ResolveSocket(Transform root, string path)
    {
        if (root == null || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return root.Find(path);
    }
}

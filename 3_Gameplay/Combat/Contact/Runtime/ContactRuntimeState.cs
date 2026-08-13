using UnityEngine;

/// <summary>一次 ContactEvent Active Window 的只读规格快照、采样历史与身份。</summary>
public sealed class ContactRuntimeState
{
    readonly ContactCandidateBuffer _candidates = new ContactCandidateBuffer(64);
    readonly VolumeContactSampler _volumeSampler = new VolumeContactSampler(64);
    readonly WeaponSegmentContactSampler _weaponSampler = new WeaponSegmentContactSampler(64);

    public ResolvedContactSpec Spec { get; private set; }
    public ActionDataSO Action { get; private set; }
    public string EventId { get; private set; }
    public string DebugName { get; private set; }
    public uint ActionLeaseVersion { get; private set; }
    public int SampleId { get; private set; }
    public bool Active { get; private set; }
    public bool HasFrozenPose { get; private set; }
    public ResolvedContactPose FrozenPose { get; private set; }
    public ResolvedContactPose PreviousPose { get; private set; }
    public bool HasPreviousPose { get; private set; }

    public ContactCandidateBuffer Candidates => _candidates;
    public WeaponSegmentContactSampler WeaponSampler => _weaponSampler;

    public void Begin(
        in ResolvedContactSpec spec,
        ActionDataSO action,
        string eventId,
        string debugName,
        uint actionLeaseVersion,
        in ResolvedContactPose beginPose)
    {
        Spec = spec;
        Action = action;
        EventId = eventId ?? string.Empty;
        DebugName = string.IsNullOrEmpty(debugName) ? EventId : debugName;
        ActionLeaseVersion = actionLeaseVersion;
        SampleId = 0;
        Active = true;
        PreviousPose = beginPose;
        HasPreviousPose = false;
        HasFrozenPose = beginPose.IsFrozen;
        FrozenPose = beginPose;
        _candidates.Clear();
        _volumeSampler.Reset();
        _weaponSampler.Reset();
    }

    public ContactCandidateBuffer Sample(
        Entity source,
        in ResolvedContactPose currentPose)
    {
        _candidates.Clear();
        SampleId++;
        var spec = Spec;
        var previous = HasPreviousPose ? PreviousPose : currentPose;

        if (spec.ShapeMode == HitShapeMode.WeaponTrace)
        {
            _weaponSampler.Sample(
                in spec,
                source,
                currentPose.Position,
                currentPose.Rotation,
                _candidates);
        }
        else
        {
            _volumeSampler.Sample(
                in spec,
                source,
                currentPose,
                previous,
                _candidates);
        }

        PreviousPose = currentPose;
        HasPreviousPose = true;
        return _candidates;
    }

    public bool IsSaturated =>
        _candidates.Saturated
        || (Spec.ShapeMode == HitShapeMode.WeaponTrace
            ? _weaponSampler.PhysicsBufferSaturated
            : _volumeSampler.PhysicsBufferSaturated);

    public string ActionNameForDiagnostics() =>
        Action != null ? Action.name : "(no-action)";

    public void End()
    {
        Active = false;
        HasFrozenPose = false;
        HasPreviousPose = false;
        _candidates.Clear();
        _volumeSampler.Reset();
        _weaponSampler.Reset();
    }
}

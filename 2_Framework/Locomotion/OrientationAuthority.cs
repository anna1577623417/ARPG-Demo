using UnityEngine;

/// <summary>237 L5 — 谁持有 Gameplay Facing Commit 租约。</summary>
public enum FacingLeaseOwner : byte
{
    None = 0,
    Locomotion = 1,
    Action = 2,
    HitReact = 3
}

/// <summary>一次朝向提交请求。只有 <see cref="OrientationAuthority.TryCommit"/> 能改 Committed。</summary>
public readonly struct FacingRequest
{
    public readonly FacingLeaseOwner Owner;
    public readonly ActionFacingPolicy Policy;
    public readonly Vector3 Desired;
    public readonly string Source;

    public FacingRequest(
        FacingLeaseOwner owner,
        Vector3 desired,
        string source,
        ActionFacingPolicy policy = ActionFacingPolicy.PreserveEntryFacing)
    {
        Owner = owner;
        Desired = desired;
        Source = source ?? string.Empty;
        Policy = policy;
    }
}

/// <summary>
/// 237 L5 — Gameplay CommittedFacing 唯一入口。Visual 只读 PresentationFacing。
/// Player 持有实例；禁止调用方绕过本类写 LogicForward。
/// </summary>
public sealed class OrientationAuthority
{
    FacingLeaseOwner _lease = FacingLeaseOwner.Locomotion;
    ActionFacingPolicy _actionPolicy = ActionFacingPolicy.PreserveEntryFacing;
    Vector3 _committed = Vector3.forward;
    Vector3 _presentation = Vector3.forward;
    Vector3 _entryFacing = Vector3.forward;
    bool _actionLeaseActive;

    public FacingLeaseOwner LeaseOwner =>
        _actionLeaseActive ? FacingLeaseOwner.Action : FacingLeaseOwner.Locomotion;

    public ActionFacingPolicy ActionPolicy => _actionPolicy;
    public Vector3 CommittedFacing => _committed;
    public Vector3 PresentationFacing => _presentation;
    public Vector3 EntryFacing => _entryFacing;
    public bool HasActionLease => _actionLeaseActive;

    public void BindInitial(Vector3 facing)
    {
        var planar = Planar(facing);
        _committed = planar;
        _presentation = planar;
        _entryFacing = planar;
        _actionLeaseActive = false;
        _lease = FacingLeaseOwner.Locomotion;
        _actionPolicy = ActionFacingPolicy.PreserveEntryFacing;
    }

    /// <summary>
    /// 进入 Action：执行 Resolver 给出的 EffectivePolicy。PreserveEntry 锁 Entry；FaceMotion 提交位移方向。
    /// TrackTarget 不得作为生效值传入；若仍传入则按 PreserveEntry（脸不跟 slot）。
    /// </summary>
    public bool TryBeginActionLease(
        ActionFacingPolicy effectivePolicy,
        Vector3 currentCommitted,
        Vector3 motionFacing,
        out Vector3 committed,
        out string denyReason)
    {
        if (_actionLeaseActive)
        {
            EndActionLease();
        }

        denyReason = null;
        var effective = effectivePolicy == ActionFacingPolicy.TrackTarget
            ? ActionFacingPolicy.PreserveEntryFacing
            : effectivePolicy;

        _actionPolicy = effective;
        _entryFacing = Planar(currentCommitted);
        _actionLeaseActive = true;
        _lease = FacingLeaseOwner.Action;

        if (effective == ActionFacingPolicy.FaceMotionAtEntry)
        {
            _committed = Planar(motionFacing);
        }
        else
        {
            _committed = _entryFacing;
        }

        _presentation = _committed;
        committed = _committed;
        return true;
    }

    public void EndActionLease()
    {
        _actionLeaseActive = false;
        _lease = FacingLeaseOwner.Locomotion;
        _actionPolicy = ActionFacingPolicy.PreserveEntryFacing;
        _presentation = _committed;
    }

    public bool TryCommit(in FacingRequest request, out Vector3 committed, out string denyReason)
    {
        committed = _committed;
        denyReason = null;
        var desired = Planar(request.Desired);

        if (request.Owner == FacingLeaseOwner.HitReact)
        {
            denyReason = "HitReactOpen";
            return false;
        }

        if (_actionLeaseActive)
        {
            if (request.Owner != FacingLeaseOwner.Action)
            {
                denyReason = "ActionLease";
                return false;
            }

            if (_actionPolicy == ActionFacingPolicy.PreserveEntryFacing
                && IsMotionSource(request.Source))
            {
                denyReason = "PreserveEntry";
                return false;
            }

            _committed = desired;
            _presentation = _committed;
            committed = _committed;
            return true;
        }

        _committed = desired;
        _presentation = _committed;
        committed = _committed;
        return true;
    }

    public static FacingLeaseOwner ClassifyOwner(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return FacingLeaseOwner.Locomotion;
        }

        if (source.IndexOf("PendingFacing", System.StringComparison.Ordinal) >= 0
            || source.IndexOf("ActionYaw", System.StringComparison.Ordinal) >= 0
            || source.IndexOf("ActionFacing", System.StringComparison.Ordinal) >= 0)
        {
            return FacingLeaseOwner.Action;
        }

        if (source.IndexOf("HitReact", System.StringComparison.Ordinal) >= 0)
        {
            return FacingLeaseOwner.HitReact;
        }

        return FacingLeaseOwner.Locomotion;
    }

    static bool IsMotionSource(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        return source.IndexOf("ActionYaw", System.StringComparison.Ordinal) >= 0
               || source.IndexOf("FromMotion", System.StringComparison.Ordinal) >= 0;
    }

    static Vector3 Planar(Vector3 dir)
    {
        dir.y = 0f;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
    }
}

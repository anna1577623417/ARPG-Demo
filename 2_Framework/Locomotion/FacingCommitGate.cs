using UnityEngine;

/// <summary>
/// 237 L2 — 暂缓 CommittedFacing。Desired 当帧可更新；Delay 从最近一次 Down Edge 起算。
/// 到期后由 Player 向 OrientationAuthority 申请 Locomotion Commit。禁止回退 ImmediateCommit。
/// 237.3 LA — 本闸只服务 Direction Edge（首次 Down）。持续 HoldRedirect 不走 Open / Expire。
/// </summary>
public sealed class FacingCommitGate
{
    public const float MinDelaySec = 0.02f;

    bool _pending;
    int _token;
    Vector3 _desired = Vector3.forward;
    float _openedUnscaled;
    float _delaySec = MinDelaySec;

    public bool IsPending => _pending;
    public int Token => _token;
    public Vector3 Desired => _desired;
    public float DelaySec => _delaySec;
    public float OpenedUnscaled => _openedUnscaled;

    public float AgeUnscaled =>
        _pending ? Mathf.Max(0f, InputClock.UnscaledNow - _openedUnscaled) : -1f;

    /// <summary>返回实际使用的 delay（已钳制）。requestedDelay 供调用方打 Warning。</summary>
    public float Open(int token, Vector3 desired, float requestedDelaySec, out bool delayClamped)
    {
        var delay = requestedDelaySec;
        delayClamped = delay < MinDelaySec;
        if (delayClamped)
        {
            delay = MinDelaySec;
        }

        _pending = true;
        _token = token;
        _desired = Planar(desired);
        _openedUnscaled = InputClock.UnscaledNow;
        _delaySec = delay;
        return delay;
    }

    public void RefreshDesired(Vector3 desired)
    {
        if (!_pending)
        {
            return;
        }

        _desired = Planar(desired);
    }

    public void Clear()
    {
        _pending = false;
        _token = 0;
        _desired = Vector3.forward;
        _openedUnscaled = 0f;
        _delaySec = MinDelaySec;
    }

    public bool TryExpire(out Vector3 commitDir, out int token)
    {
        commitDir = default;
        token = 0;
        if (!_pending)
        {
            return false;
        }

        if (AgeUnscaled + 0.0001f < _delaySec)
        {
            return false;
        }

        commitDir = _desired;
        token = _token;
        _pending = false;
        return true;
    }

    static Vector3 Planar(Vector3 dir)
    {
        dir.y = 0f;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
    }
}

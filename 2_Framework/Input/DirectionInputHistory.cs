using UnityEngine;

public enum DirectionInputPhase : byte
{
    Down = 0,
    Hold = 1,
    Up = 2
}

/// <summary>237 L1 — 一次方向边沿。Token 在 Down 递增，供后续 Claim / Event-Time 查询。</summary>
public readonly struct DirectionInputEdge
{
    public readonly int Token;
    public readonly Vector2 Raw;
    public readonly Vector3 WorldDir;
    public readonly Vector3 BasisFacing;
    public readonly float CameraYaw;
    public readonly float UnscaledTime;
    public readonly DirectionInputPhase Phase;

    public DirectionInputEdge(
        int token,
        Vector2 raw,
        Vector3 worldDir,
        Vector3 basisFacing,
        float cameraYaw,
        float unscaledTime,
        DirectionInputPhase phase)
    {
        Token = token;
        Raw = raw;
        WorldDir = worldDir;
        BasisFacing = basisFacing;
        CameraYaw = cameraYaw;
        UnscaledTime = unscaledTime;
        Phase = phase;
    }
}

/// <summary>
/// 237 L1 — 方向边沿环缓冲。Player 拥有唯一实例。Gameplay 禁止再 new 第二套。
/// </summary>
public sealed class DirectionInputHistory
{
    const int Capacity = 16;
    readonly DirectionInputEdge[] _ring = new DirectionInputEdge[Capacity];
    readonly DirectionTokenOwner[] _owners = new DirectionTokenOwner[Capacity];
    int _count;
    int _write;
    int _nextToken;

    public int LastToken { get; private set; }
    public int Count => _count;

    public int PushDown(Vector2 raw, Vector3 worldDir, Vector3 basisFacing, float cameraYaw) =>
        PushDown(raw, worldDir, basisFacing, cameraYaw, InputClock.UnscaledNow);

    public int PushDown(
        Vector2 raw,
        Vector3 worldDir,
        Vector3 basisFacing,
        float cameraYaw,
        float unscaledTime)
    {
        _nextToken++;
        LastToken = _nextToken;
        Write(new DirectionInputEdge(
            LastToken,
            raw,
            worldDir,
            basisFacing,
            cameraYaw,
            unscaledTime,
            DirectionInputPhase.Down));
        return LastToken;
    }

    public void Reset()
    {
        _count = 0;
        _write = 0;
        _nextToken = 0;
        LastToken = 0;
        for (var i = 0; i < Capacity; i++)
        {
            _ring[i] = default;
            _owners[i] = DirectionTokenOwner.None;
        }
    }

    public bool TryGetLatestDown(out DirectionInputEdge edge)
    {
        for (var i = _count - 1; i >= 0; i--)
        {
            var sample = At(i);
            if (sample.Phase == DirectionInputPhase.Down && sample.Token > 0)
            {
                edge = sample;
                return true;
            }
        }

        edge = default;
        return false;
    }

    public bool TryGetByToken(int token, out DirectionInputEdge edge)
    {
        if (!TryGetPhysicalIndex(token, out var physical))
        {
            edge = default;
            return false;
        }

        edge = _ring[physical];
        return true;
    }

    public bool TryGetOwner(int token, out DirectionTokenOwner owner)
    {
        owner = DirectionTokenOwner.None;
        if (!TryGetPhysicalIndex(token, out var physical))
        {
            return false;
        }

        owner = _owners[physical];
        return owner != DirectionTokenOwner.None;
    }

    /// <summary>
    /// 只 Claim 这一次 Edge Token。已被占用则拒绝，不覆盖。
    /// 新 Down 写入新槽且 owner=None，不继承上一次 Claim。
    /// </summary>
    public bool TryClaim(int token, DirectionTokenOwner owner, out DirectionTokenOwner existing)
    {
        existing = DirectionTokenOwner.None;
        if (token <= 0 || owner == DirectionTokenOwner.None)
        {
            return false;
        }

        if (!TryGetPhysicalIndex(token, out var physical))
        {
            return false;
        }

        existing = _owners[physical];
        if (existing != DirectionTokenOwner.None)
        {
            return false;
        }

        _owners[physical] = owner;
        return true;
    }

    DirectionInputEdge At(int logicalIndex)
    {
        var start = (_write - _count + Capacity) % Capacity;
        return _ring[(start + logicalIndex) % Capacity];
    }

    bool TryGetPhysicalIndex(int token, out int physical)
    {
        physical = 0;
        if (token <= 0 || _count <= 0)
        {
            return false;
        }

        var start = (_write - _count + Capacity) % Capacity;
        for (var i = 0; i < _count; i++)
        {
            var index = (start + i) % Capacity;
            if (_ring[index].Token == token)
            {
                physical = index;
                return true;
            }
        }

        return false;
    }

    void Write(in DirectionInputEdge edge)
    {
        _ring[_write] = edge;
        _owners[_write] = DirectionTokenOwner.None;
        _write = (_write + 1) % Capacity;
        if (_count < Capacity)
        {
            _count++;
        }
    }
}

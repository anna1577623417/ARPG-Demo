using System;
using System.Collections.Generic;

/// <summary>
/// 187.3 W2 — Motion Authority 仲裁器（Player 唯一持有）。
/// <para>规则：</para>
/// <para>  · TryGrant(channel, req)：req.Priority &lt;= 当前 Priority 时抢占成功（数字越小越优先）</para>
/// <para>  · Release(channel, expected)：仅当当前持有者匹配时回落到 Locomotion</para>
/// <para>  · AuthorityChanged 事件广播；外部订阅同步动画/HUD</para>
/// </summary>
public sealed class MotionAuthorityArbiter
{
    public const int DefaultLocomotionPriority = 100;

    AuthorityHolder _move = AuthorityHolder.Locomotion;
    AuthorityHolder _rotate = AuthorityHolder.Locomotion;
    int _movePriority = DefaultLocomotionPriority;
    int _rotatePriority = DefaultLocomotionPriority;

    readonly Queue<AuthorityChangedEvent> _recentChanges = new(8);
    const int MaxRecentChanges = 8;

    public AuthorityHolder MoveHolder => _move;
    public AuthorityHolder RotateHolder => _rotate;
    public int MovePriority => _movePriority;
    public int RotatePriority => _rotatePriority;

    /// <summary>最近 N 条切换记录（HUD / 调试用）。</summary>
    public IReadOnlyCollection<AuthorityChangedEvent> RecentChanges => _recentChanges;

    public event Action<AuthorityChangedEvent> AuthorityChanged;

    /// <summary>尝试抢占指定通道。返回 true=成功；false=被高优先级拒绝。</summary>
    public bool TryGrant(AuthorityChannel ch, in AuthorityRequest req, float now = 0f)
    {
        var (cur, curP) = Get(ch);

        // 数字大 = 优先级低，拒
        if (req.Priority > curP)
        {
            return false;
        }

        // 完全同源：免事件
        if (req.Priority == curP && req.Holder == cur)
        {
            return true;
        }

        Set(ch, req.Holder, req.Priority);
        BroadcastChange(ch, cur, req.Holder, curP, req.Priority, req.DebugReason, now);
        return true;
    }

    /// <summary>释放权威；仅当持有者匹配 expected 时才回落（防错抢被覆盖）。</summary>
    public bool Release(AuthorityChannel ch, AuthorityHolder expected, string reason, float now = 0f)
    {
        var (cur, curP) = Get(ch);
        if (cur != expected)
        {
            return false;
        }
        Set(ch, AuthorityHolder.Locomotion, DefaultLocomotionPriority);
        BroadcastChange(ch, expected, AuthorityHolder.Locomotion, curP, DefaultLocomotionPriority, reason, now);
        return true;
    }

    /// <summary>硬重置（场景切换 / 死亡复活）。</summary>
    public void HardReset()
    {
        _move = _rotate = AuthorityHolder.Locomotion;
        _movePriority = _rotatePriority = DefaultLocomotionPriority;
        _recentChanges.Clear();
    }

    /// <summary>查询是否能抢占（不实际抢占，仅 query；TryConsume 前预判用）。</summary>
    public bool CanPreempt(AuthorityChannel ch, int requestPriority)
    {
        var (_, curP) = Get(ch);
        return requestPriority <= curP;
    }

    /// <summary>查询当前持有者（一步查双通道）。</summary>
    public void Snapshot(out AuthorityHolder move, out AuthorityHolder rotate, out int moveP, out int rotateP)
    {
        move = _move;
        rotate = _rotate;
        moveP = _movePriority;
        rotateP = _rotatePriority;
    }

    // ─── 内部 ────────────────────────────────────────────────

    (AuthorityHolder, int) Get(AuthorityChannel ch)
        => ch == AuthorityChannel.Move ? (_move, _movePriority) : (_rotate, _rotatePriority);

    void Set(AuthorityChannel ch, AuthorityHolder h, int p)
    {
        if (ch == AuthorityChannel.Move)
        {
            _move = h;
            _movePriority = p;
        }
        else
        {
            _rotate = h;
            _rotatePriority = p;
        }
    }

    void BroadcastChange(
        AuthorityChannel ch,
        AuthorityHolder prev,
        AuthorityHolder cur,
        int prevP,
        int curP,
        string reason,
        float timeStamp)
    {
        var ev = new AuthorityChangedEvent
        {
            Channel = ch,
            Previous = prev,
            Current = cur,
            PreviousPriority = prevP,
            CurrentPriority = curP,
            Reason = reason,
            TimeStamp = timeStamp,
        };

        if (_recentChanges.Count >= MaxRecentChanges)
        {
            _recentChanges.Dequeue();
        }
        _recentChanges.Enqueue(ev);

        AuthorityChanged?.Invoke(ev);
    }
}

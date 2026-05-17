using System;
using UnityEngine;

/// <summary>
/// 副攻击 / 交互键边沿 — 与 PrimaryAttackPressTracker 同源策略。
/// 按下立即派发 Press；松开附带 holdSeconds 派发 Release。
/// </summary>
[Serializable]
public sealed class SecondaryInteractPressTracker
{
    const SkillEntrySlot Slot = SkillEntrySlot.RM;

    bool _lastHeld;
    bool _sessionOpen;
    float _pressStartedAt;

    public void SyncInitialHeldState(bool held)
    {
        _lastHeld = held;
        if (!held) _sessionOpen = false;
    }

    public void Tick(float time, bool held, Player player)
    {
        if (player?.InputSemantic == null) return;

        var rose = held && !_lastHeld;
        var fell = !held && _lastHeld;
        _lastHeld = held;

        if (rose)
        {
            _sessionOpen = true;
            _pressStartedAt = time;
            player.InputSemantic.OnPressEdge(Slot, time);
        }

        if (held)
        {
            player.InputSemantic.OnHoldTick(Slot, time);
        }

        if (fell)
        {
            player.InputSemantic.OnReleaseEdge(Slot, time);
            _sessionOpen = false;
        }
    }
}

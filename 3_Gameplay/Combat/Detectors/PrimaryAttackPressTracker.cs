using System;
using UnityEngine;

/// <summary>
/// 主攻击键：<b>松开</b>时派发唯一 <see cref="GameplayIntentKind.LightAttack"/>，并以累计按住时长供
/// <see cref="SkillSystem"/> / CastType.<see cref="CastType.Charge"/> 决策。
/// </summary>
[Serializable]
public struct PrimaryAttackSplitPolicy
{
    [Tooltip("阈值参考值；实际分档由 SkillData.chargeThreshold / chargeLevels 决定。")]
    [Min(0.04f)]
    public float HoldSecondsBeforeChargedIntent;
}

public sealed class PrimaryAttackPressTracker
{
    bool _lastHeld;
    bool _sessionOpen;
    float _pressStartedAt;

    public void Configure(in PrimaryAttackSplitPolicy policy)
    {
        _ = policy;
    }

    public void SyncInitialHeldState(bool attackHeld)
    {
        _lastHeld = attackHeld;
        if (!attackHeld)
        {
            _sessionOpen = false;
        }
    }

    public void Tick(float time, bool attackHeld, Player player)
    {
        if (player == null)
        {
            return;
        }

        var rose = attackHeld && !_lastHeld;
        var fell = !attackHeld && _lastHeld;
        _lastHeld = attackHeld;

        if (fell)
        {
            player.TryNotifySkillCastInputReleasedForSlot(SkillSlotType.Primary);
        }

        if (rose)
        {
            _sessionOpen = true;
            _pressStartedAt = time;
        }

        if (_sessionOpen && fell)
        {
            var hold = Mathf.Max(0f, time - _pressStartedAt);
            player.EnqueueGameplayIntent(PlayerIntentCatalog.LightAttack(time, null, hold));
            _sessionOpen = false;
        }
    }
}

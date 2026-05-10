using System;
using UnityEngine;

/// <summary>
/// 主攻击键：<b>松开</b>时派发唯一 <see cref="GameplayIntentKind.LightAttack"/>，并以累计按住时长供
/// <see cref="SkillSystem"/> / CastType.<see cref="CastType.Charge"/> 决策。
/// </summary>
[Serializable]
public struct PrimaryAttackSplitPolicy
{
    [Tooltip("长按阈值（秒）：超过后派发 ChargeAttack。")]
    [Min(0.04f)]
    public float HoldSecondsBeforeChargedIntent;

    [Tooltip("连段判定窗口（秒）：短于该窗口的连续短按派发 ComboAttack。")]
    [Min(0.05f)]
    public float ComboTapWindowSeconds;
}

public sealed class PrimaryAttackPressTracker
{
    bool _lastHeld;
    bool _sessionOpen;
    float _pressStartedAt;
    float _lastReleaseAt;
    PrimaryAttackSplitPolicy _policy;

    public void Configure(in PrimaryAttackSplitPolicy policy)
    {
        _policy = policy;
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
            player.TryNotifySkillCastInputReleasedForSlot(SkillSlotType.Skill_Primary_01);
            player.TryNotifySkillCastInputReleasedForSlot(SkillSlotType.Skill_Primary_02);
            player.TryNotifySkillCastInputReleasedForSlot(SkillSlotType.Skill_Primary_03);
        }

        if (rose)
        {
            _sessionOpen = true;
            _pressStartedAt = time;
        }

        if (_sessionOpen && fell)
        {
            var hold = Mathf.Max(0f, time - _pressStartedAt);
            var isCharge = hold >= Mathf.Max(0.04f, _policy.HoldSecondsBeforeChargedIntent);
            var comboWindow = Mathf.Max(0.05f, _policy.ComboTapWindowSeconds);
            var isCombo = !isCharge && _lastReleaseAt > 0.001f && time - _lastReleaseAt <= comboWindow;
            GameplayIntent intent;

            if (isCharge)
            {
                intent = PlayerIntentCatalog.ChargeAttack(time, null, hold);
            }
            else if (isCombo)
            {
                intent = PlayerIntentCatalog.ComboAttack(time, null, hold);
            }
            else
            {
                intent = PlayerIntentCatalog.LightAttack(time, null, hold);
            }

            player.EnqueueGameplayIntent(intent);
            if (player.DebugInterruptFlow)
            {
                var slotLabel = intent.HasSkillSlot ? intent.SkillSlot.ToString() : "(none)";
                Debug.Log(
                    $"[IntentInput] source=PrimaryRelease slot={slotLabel} kind={intent.Kind} hasSlot={intent.HasSkillSlot} hold={hold:F3}s",
                    player);
            }

            _lastReleaseAt = time;
            _sessionOpen = false;
        }
    }
}

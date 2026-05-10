using System;
using UnityEngine;

/// <summary>
/// 交互键（默认映射可作副攻）：松开时派发 <see cref="GameplayIntentKind.HeavyAttack"/> 与按住时长；
/// 与 <see cref="Player.TryNotifySkillCastInputReleasedForSlot"/>（Secondary 槽 Cast 五态）配合。
/// </summary>
[Serializable]
public sealed class SecondaryInteractPressTracker
{
    bool _lastHeld;
    bool _sessionOpen;
    float _pressStartedAt;

    public void SyncInitialHeldState(bool interactHeld)
    {
        _lastHeld = interactHeld;
        if (!interactHeld)
        {
            _sessionOpen = false;
        }
    }

    public void Tick(float time, bool interactHeld, Player player)
    {
        if (player == null)
        {
            return;
        }

        var rose = interactHeld && !_lastHeld;
        var fell = !interactHeld && _lastHeld;
        _lastHeld = interactHeld;

        if (fell)
        {
            player.TryNotifySkillCastInputReleasedForSlot(SkillSlotType.Secondary_04);
        }

        if (rose)
        {
            _sessionOpen = true;
            _pressStartedAt = time;
        }

        if (_sessionOpen && fell)
        {
            var hold = Mathf.Max(0f, time - _pressStartedAt);
            var intent = PlayerIntentCatalog.HeavyAttack(time, null, hold);
            player.EnqueueGameplayIntent(intent);
            if (player.DebugInterruptFlow)
            {
                var slotLabel = intent.HasSkillSlot ? intent.SkillSlot.ToString() : "(none)";
                Debug.Log(
                    $"[IntentInput] source=SecondaryRelease slot={slotLabel} kind={intent.Kind} hasSlot={intent.HasSkillSlot} hold={hold:F3}s",
                    player);
            }

            _sessionOpen = false;
        }
    }
}

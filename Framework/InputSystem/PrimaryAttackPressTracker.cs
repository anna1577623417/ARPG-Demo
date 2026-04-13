using System;
using UnityEngine;

/// <summary>
/// 主攻击键（单一 <c>Attack</c> 绑定，如鼠标左键）在「轻击」与「蓄力攻击」之间的按压段仲裁。
/// <list type="bullet">
/// <item>按下边沿：开始一段按压会话。</item>
/// <item>按住超过 <see cref="PrimaryAttackSplitPolicy.HoldSecondsBeforeChargedIntent"/>：入队 <see cref="GameplayIntentKind.ChargedAttack"/>（至多一次/段按压）。</item>
/// <item>松手边沿且本段未派发过蓄力：入队 <see cref="GameplayIntentKind.LightAttack"/>。</item>
/// </list>
/// 与 <see cref="InputReader.OnAttack"/> 发布的边沿事件解耦，仅以 <see cref="InputReader.IsAttackHeld"/> 为真值来源，避免 Input 回调与 Update 顺序导致的双派发。
/// </summary>
[Serializable]
public struct PrimaryAttackSplitPolicy
{
    [Tooltip("主攻击键从按下起累计按住达到该秒数后，入队蓄力意图（若本段尚未派发蓄力）。")]
    [Min(0.04f)]
    public float HoldSecondsBeforeChargedIntent;
}

public sealed class PrimaryAttackPressTracker
{
    PrimaryAttackSplitPolicy _policy;
    bool _lastHeld;
    bool _sessionOpen;
    bool _chargedIssuedThisSession;
    float _pressStartedAt;

    public void Configure(in PrimaryAttackSplitPolicy policy)
    {
        _policy = policy;
    }

    /// <summary>与当前硬件状态对齐（例如 OnEnable 首帧），避免误识别上升沿。</summary>
    public void SyncInitialHeldState(bool attackHeld)
    {
        _lastHeld = attackHeld;
        if (!attackHeld)
        {
            _sessionOpen = false;
            _chargedIssuedThisSession = false;
        }
    }

    /// <summary>每帧调用一次：<paramref name="attackHeld"/> 须与 <see cref="InputReader.IsAttackHeld"/> 一致。</summary>
    public void Tick(float time, bool attackHeld, Player player)
    {
        if (player == null)
        {
            return;
        }

        var rose = attackHeld && !_lastHeld;
        var fell = !attackHeld && _lastHeld;
        _lastHeld = attackHeld;

        if (rose)
        {
            _sessionOpen = true;
            _chargedIssuedThisSession = false;
            _pressStartedAt = time;
        }

        if (_sessionOpen && attackHeld && !_chargedIssuedThisSession)
        {
            var hold = Mathf.Max(0.04f, _policy.HoldSecondsBeforeChargedIntent);
            if (time - _pressStartedAt >= hold)
            {
                player.EnqueueGameplayIntent(PlayerIntentCatalog.ChargedAttack(time, null));
                ChargeAttackDiagnostics.Log($"PrimaryAttack → ChargedAttack (held ≥ {hold:F3}s)");
                _chargedIssuedThisSession = true;
            }
        }

        if (_sessionOpen && fell)
        {
            if (!_chargedIssuedThisSession)
            {
                player.EnqueueGameplayIntent(PlayerIntentCatalog.LightAttack(time, null));
                ChargeAttackDiagnostics.Log("PrimaryAttack → LightAttack (release before charge threshold)");
            }

            _sessionOpen = false;
            _chargedIssuedThisSession = false;
        }
    }
}

using UnityEngine;

/// <summary>
/// 185.2 — Edge Conditions[] 评估上下文（不含 Action NT；时间窗由 Runner 先行判定）。
/// 186.1 — 追加 MoveDirection 字段（供 MoveDirectionConditionSO 消费）。
/// </summary>
public readonly struct EdgeContext
{
    public readonly Player Player;
    public readonly GameplayIntent Intent;
    public readonly PhaseContext Phase;
    public readonly ContextWindowTracker Windows;
    public readonly EdgeCombatContext Combat;
    public readonly MoveDirection8 MoveDirection;
    public readonly float TimeNow;

    public EdgeContext(
        Player player,
        in GameplayIntent intent,
        in PhaseContext phase,
        ContextWindowTracker windows,
        in EdgeCombatContext combat,
        MoveDirection8 moveDirection,
        float timeNow)
    {
        Player = player;
        Intent = intent;
        Phase = phase;
        Windows = windows;
        Combat = combat;
        MoveDirection = moveDirection;
        TimeNow = timeNow;
    }

    public static EdgeContext From(Player player, in GameplayIntent intent, float timeNow)
    {
        var moveDir = MoveDirection8.None;
        if (player != null)
        {
            var snapshot = player.BuildCombatContext(false, Vector2.zero, false);
            moveDir = snapshot.MoveDirection;
        }

        return new EdgeContext(
            player,
            in intent,
            PhaseContext.FromPlayer(player),
            player != null ? player.ContextWindows : null,
            EdgeCombatContext.FromPlayer(player),
            moveDir,
            timeNow);
    }
}

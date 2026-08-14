using UnityEngine;

/// <summary>
/// 184.4 — Motion Grammar 验收探针。Console 过滤：<c>[Grammar]</c>。
/// </summary>
public static class MotionGrammarProbe
{
    public const string Prefix = "[Grammar]";

    static bool IsEnabled(Player player) =>
        player != null && (GameMainDebugSettings.TurnSubState || GameMainDebugSettings.Locomotion);

    public static void LogFacingCached(
        Player player,
        Vector3 from,
        Vector3 to,
        ActionDataSO owner)
    {
        if (!IsEnabled(player))
        {
            return;
        }

        var reason = owner != null
            ? $"{owner.TransitionType}.ConsumesDirChange"
            : "unknown";
        Debug.Log(
            $"{Prefix} FACING-CACHED owner={owner?.name ?? "?"} " +
            $"from=({from.x:F2},{from.z:F2}) to=({to.x:F2},{to.z:F2}) " +
            $"reason={reason} frame={Time.frameCount}",
            player);
    }

    public static void LogFacingApplied(Player player, ActionDataSO owner, Vector3 to)
    {
        if (!IsEnabled(player))
        {
            return;
        }

        Debug.Log(
            $"{Prefix} FACING-APPLIED owner={owner?.name ?? "?"} " +
            $"to=({to.x:F2},{to.z:F2}) (Turn 动画未播 — Transition 已消费方向变化) frame={Time.frameCount}",
            player);
    }

    public static void LogFacingIgnored(Player player, ActionDataSO owner, string reason)
    {
        if (!IsEnabled(player))
        {
            return;
        }

        Debug.Log(
            $"{Prefix} FACING-IGNORED owner={owner?.name ?? "?"} reason={reason} frame={Time.frameCount}",
            player);
    }

    public static void LogPendingCleared(Player player, string reason)
    {
        if (!IsEnabled(player))
        {
            return;
        }

        Debug.Log($"{Prefix} PENDING-CLEARED reason={reason} frame={Time.frameCount}", player);
    }

    public static void LogTransitionEnter(Player player, ActionDataSO action)
    {
        if (!IsEnabled(player) || action == null || action.TransitionType == TransitionType.None)
        {
            return;
        }

        var g = MotionGrammar.ResolveGrammar(action);
        Debug.Log(
            $"{Prefix} action={action.name} type={action.TransitionType} " +
            $"OwnsPresentation={g.OwnsPresentation} ConsumesDirChange={g.ConsumesDirectionChange} " +
            $"ConsumesMomentumChange={g.ConsumesMomentumChange} BlocksOtherTransitions={g.BlocksOtherTransitions} " +
            $"frame={Time.frameCount}",
            player);
    }
}

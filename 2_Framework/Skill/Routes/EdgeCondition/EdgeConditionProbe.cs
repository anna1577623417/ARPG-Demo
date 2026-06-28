using UnityEngine;

/// <summary>185.2 — Edge Condition 验收探针。Console 过滤：<c>[EdgeCond]</c>。</summary>
public static class EdgeConditionProbe
{
    public const string Prefix = "[EdgeCond]";

    static string s_lastDedupKey;
    static float s_lastDedupTime;

    static bool IsEnabled(Player player) => player != null && player.DebugSkillRoute;

    public static void LogReject(
        Player player,
        in CombatFlowCompiledEdge edge,
        string firstFailLabel,
        in EdgeContext ctx)
    {
        if (!IsEnabled(player))
        {
            return;
        }

        var key = $"{edge.FromNodeId}->{edge.ToNodeId}|{firstFailLabel}|{ctx.Phase.IsGrounded}|{ctx.Intent.Kind}";
        var now = Time.unscaledTime;
        if (key == s_lastDedupKey && now - s_lastDedupTime < 0.05f)
        {
            return;
        }

        s_lastDedupKey = key;
        s_lastDedupTime = now;
        var label = string.IsNullOrEmpty(edge.Label) ? edge.ToNodeId : edge.Label;
        Debug.Log(
            $"{Prefix} reject edge={label} firstFail={firstFailLabel ?? "?"} " +
            $"phase=G:{ctx.Phase.IsGrounded} A:{ctx.Phase.IsAirborne} vy={ctx.Phase.VerticalSpeed:F2} " +
            $"intent={ctx.Intent.Kind} slot={ctx.Intent.EntrySlot} frame={Time.frameCount}",
            player);
    }

    public static void LogPass(Player player, in CombatFlowCompiledEdge edge, in EdgeContext ctx)
    {
        if (!IsEnabled(player))
        {
            return;
        }

        var label = string.IsNullOrEmpty(edge.Label) ? edge.ToNodeId : edge.Label;
        Debug.Log(
            $"{Prefix} pass edge={label} intent={ctx.Intent.Kind} slot={ctx.Intent.EntrySlot} " +
            $"phase=air:{ctx.Phase.IsAirborne} vy={ctx.Phase.VerticalSpeed:F2} frame={Time.frameCount}",
            player);
    }
}

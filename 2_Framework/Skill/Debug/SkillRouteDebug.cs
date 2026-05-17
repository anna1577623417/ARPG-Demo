using UnityEngine;

/// <summary>
/// 技能 Route 运行时诊断 — 由 Player.DebugSkillRoute 总开关控制。
/// 用于排查 HUD 未生成、Route 不退出、Action 支柱无法切回等。
/// </summary>
public static class SkillRouteDebug
{
    public const string CatHud = "Hud";
    public const string CatRebuild = "Rebuild";
    public const string CatRoute = "Route";
    public const string CatAction = "Action";
    public const string CatIntent = "Intent";
    public const string CatSemantic = "Semantic";   // 输入语义层（InputSemanticResolver）
    public const string CatResolve = "Resolve";     // SkillEntryService.TryResolveForIntent 决策
    public const string CatCharge = "Charge";       // ChargeRouteRuntime 状态机
    public const string CatCombo = "Combo";         // Combo Session / 段位提交

    static float s_nextStuckLogTime;

    public static bool IsEnabled(Player player) => player != null && player.DebugSkillRoute;

    public static void Log(Player player, string category, string message, Object context = null)
    {
        if (!IsEnabled(player))
        {
            return;
        }

        var ctx = context != null ? context : player;
        Debug.Log($"[SkillRoute][{category}] {message}", ctx);
    }

    public static void LogWarn(Player player, string category, string message, Object context = null)
    {
        if (player != null && !IsEnabled(player))
        {
            return;
        }

        var ctx = context != null ? context : player;
        Debug.LogWarning($"[SkillRoute][{category}] {message}", ctx);
    }

    /// <summary>Action 内 Route 仍 Active 且动作已播完 — 节流告警（疑似 Stage 未完成 / Route 未收口）。</summary>
    public static void TryLogActionStuck(Player player, float actionNt, bool routeActive, bool stageCompleted, float stageDuration)
    {
        if (!IsEnabled(player) || actionNt < 0.9999f)
        {
            return;
        }

        if (routeActive && Time.time < s_nextStuckLogTime)
        {
            return;
        }

        if (!routeActive)
        {
            return;
        }

        s_nextStuckLogTime = Time.time + 1f;
        LogWarn(
            player,
            CatAction,
            $"STUCK? actionNt={actionNt:F2} routeActive=true stageCompleted={stageCompleted} stageDur={stageDuration:F3}s " +
            "(Normal 单段需 Stage.Completed；检查 Action 时长与 Transitions)");
    }
}

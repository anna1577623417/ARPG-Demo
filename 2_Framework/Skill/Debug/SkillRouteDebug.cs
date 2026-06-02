using UnityEngine;

/// <summary>
/// SkillRoute 诊断 — 由 Player 上 Debug 开关分通道控制。
/// </summary>
public static class SkillRouteDebug
{
    public const string CatDodge4 = "Dodge4";
    public const string CatRoll4 = "Roll4";
    public const string CatAbility = "Ability";
    public const string CatFlow = "Flow";

    public const string CatHud = "Hud";
    public const string CatRebuild = "Rebuild";
    public const string CatRoute = "Route";
    public const string CatAction = "Action";
    public const string CatIntent = "Intent";
    public const string CatSemantic = "Semantic";
    public const string CatResolve = "Resolve";
    public const string CatCharge = "Charge";
    public const string CatCombo = "Combo";
    public const string CatComboCd = "ComboCd";
    public const string CatStage = "Stage";
    public const string CatCtx = "Ctx";
    public const string CatGraph = "Graph";
    public const string CatCond = "Cond";
    public const string CatUnit = "Unit";
    public const string CatExtract = "Extract";

    public static bool IsEnabled(Player player) => player != null && player.DebugSkillRoute;

    public static bool IsDodge4Enabled(Player player) =>
        player != null && player.DebugSkillRouteDodge4;

    public static bool IsRoll4Enabled(Player player) =>
        player != null && player.DebugSkillRouteRoll4;

    public static bool IsAbilityEnabled(Player player) =>
        player != null && player.DebugSkillAbility;

    public static bool IsDodge4TraceIntent(in GameplayIntent intent) =>
        intent.Semantic == InputSemanticType.Directional;

    public static bool IsDodge4TraceSlot(SkillEntrySlot slot) =>
        slot == SkillEntrySlot.Space;

    public static bool IsRoll4Group(SkillGroupDefinition group) =>
        group != null && group.name.IndexOf("Wuxia", System.StringComparison.OrdinalIgnoreCase) >= 0;

    public static void LogDodge4(Player player, string phase, string message, Object context = null)
    {
        if (!IsDodge4Enabled(player))
        {
            return;
        }

        Emit(player, CatDodge4, phase, message, context, warning: false);
    }

    public static void LogRoll4(Player player, string phase, string message, Object context = null)
    {
        if (!IsRoll4Enabled(player))
        {
            return;
        }

        Emit(player, CatRoll4, phase, message, context, warning: false);
    }

    /// <summary>四向解析：按 Group 名自动走 Dodge4 或 Roll4 通道。</summary>
    public static void LogDirectional4(
        Player player,
        SkillGroupDefinition group,
        string phase,
        string message,
        Object context = null)
    {
        if (IsRoll4Group(group))
        {
            LogRoll4(player, phase, message, context);
        }
        else
        {
            LogDodge4(player, phase, message, context);
        }
    }

    public static void LogFlow(Player player, string message, Object context = null)
    {
        if (!IsEnabled(player) && !IsAbilityEnabled(player))
        {
            return;
        }

        var ctx = context != null ? context : player;
        Debug.Log($"[SkillRoute][{CatFlow}] {message}", ctx);
    }

    public static void LogAbility(Player player, string message, Object context = null)
    {
        if (!IsAbilityEnabled(player))
        {
            return;
        }

        var ctx = context != null ? context : player;
        Debug.Log($"[Ability] {message}", ctx);
    }

    public static void LogSetup(Player player, CombatGraphAsset flow, AbilityMapSO abilityMap, int contextGroupCount)
    {
        if (!IsEnabled(player) && !IsAbilityEnabled(player))
        {
            return;
        }

        var ctx = player != null ? player : null;
        Debug.Log(
            $"[SkillRoute][Setup] loadout flow={(flow != null ? flow.name : "null")} " +
            $"abilityMap={(abilityMap != null ? abilityMap.name : "null")} contextGroups={contextGroupCount}",
            ctx);
    }

    public static void LogDodge4Warn(Player player, string phase, string message, Object context = null)
    {
        if (!IsDodge4Enabled(player))
        {
            return;
        }

        Emit(player, CatDodge4, phase, message, context, warning: true);
    }

    public static void LogRoll4Warn(Player player, string phase, string message, Object context = null)
    {
        if (!IsRoll4Enabled(player))
        {
            return;
        }

        Emit(player, CatRoll4, phase, message, context, warning: true);
    }

    public static void Log(Player player, string category, string message, Object context = null)
    {
        if (!IsEnabled(player) || !ShouldEmit(category, message))
        {
            return;
        }

        var ctx = context != null ? context : player;
        Debug.Log($"[SkillRoute][{category}] {message}", ctx);
    }

    public static void LogWarn(Player player, string category, string message, Object context = null)
    {
        if (player != null && (!IsEnabled(player) || !ShouldEmit(category, message)))
        {
            return;
        }

        var ctx = context != null ? context : player;
        Debug.LogWarning($"[SkillRoute][{category}] {message}", ctx);
    }

    public static void LogError(Player player, string category, string message, Object context = null)
    {
        if (!ShouldEmit(category, message) && category != CatGraph)
        {
            return;
        }

        var ctx = context != null ? context : player;
        Debug.LogError($"[SkillRoute][{category}] {message}", ctx);
    }

    public static void TryLogActionStuck(
        Player player,
        float actionNt,
        bool routeActive,
        bool stageCompleted,
        float stageDuration)
    {
    }

    static void Emit(Player player, string category, string phase, string message, Object context, bool warning)
    {
        var ctx = context != null ? context : player;
        var line = $"[SkillRoute][{category}] {phase} | {message}";
        if (warning)
        {
            Debug.LogWarning(line, ctx);
        }
        else
        {
            Debug.Log(line, ctx);
        }
    }

    static bool ShouldEmit(string category, string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return false;
        }

        switch (category)
        {
            case CatSemantic:
                return message.StartsWith("DIRECTIONAL");

            case CatResolve:
                return message.Contains("Directional")
                    || message.Contains("Group=")
                    || message.Contains("PrimaryRoute")
                    || message.Contains("PrimaryUnit");

            case CatCtx:
            case CatGraph:
            case CatCond:
            case CatUnit:
                return true;

            default:
                return false;
        }
    }
}

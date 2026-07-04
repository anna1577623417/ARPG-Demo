using UnityEngine;

/// <summary>
/// 165.1 Bug 调试 Log — 稳定前缀供 Console 过滤；门控见 <see cref="IsActionTimingEnabled"/> / <see cref="IsRouteExitEnabled"/>。
/// </summary>
public static class Locomotion165Diagnostics
{
    public const string PrefixAction = "[Action]";
    public const string PrefixJump = "[Jump]";
    public const string PrefixLoco = "[Loco]";

    /// <summary>Bug #1+#4+#5：Action 时序 / 空中 Locomotion 微调。</summary>
    public static bool IsActionTimingEnabled(Player player) =>
        player != null && GameMainDebugSettings.Locomotion;

    /// <summary>Bug #6：多 Stage Route 末段退出。</summary>
    public static bool IsRouteExitEnabled(Player player) =>
        player != null && GameMainDebugSettings.Locomotion;

    /// <summary>Bug #3+#7：Locomotion 边沿 / 跑步。</summary>
    public static bool IsLocomotionEnabled(Player player) =>
        player != null && GameMainDebugSettings.Locomotion;

    public static void LogAnimSync(Player player, ActionDataSO action)
    {
        if (!IsActionTimingEnabled(player) || action == null)
        {
            return;
        }

        var clipLen = action.MainClip != null ? action.MainClip.length : 0f;
        Debug.Log(
            $"{PrefixAction}[AnimSync] action={action.name} dur={action.Duration:F3} clip={clipLen:F3} " +
            $"animSpd={action.ResolveEffectiveAnimSpeed():F3} mode={action.ClipAnimSpeedMode}",
            player);
    }

    /// <summary>Bug #5：Locomotion 车道 Action 在空中叠加 MoveByLocomotionIntent（节流）。</summary>
    public static void LogAirLocoMove(Player player, ActionDataSO action, ref float nextLogTime)
    {
        if (!IsActionTimingEnabled(player) || action == null)
        {
            return;
        }

        var now = Time.unscaledTime;
        if (now < nextLogTime)
        {
            return;
        }

        nextLogTime = now + 0.25f;
        Debug.Log(
            $"{PrefixAction}[AirLocoMove] action={action.name} grounded={player.IsGrounded} " +
            $"wantsRun={player.WantsRun} frame={Time.frameCount}",
            player);
    }

    /// <summary>Bug #6：Action 退出原因（Route 收尾 vs 末段 Stage 兜底）。</summary>
    public static void LogActionExit(
        Player player,
        ActionDataSO action,
        float normalizedTime,
        bool routeEnded,
        bool stageCompleted,
        bool isLastStage,
        string exitReason)
    {
        if (!IsRouteExitEnabled(player))
        {
            return;
        }

        var route = player.SkillEntries?.ActiveRoute;
        Debug.Log(
            $"{PrefixAction}[Exit] reason={exitReason} action={(action != null ? action.name : "NULL")} " +
            $"nt={normalizedTime:F3} routeActive={route != null && route.IsActive} " +
            $"stageCompleted={stageCompleted} isLastStage={isLastStage} routeEnded={routeEnded} " +
            $"frame={Time.frameCount}",
            player);
    }

    public static void LogJumpLand(
        Player player,
        string source,
        ActionDataSO landAction,
        float fallHeight)
    {
        if (!IsLocomotionEnabled(player))
        {
            return;
        }

        Debug.Log(
            $"{PrefixJump}[Land] source={source} action={(landAction != null ? landAction.name : "NULL")} " +
            $"fallHeight={fallHeight:F2} frame={Time.frameCount}",
            player);
    }

    /// <summary>Profile 未配 JumpLand — 始终 Warning（配置错误，不限 Debug 开关）。</summary>
    public static void WarnJumpLandProfileMissing(Player player)
    {
        Debug.LogWarning(
            $"{PrefixJump}[Land] source=NONE Profile.JumpLand 未配置 → AnimLibrary.Airborne_Land 兜底。" +
            "请在 LocomotionProfile.Bindings 注册 JumpLand。",
            player);
    }

    public static void LogJumpLandAnimFallback(Player player)
    {
        if (!IsLocomotionEnabled(player))
        {
            return;
        }

        Debug.Log(
            $"{PrefixJump}[Land] source=NONE → fallback PlayerAnimLibrary.Airborne_Land frame={Time.frameCount}",
            player);
    }

    public static void LogSuppressEdge(Player player, int suppressFrames, LocomotionStateId hint)
    {
        if (!IsLocomotionEnabled(player))
        {
            return;
        }

        Debug.Log(
            $"{PrefixLoco}[SuppressEdge] frame={suppressFrames} reason=AfterEnd hint={hint} frame={Time.frameCount}",
            player);
    }

    public static void LogEndHintSet(Player player, LocomotionStateId hint)
    {
        if (!IsLocomotionEnabled(player))
        {
            return;
        }

        Debug.Log($"{PrefixLoco}[EndHint] set={hint} frame={Time.frameCount}", player);
    }

    /// <summary>Bug #8：Walk/Run End 退出时残余平面速度（182.1 Stop Authoring）。</summary>
    public static void LogActionExitStop(
        Player player,
        LocomotionStateId endHint,
        Vector3 residualPlanar,
        bool cleared,
        bool stopFeatureEnabled,
        StopStrategy? stopStrategy)
    {
        if (player == null)
        {
            return;
        }

        if (!GameMainDebugSettings.Locomotion && !GameMainDebugSettings.Stop && endHint == LocomotionStateId.None)
        {
            return;
        }

        var strategyLabel = stopFeatureEnabled && stopStrategy.HasValue
            ? stopStrategy.Value.ToString()
            : "<no-stop-feature>";
        Debug.Log(
            $"{PrefixAction}[Exit] endHint={endHint} stop={stopFeatureEnabled} strategy={strategyLabel} " +
            $"residualVel=({residualPlanar.x:F2},{residualPlanar.z:F2}) " +
            $"mag={new Vector3(residualPlanar.x, 0f, residualPlanar.z).magnitude:F2} cleared={cleared} " +
            $"frame={Time.frameCount}",
            player);
    }

    public static void LogStopOpen(Player player, ActionDataSO action, string reason)
    {
        if (player == null || (!GameMainDebugSettings.Locomotion && !GameMainDebugSettings.Stop))
        {
            return;
        }

        Debug.Log(
            $"{PrefixAction}[Exit] OPEN handshake Stop: {reason} action={(action != null ? action.name : "NULL")} " +
            $"frame={Time.frameCount}",
            player);
    }

    /// <summary>Bug #8：进入 Locomotion 时的平面速度（对照 Exit 是否已清零）。</summary>
    public static void LogLocoEnter(Player player, Vector3 planarVelocity)
    {
        if (!IsLocomotionEnabled(player))
        {
            return;
        }

        Debug.Log(
            $"{PrefixLoco}[Enter] planarVel=({planarVelocity.x:F2},{planarVelocity.z:F2}) " +
            $"mag={new Vector3(planarVelocity.x, 0f, planarVelocity.z).magnitude:F2} frame={Time.frameCount}",
            player);
    }

    /// <summary>Bug #7：Sprint Hold/Toggle 状态变化。</summary>
    public static void LogRunIntent(
        Player player,
        RunInputMode mode,
        bool wantsRun,
        bool runToggled,
        bool sprintHeld,
        string trigger)
    {
        if (!IsLocomotionEnabled(player))
        {
            return;
        }

        Debug.Log(
            $"{PrefixLoco}[Run] mode={mode} trigger={trigger} wantsRun={wantsRun} " +
            $"toggled={runToggled} sprintHeld={sprintHeld} frame={Time.frameCount}",
            player);
    }
}

using UnityEngine;

/// <summary>
/// 动作期间转向诊断通道（独立于 [ActInpDiag]，prefix = [ActTurnDiag]）。
///
/// 设计目标：
///   ① 只在 PlayerActionState 状态下报告（普通 Locomotion 转向不报）。
///   ② 每个调用点必须传自己的 caller 名字 —— "谁动了 LogicForward" 一眼看清。
///   ③ 同时输出 ShouldSuppressLocomotionRotation 当前真值，便于核对保护标志是否生效。
///   ④ 微小角度（&lt; 1°）自动过滤，避免每帧朝向微调刷屏。
///
/// 用法：
///   ActionTurnProbeSwitch 挂场景 → Enabled = true → TrackedPlayer 拖玩家
///   或代码：ActionTurnProbe.Enabled = true; ActionTurnProbe.SetTrackedPlayer(player);
/// </summary>
public static class ActionTurnProbe
{
    /// <summary>总开关。关闭时所有 Log 方法零成本短路。</summary>
    public static bool Enabled = false;

    /// <summary>仅跟踪指定 Player。null = 全跟。</summary>
    static Player s_tracked;

    /// <summary>角度差阈值（度），低于此值不打 log。</summary>
    public static float MinDeltaDeg = 1f;

    const string Prefix = "[ActTurnDiag]";

    public static void SetTrackedPlayer(Player player) => s_tracked = player;

    static bool ShouldSkip(Player p)
    {
        if (!Enabled) return true;
        if (s_tracked != null && !ReferenceEquals(s_tracked, p)) return true;
        return false;
    }

    /// <summary>
    /// SetLogicForward 调用点统一探针。caller 字符串带具体调用方法名。
    /// 只在 PlayerActionState 状态下打 log（Locomotion 状态的正常转向不报）。
    /// </summary>
    public static void Log(Player p, Vector3 from, Vector3 to, string caller)
    {
        if (ShouldSkip(p)) return;
        if (p == null) return;

        // 仅 Action 状态期间的转向才关心
        var act = p.States?.Current as PlayerActionState;
        if (act == null) return;

        from.y = 0f;
        to.y = 0f;
        if (from.sqrMagnitude < 0.0001f || to.sqrMagnitude < 0.0001f) return;

        var signed = Vector3.SignedAngle(from.normalized, to.normalized, Vector3.up);
        if (Mathf.Abs(signed) < MinDeltaDeg) return;

        var actionName = act.CurrentAction != null ? act.CurrentAction.name : "-";
        var suppressFlag = p.ShouldSuppressLocomotionRotation() ? "Y" : "N";

        Debug.Log(
            $"{Prefix} [{Time.frameCount}|{Time.unscaledTime:F3}s] " +
            $"action={actionName} " +
            $"Δyaw={signed:+0.0;-0.0}° " +
            $"caller={caller} " +
            $"suppressFlag={suppressFlag}",
            p);
    }

    /// <summary>诊断状态摘要（开关助手用）。</summary>
    public static string DescribeState()
    {
        var tracked = s_tracked != null ? s_tracked.name : "(all)";
        return $"{Prefix} Enabled={Enabled} tracked={tracked} minDelta={MinDeltaDeg:F1}°";
    }
}

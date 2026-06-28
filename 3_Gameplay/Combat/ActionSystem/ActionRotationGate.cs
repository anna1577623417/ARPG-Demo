using UnityEngine;

/// <summary>
/// 198.3 — 动作期间转向/移动单点闸门。
///
/// 所有 SetLogicForward / SetPlanarVelocity 调用前查 <see cref="IsAllowed"/> —— 默认拒绝。
/// 仅在以下条件**全部**满足时放行：
///   ① 当前 PlayerActionState 持有合法 ActionDataSO
///   ② action.EnableRotationInput = true（总开关，双保险）
///   ③ action.Windows 存在某个窗口 w，使得 nt ∈ [w.NormalizedStart, w.NormalizedEnd]
///   ④ w.AllowFacingInput（Kind.Facing）或 w.AllowMoveInput（Kind.Move）= true
///
/// 非 Action 状态（Locomotion / Airborne）默认放行（由 Locomotion 自身负责仲裁）。
///
/// 数据存储：复用 ActionWindow.AllowFacingInput / AllowMoveInput 字段。
/// 编辑入口：Action Timeline 编辑器内 "Rotation Input" 虚拟轨道。
/// </summary>
public static class ActionRotationGate
{
    /// <summary>判定维度。</summary>
    public enum Kind : byte
    {
        /// <summary>逻辑转向（SetLogicForward）。</summary>
        Facing = 0,
        /// <summary>移动叠加（SetPlanarVelocity / SetMovementIntent）。</summary>
        Move = 1,
    }

    /// <summary>
    /// 在调用 SetLogicForward / SetPlanarVelocity 之前调用，决定本帧输入是否允许生效。
    /// 默认拒绝（return false）；仅在 Action Windows 内某个窗口允许时返回 true。
    /// </summary>
    public static bool IsAllowed(Player player, Kind kind)
    {
        if (player == null) return true; // 防御性：无玩家直接放行（编辑器场景 / 单元测试 mock）

        // 非 Action 状态：完全不管（Locomotion 自己负责）
        var actState = player.States?.Current as PlayerActionState;
        if (actState == null) return true;

        var action = actState.CurrentAction;
        if (action == null) return false; // Action 状态但无 Action 数据：屏蔽

        // ★ 总开关（双保险）：未显式启用时直接拒绝，无视所有窗口配置（修复 198.2 默认行为）
        if (!action.EnableRotationInput) return false;

        var windows = action.Windows;
        if (windows == null || windows.Count == 0) return false;

        var nt = actState.NormalizedTime;
        for (var i = 0; i < windows.Count; i++)
        {
            var w = windows[i];
            if (nt < w.NormalizedStart || nt > w.NormalizedEnd) continue;
            var allow = kind == Kind.Facing ? w.AllowFacingInput : w.AllowMoveInput;
            if (allow) return true;
        }
        return false;
    }
}

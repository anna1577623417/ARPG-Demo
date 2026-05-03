using UnityEngine;

/// <summary>
/// Player ⇄ KCC 解耦的最小契约：把"物理意图入口 / 触发 / 查询"凝练为一组方法，
/// 状态机与 MotionExecutor 仅通过本接口与马达交互；不直接读 Player.transform。
///
/// ═══ Stage R1（当前阶段）═══
///   · Player 实现该接口（保留全部内部物理实现 / 9 道闸门）。
///   · 调用方（PlayerLocomotionState / PlayerActionState / Adapters）改为通过该接口调用。
///   · 行为零变化；只是把"调用面"收窄。
///
/// ═══ Stage R2 / R3（后续）═══
///   · R2：把 ApplyMotor / RefreshGroundedState / ApplySimpleGravity / Resolve* / EdgeSlip
///         逐个迁出到独立 PlayerKCCMotor : MonoBehaviour, IPlayerMotor。
///   · R3：引入 MotorPolicy（Locomotion / Airborne / ActionSuspended / ActionPhysics …），
///         由状态机 SetPolicy 驱动 Motor 的「重力 / 接地 / EdgeSlip / Snap」分支选择。
/// </summary>
public interface IPlayerMotor
{
    // ─── 输入：写速度 / 重力开关 ─────────────────────────────────────────
    void SetPlanarVelocity(Vector3 planar);
    void SetVerticalSpeed(float vy);
    void SuspendGravity();
    void ReleaseGravity();

    // ─── 触发：每帧/瞬时执行 ────────────────────────────────────────────
    /// <summary>用 Player 当前 (planar, vy) 求解一帧位移；策略由 ctx 决定。</summary>
    void ApplyMotor(in MotorSolveContext ctx);

    /// <summary>跳过 planar 累加，直接用外部 gameplay 速度求解一帧位移（MotionExecutor 路径）。</summary>
    void ApplyMotorFromGameplayVelocity(Vector3 worldVelocity, in MotorSolveContext ctx);

    /// <summary>离散瞬移；forceAirborne=true：禁止贴地射线 / 不清 vy / 用 Airborne 上下文刷新接地。</summary>
    void TeleportTo(Vector3 worldPosition, bool forceAirborne = false);

    // ─── 查询 ────────────────────────────────────────────────────────────
    bool IsGrounded { get; }
    float VerticalSpeed { get; }
    Vector3 PlanarVelocity { get; }
    bool IsGravitySuspended { get; }

    /// <summary>当前 motor 上下文构造便携助手（动作内常用）。</summary>
    MotorSolveContext BuildActionMotorSolveContext();
}

using System;

/// <summary>
/// 全局状态与能力标签（ulong 位掩码）。
/// 分区约定（便于策划表驱动与代码审查）：
/// - Bit 0–15：物理/空间姿态（Physical）
/// - Bit 16–31：动作阶段（Phase），如起手/判定/收招
/// - Bit 32–47：能力窗口（Ability），用于“只查标签不查状态名”的仲裁
/// - Bit 48–63：预留（Reserved），例如阵营、武器形态等后续扩展
///
/// 使用方式：
/// - 逻辑层每帧更新 <see cref="GameplayTagMask"/>，由状态写入/剥离标签。
/// - 意图与 <see cref="Framework.ActionSystem.FrameContext"/> 只做位运算匹配。
/// </summary>
[Flags]
public enum StateTag : ulong
{
    None = 0UL,

    // ─── Physical (0–15) ───
    Grounded = 1UL << 0,
    Airborne = 1UL << 1,
    Climbing = 1UL << 2,
    Swimming = 1UL << 3,

    // ─── Phase (16–31) ───
    PhaseStartup = 1UL << 16,
    PhaseActive = 1UL << 17,
    PhaseRecovery = 1UL << 18,
    Stunned = 1UL << 19,

    // ─── Ability windows (32–47) ───
    CanJump = 1UL << 32,
    CanDodge = 1UL << 33,
    CanLightAttack = 1UL << 34,
    CanHeavyAttack = 1UL << 35,
    CanCancelToLocomotion = 1UL << 36,
    Invulnerable = 1UL << 37,
    /// <summary>离散直线爆发（剑冲等），与翻滚冷却可独立配置。</summary>
    CanSwordDash = 1UL << 38,

    /// <summary>本段攻击带有蓄力完成 payload（伤害倍率等由命中盒读取标签区分）。</summary>
    AttackCharged = 1UL << 39,

    // ─── Reserved (48–63) ───
    Dead = 1UL << 48,
}

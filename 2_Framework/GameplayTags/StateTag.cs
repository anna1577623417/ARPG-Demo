using System;

/// <summary>
/// 全局状态与能力标签（ulong 位掩码）。
/// 分区约定（便于策划表驱动与代码审查）：
/// - Bit 0–15：物理/空间姿态（Physical）
/// - Bit 16–31：动作阶段（Phase），如起手/判定/收招
/// - Bit 32–39：实体能力闸门（Ability gates）—— "实体当前是否具备做某事的资格"
///             由 Locomotion / Airborne 状态根据冷却、资源、姿态写入 Player.GameplayTags
/// - Bit 40–47：动作打断许可（Action interrupt permissions）—— "当前动作的此时间段允许被某类意图打断"
///             ★ 仅出现在 ActionWindow 中，由 ActionInterruptResolver 在 Action 状态内仲裁
///             ★ 与 Bit 32–39 严格区分：Can* = 实体属性；AllowInterruptBy* = 动作窗口属性
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

    // ─── Ability gates (32–39) — 实体属性，由 Locomotion/Airborne 写入 ───
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

    // ─── Action interrupt permissions (40–47) ─────────────────────────────────
    // 语义：仅出现在 ActionWindow.WindowSlotMask 中，表示"该窗口允许被某类意图打断"。
    // 由 ActionInterruptResolver 读取窗口聚合掩码，与传入意图做位与判定。
    // 例：翻滚动作 [0.5, 1.0] 窗口勾选 AllowInterruptBySwordDash → 翻滚后半段可被 Shift 打断。
    AllowInterruptByDodge       = 1UL << 40,
    AllowInterruptBySwordDash   = 1UL << 41,
    AllowInterruptByLight       = 1UL << 42,
    AllowInterruptByHeavy       = 1UL << 43,
    AllowInterruptByCharged     = 1UL << 44,
    AllowInterruptByJump        = 1UL << 45,

    // ─── Reserved (48–63) ───
    Dead = 1UL << 48,
}

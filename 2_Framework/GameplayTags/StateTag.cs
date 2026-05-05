using System;

/// <summary>
/// State.* — 行为状态（正在做什么），与 FSM / Action / Motion 对齐。
/// <para>语义路径对照（设计文档 → 本枚举）：</para>
/// <list type="bullet">
/// <item><description>State.Locomotion.Grounded → <see cref="Grounded"/></description></item>
/// <item><description>State.Locomotion.Airborne → <see cref="Airborne"/></description></item>
/// <item><description>State.Locomotion.Dashing → （可用独立状态或后续扩展位）</description></item>
/// <item><description>State.Action.* / 阶段 → <see cref="PhaseStartup"/> / <see cref="PhaseActive"/> / <see cref="PhaseRecovery"/></description></item>
/// <item><description>State.Action.Attacking.* → 阶段/受击等由状态与别数据表达；<b>ActionWindow 仅配 AllowInterrupt*（打断机制）</b></description></item>
/// <item><description>State.Ability.Casting → 可由 Phase 或后续专用位扩展</description></item>
/// </list>
/// <para>分区约定（便于策划表驱动与代码审查）：</para>
/// - Bit 0–15：物理/空间姿态（Physical）
/// - Bit 16–31：动作阶段（Phase），如起手/判定/收招
/// - Bit 32–39：【遗留/元数据】Can* 已迁往实体 Ability 轨；Invulnerable / AttackCharged 等供别用（非 ActionWindow 打断专精 UI）
/// - Bit 40–45：AllowInterrupt* — 唯一定义「某时刻可被哪类意图打断」；<b>ActionWindow / 连续状态打断掩码 Inspector 仅暴露这几位</b>，由 <see cref="ActionInterruptResolver"/> 聚合读取。
/// - Bit 46–47：预留打断扩展（保留枚举槽位；当前常用为 40–45）
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

    /// <summary>攻击判定碰撞体启用（动作时间窗内的 Hitbox 激活）。帧事件见 ActionWindow.RuntimeEvents。</summary>
    HitboxActive_Window = 1UL << 20,

    /// <summary>Root motion 驱动位移（该片段时间由动画根运动接管平移）。</summary>
    RootMotion_Window = 1UL << 21,

    // ─── Legacy / action-meta (32–39)：槽位与旧资产兼容；Can* 勿再写入 State 轨，勿出现在窗口合并结果中 ───
    [Obsolete("使用 EntityCapabilityTag + GameplayTagContainer.Ability 轨")]
    CanJump = 1UL << 32,
    [Obsolete("使用 EntityCapabilityTag + GameplayTagContainer.Ability 轨")]
    CanDodge = 1UL << 33,
    [Obsolete("使用 EntityCapabilityTag + GameplayTagContainer.Ability 轨")]
    CanLightAttack = 1UL << 34,
    [Obsolete("使用 EntityCapabilityTag + GameplayTagContainer.Ability 轨")]
    CanHeavyAttack = 1UL << 35,
    [Obsolete("使用 EntityCapabilityTag + GameplayTagContainer.Ability 轨")]
    CanCancelToLocomotion = 1UL << 36,
    Invulnerable = 1UL << 37,
    [Obsolete("使用 EntityCapabilityTag + GameplayTagContainer.Ability 轨")]
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

    // ─── Window timeline semantics (46–47)：仅 ActionWindow 时间片段；非 Ability、非资格闸门 ───
    /// <summary>连招 / 输入缓冲开放区间（时间语义）。消费逻辑由 IntentBuffer / 连招状态机读取。</summary>
    ComboInput_Window = 1UL << 46,

    // ─── Reserved (48–63) ───
    Dead = 1UL << 48,
}

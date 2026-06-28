using System;
using UnityEngine;

/// <summary>
/// 168.3 — 空中三阶段（Ascending / ApexHold / Descending）位掩码。
/// 用于 <see cref="AirInterruptPolicy"/> 三段独立 mask 的 Phase 判定。
/// </summary>
[Flags]
public enum AirbornePhaseMask : byte
{
    None        = 0,
    Ascending   = 1 << 0,   // VerticalSpeed > +ApexVyThreshold
    ApexHold    = 1 << 1,   // |VerticalSpeed| <= ApexVyThreshold
    Descending  = 1 << 2,   // VerticalSpeed < -ApexVyThreshold
    All         = Ascending | ApexHold | Descending,
}

/// <summary>
/// 168.3 — 角色画像：空中状态可中断闸门（L1）。
///
/// ═══ 设计契约 ═══
///   · 仅决定"本角色在空中三阶段是否允许新 Intent 中断当前 Airborne 控制权"。
///   · 不决定"具体技能能不能释放"（那是 AbilityGateService / L2 的职责）。
///   · 配置在 <see cref="SkillEntryLoadoutSO"/>，与角色装载绑定；换 Loadout = 换整套空中画像。
///
/// ═══ 判定流（PlayerAirborneState.TryConsumeGameplayIntent） ═══
///   AirInterruptResolver.Evaluate
///     ① 系统硬下限（PlayerStateManager.airborneHardFloorBlock，默认 None）
///     ② 当前 Phase 的 Loadout mask 是否允许 incomingCategory
///   → 通过 → 进入 IntentRouter.Route → SkillEntryService（L2 Ability 闸门）
/// </summary>
[Serializable]
public struct AirInterruptPolicy
{
    [Tooltip("Ascending（上升）允许哪些 ActionCategory 中断进入新意图")]
    public ActionCategory AscendingAllowed;

    [Tooltip("ApexHold（顶点）允许哪些 ActionCategory")]
    public ActionCategory ApexAllowed;

    [Tooltip("Descending（下落）允许哪些 ActionCategory")]
    public ActionCategory DescendingAllowed;

    [Tooltip("Apex 判定阈值：|VerticalSpeed| < 此值视为顶点段")]
    [Range(0.1f, 3f)] public float ApexVyThreshold;

    [Tooltip("Coyote 宽限：刚离地此秒数内仍按地面判定（与 Jump 系统 CoyoteTime 独立；当前未消费，预留给 §7.3 #12 验收）")]
    [Range(0f, 0.3f)] public float GroundCoyoteSeconds;

    /// <summary>默认全开 —— 经典 ACT 灵活角色（鬼泣 / 战神）。</summary>
    public static AirInterruptPolicy Universal => new()
    {
        AscendingAllowed  = ActionCategoryPresets.AllPillar,
        ApexAllowed       = ActionCategoryPresets.AllPillar,
        DescendingAllowed = ActionCategoryPresets.AllPillar,
        ApexVyThreshold   = 1.5f,
        GroundCoyoteSeconds = 0.08f,
    };

    /// <summary>上升段不可中断，顶点局部可，下落全开 —— 黑魂 / 只狼（起跳冲量神圣）。</summary>
    public static AirInterruptPolicy DescendingOnly => new()
    {
        AscendingAllowed  = ActionCategory.None,
        ApexAllowed       = ActionCategory.Movement | ActionCategory.Offense,
        DescendingAllowed = ActionCategoryPresets.AllPillar,
        ApexVyThreshold   = 1.5f,
        GroundCoyoteSeconds = 0.08f,
    };

    /// <summary>纯地面角色，仅下落允许翻滚保命 —— 弓手 / 法师 / 重甲。</summary>
    public static AirInterruptPolicy GroundOnly => new()
    {
        AscendingAllowed  = ActionCategory.None,
        ApexAllowed       = ActionCategory.None,
        DescendingAllowed = ActionCategory.Movement,
        ApexVyThreshold   = 1.5f,
        GroundCoyoteSeconds = 0.05f,
    };
}

/// <summary>168.3 —— <see cref="ActionCategory"/> 常用聚合常量（Editor / 预设公用）。</summary>
public static class ActionCategoryPresets
{
    /// <summary>4 类玩家可控行为 + Locomotion 全开。</summary>
    public const ActionCategory AllPillar =
        ActionCategory.Movement
        | ActionCategory.Offense
        | ActionCategory.Defensive
        | ActionCategory.Utility
        | ActionCategory.Locomotion;
}

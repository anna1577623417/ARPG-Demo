using System;
using UnityEngine;

/// <summary>技能释放模型。</summary>
public enum CastType : byte
{
    Instant = 0,
    CastTime = 1,
    Channel = 2,
    Charge = 3,
    HoldRelease = 4,
}

/// <summary>冷却开始策略。</summary>
public enum CooldownPolicy : byte
{
    OnFirstCast = 0,
    OnLastCast = 1,
    OnSkillEnd = 2,
}

/// <summary>目标类型（Targeting）。</summary>
public enum TargetType : byte
{
    Self = 0,
    SingleTarget = 1,
    Area = 2,
    Direction = 3,
    Projectile = 4,
}

/// <summary>多段技能的阶段流转。</summary>
public enum StageTransitionType : byte
{
    Auto = 0,
    OnInput = 1,
    OnHit = 2,
    OnTimer = 3,
}

/// <summary>技能机制位标签。</summary>
/// <remarks>
/// 必须使用 int 作为底层类型：Unity 序列化/Inspector 不支持 ulong 枚举（会报 Unsupported enum type）。
/// </remarks>
[Flags]
public enum SkillTrait
{
    None = 0,
    ResetCooldownOnKill = 1 << 0,
    IgnoreDefense = 1 << 1,
    Unstoppable = 1 << 2,
    RefundCostOnMiss = 1 << 3,
    ScaleWithAttackSpeed = 1 << 4,
}

/// <summary>资源消耗条目。</summary>
[Serializable]
public struct SkillCost
{
    public ResourceType resourceType;
    public float baseAmount;
}

/// <summary>等级解锁 Trait。</summary>
[Serializable]
public struct SkillTraitUnlock
{
    public int requiredLevel;
    public SkillTrait trait;
}

/// <summary>蓄力分档（Charge CastType）。</summary>
[Serializable]
public struct ChargeLevel
{
    public float minHoldTime;
    public ActionDataSO action;
    public SkillStageSO stageOverride;
    public float damageMultiplier;
    public MotionProfileSO motionOverride;
}

/// <summary>冷却外部修改操作。</summary>
public enum CooldownOp : byte
{
    Reset = 0,
    ReduceFlat = 1,
    ReducePercent = 2,
    Set = 3,
}

/// <summary>输入槽位 → 技能绑定。</summary>
public enum SkillSlotType : byte
{
    [InspectorName("Skill_Primary_01")] Skill_Primary_01 = 0,
    [InspectorName("Skill_Primary_02")] Skill_Primary_02 = 1,
    [InspectorName("Skill_Primary_03")] Skill_Primary_03 = 2,
    [InspectorName("Secondary_04")] Secondary_04 = 3,
    [InspectorName("Ultimate_05")] Ultimate_05 = 4,
    [InspectorName("Ability_06")] Ability_06 = 5,
    [InspectorName("Ability_07")] Ability_07 = 6,
    [InspectorName("Ability_08")] Ability_08 = 7,
    [InspectorName("Ability_09")] Ability_09 = 8,
    [InspectorName("Ability_10")] Ability_10 = 9,
    [InspectorName("Ability_11")] Ability_11 = 10,
    [InspectorName("Ability_12")] Ability_12 = 11,
    [InspectorName("Ability_13")] Ability_13 = 12,
    [InspectorName("Ability_14")] Ability_14 = 13,
    [InspectorName("Ability_15")] Ability_15 = 14,
    [InspectorName("Ability_16")] Ability_16 = 15,
    [InspectorName("Ability_17")] Ability_17 = 16,
}

using System;

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
[Flags]
public enum SkillTrait : ulong
{
    None = 0,
    ResetCooldownOnKill = 1UL << 0,
    IgnoreDefense = 1UL << 1,
    Unstoppable = 1UL << 2,
    RefundCostOnMiss = 1UL << 3,
    ScaleWithAttackSpeed = 1UL << 4,
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
    Primary = 0,
    Secondary = 1,
    Ability1 = 2,
    Ability2 = 3,
    Dodge = 4,
    Ultimate = 5,
    Jump = 6,
}

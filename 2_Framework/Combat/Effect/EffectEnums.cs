/// <summary>180.1 — Effect/Buff 子系统共用枚举。</summary>

/// <summary>多源分类（180.1 §2.1）—— Modifier.Source 按 Kind 批量回收。</summary>
public enum EffectSourceKind : byte
{
    Skill = 0,
    Equipment = 1,
    Weapon = 2,
    Rune = 3,
    Item = 4,
    Aura = 5,
    Environment = 6,
    Boss = 7,
    Self = 8,
    Cheat = 9,
}

public enum EffectCategory : byte
{
    Buff = 0,
    Debuff = 1,
    Aura = 2,
    Curse = 3,
    Blessing = 4,
}

public enum DurationPolicy : byte
{
    Instant = 0,
    Timed = 1,
    Infinite = 2,
    WhileTagPresent = 3,
    WhileConditionTrue = 4,
}

/// <summary>180.1 §2.2 — Stack 5 策略。</summary>
public enum StackPolicy : byte
{
    /// <summary>同 Def 二次 Apply 仅刷新时长，不增层（LOL 神圣盾）。</summary>
    Refresh = 0,

    /// <summary>每次 Apply 都开新实例，独立计时（多次中毒 DOT 并行）。</summary>
    Independent = 1,

    /// <summary>实例数 ≤ MaxStack 新建；满后刷新最早（出血 3 层独立）。</summary>
    RefreshIndependent = 2,

    /// <summary>同实例 +1 层 + 刷新时长（雨2 蓝羽叠层）。</summary>
    AddStack = 3,

    /// <summary>同 Tag 中只保留数值最高（攻击 Buff 多档只取最强）。</summary>
    ReplaceLower = 4,
}

public enum StackMergeMode : byte
{
    /// <summary>Modifier 实际值 = Value × StackCount。</summary>
    Multiply = 0,

    /// <summary>Modifier 实际值 = Value × StackCount（与 Multiply 等价；命名习惯）。</summary>
    Linear = 1,

    /// <summary>Modifier 实际值 = Value × log2(1 + StackCount)。</summary>
    Logarithmic = 2,

    /// <summary>不放大 Modifier，仅刷新 / 计层。</summary>
    NoEffect = 3,
}

/// <summary>EffectInstance 离场原因。</summary>
public enum EffectRemoveReason : byte
{
    Expired = 0,
    Cleansed = 1,
    SourceRevoked = 2,
    Replaced = 3,
    ManualRemove = 4,
    SceneUnload = 5,
}

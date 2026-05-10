/// <summary>
/// 离散输入经“语义化”后的意图种类（不要用字符串状态名做分支）。
/// </summary>
public enum GameplayIntentKind : byte
{
    None = 0,
    Jump = 1,
    LightAttack = 2,
    HeavyAttack = 3,
    Dodge = 4,
    /// <summary>直线爆发位移（原 Shift「冲刺」语义，走 ActionDataSO，非连续 Run）。</summary>
    SwordDash = 5,

    /// <summary>序列化占位（历史值为旧蓄力枚举）；禁止入队。</summary>
    UnusedLegacy6 = 6,

    /// <summary>技能槽 Ability_06（对应 <see cref="SkillSlotType.Ability_06"/>）。</summary>
    CastAbility1 = 7,

    /// <summary>技能槽 Ultimate_05（对应 <see cref="SkillSlotType.Ultimate_05"/>）。</summary>
    CastUltimate = 8,

    /// <summary>左键连段（对应 <see cref="SkillSlotType.Skill_Primary_02"/>）。</summary>
    ComboAttack = 9,

    /// <summary>左键蓄力（对应 <see cref="SkillSlotType.Skill_Primary_03"/>）。</summary>
    ChargeAttack = 10,

    /// <summary>Shift 技能槽（对应 <see cref="SkillSlotType.Ability_07"/>）。</summary>
    CastAbility7 = 11,

    /// <summary>Space 技能槽（对应 <see cref="SkillSlotType.Ability_08"/>）。</summary>
    CastAbility8 = 12,

    Secondary_Combat = HeavyAttack,
    Ultimate_05 = CastUltimate,
    Ability_06 = CastAbility1,
    Ability_07 = CastAbility7,
    Ability_08 = CastAbility8,
    Ability_09 = 13,
    Ability_10 = 14,
    Ability_11 = 15,
    Ability_12 = 16,
    Ability_13 = 17,
    Ability_14 = 18,
    Ability_15 = 19,
    Ability_16 = 20,
    Ability_17 = 21,
}

/// <summary>
/// 一条带时间戳的语义意图。
/// - <see cref="TimeStamp"/>：入队时间，用于调试与优先级扩展。
/// - <see cref="ExpireTime"/>：过期时间；过期后由缓冲队列丢弃，实现输入缓冲窗口。
/// - 标签字段：由工厂在入队时填好，供 <see cref="TransitionResolver"/> 做“只查标签”的过滤。
/// </summary>
public struct GameplayIntent
{
    public GameplayIntentKind Kind;

    /// <summary>入队时的 Time.time。</summary>
    public float TimeStamp;

    /// <summary>超过该 Time.time 后意图作废。</summary>
    public float ExpireTime;

    /// <summary>上下文中必须全部具备的标签（ulong 位）。</summary>
    public ulong RequiredAllTags;

    /// <summary>若非 0：上下文中至少命中其中一位。</summary>
    public ulong RequiredAnyTags;

    /// <summary>若上下文中命中任意禁止位，则意图非法。</summary>
    public ulong ForbiddenTags;

    /// <summary>Ability 轨（<see cref="EntityCapabilityTag"/>）须全部具备；0 表示不检查。</summary>
    public ulong RequiredAllAbilityTags;

    /// <summary>Ability 轨禁止位（如未来沉默技能）。</summary>
    public ulong ForbiddenAbilityTags;

    /// <summary>可选：注入到 Action 支柱的动作资产（可为 null，走代码内默认动作）。</summary>
    public ActionDataSO Action;

    /// <summary>主攻击抬起时的累计按住时长（秒）；其它意图保持 0。</summary>
    public float PrimaryHoldDurationSeconds;

    /// <summary>交互/副攻键抬起时的按住时长（秒）；供 Secondary 槽 Charge 等扩展。</summary>
    public float SecondaryHoldDurationSeconds;

    /// <summary>是否携带明确槽位（Phase B：槽位优先，Kind 兼容回退）。</summary>
    public bool HasSkillSlot;

    /// <summary>明确的技能槽位语义（当 <see cref="HasSkillSlot"/> 为真时优先使用）。</summary>
    public SkillSlotType SkillSlot;

    public static GameplayIntent Create(
        GameplayIntentKind kind,
        float time,
        float bufferSeconds,
        ulong requiredAll,
        ulong requiredAny,
        ulong forbidden,
        ActionDataSO action = null,
        ulong requiredAllAbility = 0UL,
        ulong forbiddenAbility = 0UL,
        float primaryHoldDurationSeconds = 0f,
        float secondaryHoldDurationSeconds = 0f,
        bool hasSkillSlot = false,
        SkillSlotType skillSlot = default)
    {
        return new GameplayIntent
        {
            Kind = kind,
            TimeStamp = time,
            ExpireTime = time + bufferSeconds,
            RequiredAllTags = requiredAll,
            RequiredAnyTags = requiredAny,
            ForbiddenTags = forbidden,
            RequiredAllAbilityTags = requiredAllAbility,
            ForbiddenAbilityTags = forbiddenAbility,
            Action = action,
            PrimaryHoldDurationSeconds = primaryHoldDurationSeconds,
            SecondaryHoldDurationSeconds = secondaryHoldDurationSeconds,
            HasSkillSlot = hasSkillSlot,
            SkillSlot = skillSlot,
        };
    }
}

/// <summary>
/// 语义意图种类 — 仅描述「玩家想做哪一件抽象事」，不携带技能 / Combo / Charge 等具体语义。
///
/// ═══ 设计契约（Ver4.3.6+）═══
///   · 入口语义全部用 Skill_Entry_NN（NN 对应 <see cref="SkillEntrySlot"/>）；
///     旧的 LightAttack/ChargeAttack/ComboAttack 等"自带技能语义"枚举已彻底删除。
///   · Jump 仍保留为独立 Kind — 它跨支柱（→ Airborne），不属于 Skill 路由。
///   · 任何"方向 / 修饰键 / 蓄力时长"信息一律由 <see cref="GameplayIntent"/> 数据字段携带，不污染本枚举。
/// </summary>
public enum GameplayIntentKind : byte
{
    None = 0,
    Jump = 1,
    /// <summary>WASD 走/跑 — Locomotion 车道；仅在 Action 后摇窗口放行时入队（157.2）。</summary>
    Move = 2,
    /// <summary>220.6.1：由 FeedbackRouter 生成的受击动作语义。</summary>
    HitReact = 3,

    Skill_Entry_01 = 10,
    Skill_Entry_02 = 11,
    Skill_Entry_03 = 12,
    Skill_Entry_04 = 13,
    Skill_Entry_05 = 14,
    Skill_Entry_06 = 15,
    Skill_Entry_07 = 16,
    Skill_Entry_08 = 17,
    Skill_Entry_09 = 18,
    Skill_Entry_10 = 19,
    Skill_Entry_11 = 20,
    Skill_Entry_12 = 21,
    Skill_Entry_13 = 22,
    Skill_Entry_14 = 23,
    Skill_Entry_15 = 24,
    Skill_Entry_16 = 25,
    Skill_Entry_17 = 26,
}

/// <summary>
/// 一条带时间戳的语义意图（缓冲队列单元）。
/// </summary>
public struct GameplayIntent
{
    public GameplayIntentKind Kind;

    /// <summary>入队时的 Time.time。</summary>
    public float TimeStamp;

    /// <summary>超过该 Time.time 后意图作废。</summary>
    public float ExpireTime;

    /// <summary>上下文 State 轨：必须全部具备的标签（ulong 位）。</summary>
    public ulong RequiredAllTags;

    /// <summary>State 轨：至少命中其中一位。0 表示不检查。</summary>
    public ulong RequiredAnyTags;

    /// <summary>State 轨：禁止位 — 任一命中即非法。</summary>
    public ulong ForbiddenTags;

    /// <summary>Ability 轨须全部具备的位（沉默/封技检测）；0 表示不检查。</summary>
    public ulong RequiredAllAbilityTags;

    /// <summary>Ability 轨禁止位。</summary>
    public ulong ForbiddenAbilityTags;

    /// <summary>本意图绑定的入口槽位 — Skill_Entry_NN 必填，Jump 等可保持 default。</summary>
    public SkillEntrySlot EntrySlot;

    /// <summary>触发按键按住时长（秒，松开瞬间写入）。Charge / HoldRelease 路由依据本字段决定释放档位。</summary>
    public float HoldDurationSeconds;

    /// <summary>WASD 缓冲方向（单位向量；中性时为 zero）。</summary>
    public UnityEngine.Vector2 MoveBuffered;

    /// <summary>缓冲方向是否在 InputModifierBuffer 有效窗口内。</summary>
    public bool MoveBufferValid;

    // ═══ Ver4.3.7+ Input Semantic Layer 字段（由 InputSemanticResolver 单点写入） ═══

    /// <summary>
    /// 输入语义类型 — 替代下游"再次解析 hold 时间"。
    /// 默认 <see cref="InputSemanticType.None"/> 表示沿用旧路径（向后兼容）。
    /// Phase C 之后 SkillEntryService 改读本字段决策；当前与 <see cref="HoldDurationSeconds"/> 双轨平行。
    /// </summary>
    public InputSemanticType Semantic;

    /// <summary>
    /// 方向轴（仅 <see cref="InputSemanticType.Directional"/> 时有意义；与 <see cref="MoveBuffered"/> 互补：
    /// MoveBuffered 是 raw 缓冲向量；DirectionAxis 是 Resolver 解析后离散化的 4/8 方向单位向量）。
    /// </summary>
    public UnityEngine.Vector2 DirectionAxis;

    /// <summary>
    /// Combo 段位序号（仅 <see cref="InputSemanticType.Combo"/> 时有意义） — 由 Resolver 维护，
    /// 让 SkillEntryService 直接拿到段位无需自行维护窗口。
    /// </summary>
    public int ComboIndex;

    /// <summary>
    /// 组合键修饰槽位（仅 <see cref="InputSemanticType.Chord"/> 时有意义）。
    /// <see cref="SkillEntrySlot.Any"/> 表示无修饰键。
    /// </summary>
    public SkillEntrySlot ModifierSlot;

    /// <summary>220.6.1：ReactionSet 的稳定路线键，仅 HitReact 使用。</summary>
    public string ReactionRouteId;

    /// <summary>220.6.1：产生本次受击计划的 CombatResolvedEvent id。</summary>
    public ulong ReactionSourceEventId;

    /// <summary>220.6.1 C4：Resolver 快照的受击打断策略。</summary>
    public ReactionInterruptDisposition ReactionInterruptDisposition;

    /// <summary>220.6.1 C4：本次计划解析时目标是否带 SuperArmor。</summary>
    public bool ReactionSuperArmor;

    public static GameplayIntent ForEntry(
        SkillEntrySlot slot,
        float time,
        float bufferSeconds,
        ulong requiredAll,
        ulong requiredAny,
        ulong forbidden,
        ulong requiredAllAbility = 0UL,
        ulong forbiddenAbility = 0UL,
        float holdSeconds = 0f,
        UnityEngine.Vector2 moveBuffered = default,
        bool moveBufferValid = false)
    {
        return new GameplayIntent
        {
            Kind = SkillEntrySlotToIntentKind(slot),
            TimeStamp = time,
            ExpireTime = time + bufferSeconds,
            RequiredAllTags = requiredAll,
            RequiredAnyTags = requiredAny,
            ForbiddenTags = forbidden,
            RequiredAllAbilityTags = requiredAllAbility,
            ForbiddenAbilityTags = forbiddenAbility,
            EntrySlot = slot,
            HoldDurationSeconds = holdSeconds,
            MoveBuffered = moveBuffered,
            MoveBufferValid = moveBufferValid,
        };
    }

    public static GameplayIntent ForJump(float time, float bufferSeconds, ulong requiredAll, ulong forbidden)
    {
        return new GameplayIntent
        {
            Kind = GameplayIntentKind.Jump,
            TimeStamp = time,
            ExpireTime = time + bufferSeconds,
            RequiredAllTags = requiredAll,
            RequiredAnyTags = 0UL,
            ForbiddenTags = forbidden,
            RequiredAllAbilityTags = 0UL,
            ForbiddenAbilityTags = 0UL,
            EntrySlot = default,
        };
    }

    public static GameplayIntent ForMove(
        float time,
        float bufferSeconds,
        UnityEngine.Vector2 moveBuffered,
        bool moveBufferValid,
        ulong forbidden = 0UL)
    {
        return new GameplayIntent
        {
            Kind = GameplayIntentKind.Move,
            TimeStamp = time,
            ExpireTime = time + bufferSeconds,
            RequiredAllTags = 0UL,
            RequiredAnyTags = 0UL,
            ForbiddenTags = forbidden,
            RequiredAllAbilityTags = 0UL,
            ForbiddenAbilityTags = 0UL,
            EntrySlot = default,
            MoveBuffered = moveBuffered,
            MoveBufferValid = moveBufferValid,
        };
    }

    public static GameplayIntent ForHitReact(
        string reactionRouteId,
        ulong sourceEventId,
        float time,
        float bufferSeconds,
        ReactionInterruptDisposition interruptDisposition,
        bool superArmor)
    {
        return new GameplayIntent
        {
            Kind = GameplayIntentKind.HitReact,
            TimeStamp = time,
            ExpireTime = time + UnityEngine.Mathf.Max(0.01f, bufferSeconds),
            EntrySlot = default,
            ReactionRouteId = reactionRouteId,
            ReactionSourceEventId = sourceEventId,
            ReactionInterruptDisposition = interruptDisposition,
            ReactionSuperArmor = superArmor,
        };
    }

    /// <summary>槽位 ↔ Kind 一对一映射（SoA 友好；越界返回 None）。</summary>
    public static GameplayIntentKind SkillEntrySlotToIntentKind(SkillEntrySlot slot)
    {
        var idx = (int)slot;
        if (idx < 0 || idx >= SkillEntrySlots.TableSize)
        {
            return GameplayIntentKind.None;
        }

        return (GameplayIntentKind)((int)GameplayIntentKind.Skill_Entry_01 + idx);
    }

    /// <summary>Kind → 槽位反查；非 Skill_Entry_NN 返回 false。</summary>
    public static bool TryIntentKindToSlot(GameplayIntentKind kind, out SkillEntrySlot slot)
    {
        var lo = (int)GameplayIntentKind.Skill_Entry_01;
        var hi = lo + SkillEntrySlots.TableSize - 1;
        var raw = (int)kind;
        if (raw < lo || raw > hi)
        {
            slot = default;
            return false;
        }

        slot = (SkillEntrySlot)(raw - lo);
        return true;
    }
}

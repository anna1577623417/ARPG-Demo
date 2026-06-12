using UnityEngine;

/// <summary>
/// 玩家实体（Ver4.3.6+）— 纯能力执行器。
///
/// ═══ 职责 ═══
///   Player 只负责"做"：移动、跳跃、重力、地面检测、动作占用、标签持有。
///   "何时做"由 4 支柱状态机决定（Locomotion / Airborne / Action / Dead）。
///   "做什么"由 SkillEntryDefinition → SkillRouteDefinition → SkillStageDefinition → ActionDataSO 数据资产驱动。
///
/// ═══ 决策链路 ═══
///   InputReader → PlayerController（语义意图）→ IntentBuffer
///     → PlayerStateManager (TransitionResolver + SkillEntryService.TryResolveForIntent)
///     → PlayerActionState.OnEnter → 读取 SkillEntries.ActiveRoute.Stage.Action → 推进时间轴
/// </summary>
[RequireComponent(typeof(PlayerStateManager))]
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(PlayerKCCMotor))]
public class Player : Entity<Player>, IEntity, IDamageable, IEffectReceiver
{
    // ─── 输入 ───
    [Header("Input")]
    [SerializeField] InputReader inputReader;

    // ─── 移动参数（158.2 L3+：legacy fallback；Tuning 配置后由 LocomotionTuningSO 接管）───
    [Header("Movement (Legacy — Tuning 接管前的回落值；零回归保留)")]
    [Tooltip("移动加速度（手感硬度）。Tuning 接管后由 LocomotionTuningSO.GroundAcceleration 替代。")]
    [SerializeField] float moveAcceleration = 18f;
    [Tooltip("停止减速度。Tuning 接管后由 LocomotionTuningSO.GroundDeceleration 替代。")]
    [SerializeField] float moveDeceleration = 22f;
    [Tooltip("跳跃初速。Tuning 接管后由 LocomotionTuningSO.JumpForce 替代。")]
    [SerializeField] float jumpForce = 12f;
    [Tooltip("空中控制乘子 (0..1)。Tuning 接管后由 LocomotionTuningSO.AirMoveMultiplier 替代。")]
    [SerializeField] float airMoveMultiplier = 0.6f;

    // ─── 攻击参数 ───
    [Header("Attack")]
    [Tooltip("攻击状态时长。")]
    [SerializeField] float attackDuration = 0.45f;
    [Tooltip("攻击/潜行时的移动衰减。")]
    [SerializeField, Range(0f, 1f)] float walkSpeedMultiplier = 0.55f;

    // ─── Locomotion 行为资产（158.2 L1：仅占位，不通电）───
    [Header("Locomotion (158.2)")]
    [Tooltip("L1：玩家级 Locomotion 行为资产 —— 状态注册 / 动画绑定 / Tuning 引用。\n" +
             "本字段在 L1 仅作为 Inspector 可编辑占位，Resolver 与 Tuning 公式接入分别在 L2 / L3 完成。\n" +
             "为空时一切沿用旧路径（PlayerAnimManagerSO + Movement 旧字段），零回归。")]
    [SerializeField] LocomotionProfile locomotionProfile;

    // ─── Skill Entry 装配 ───
    [Header("Skill Entries (Ver4.3.6+)")]
    [Tooltip("槽位 → SkillEntryDefinition 装配。运行时由 SkillEntryService 接管。")]
    [SerializeField] SkillEntryLoadoutSO skillEntryLoadout;

    [Tooltip("Phase F+：玩家级输入语义阈值（TapThreshold/ComboWindow/EnableDirectional）。" +
             "未指派或某槽位未在 SO 中配置时回落到 ChargeRoute / ComboRoute 资产字段。")]
    [SerializeField] SemanticConfigSO semanticConfig;

    // ─── Debug ───
    [Header("Debug")]
    [SerializeField] bool debugInterruptFlow;
    [Tooltip("Combat Graph 专向 Log：Console 过滤 [SkillRoute][Graph] 与 [CombatGraph][Finisher]；不含 Resolve/Combo/Route 全量 Flow。")]
    [SerializeField] bool debugSkillRoute;
    [Tooltip("四向站立闪避链 [SkillRoute][Dodge4]；默认关。")]
    [SerializeField] bool debugSkillRouteDodge4;
    [Tooltip("四向武侠翻滚链 [SkillRoute][Roll4]；默认关，调试 Wuxia Group 时开。")]
    [SerializeField] bool debugSkillRouteRoll4;
    [Tooltip("能力准入 [Ability] route gate / loadout map（与 Route.abilityGateRules 对应）。")]
    [SerializeField] bool debugSkillAbility;
    [Tooltip("158.2 Locomotion Resolver / ControlOwner / Tuning 决策日志（L2-L5）。")]
    [SerializeField] bool debugLocomotion;
    [Tooltip("162.1 Locomotion 输入/移动/转身节流 Trace（Console 过滤 [Loco]）；恶性移动故障时优先开启。")]
    [SerializeField] bool debugLocomotionTrace;
    [Tooltip("159.1 L2/L3：Play 验收用 — 模拟 LockOn 以启用 StrafeLocomotion 解析；LockOn 切片接入后删除。")]
    [SerializeField] bool debugLockOnLocomotion;

    // ─── 运行时 ───
    PlayerStateManager m_stateManager;
    PlayerKCCMotor m_motor;
    SkillEntryService m_skillEntries;
    InputSemanticResolver m_inputSemantic;
    float m_attackTimer;
    Vector3 m_movementIntent;
    bool m_runIntent;
    float m_runLatchEndTime;
    bool m_isInitialized;

    GameplayTagContainer m_gameplayTags;
    public ref GameplayTagContainer Tags => ref m_gameplayTags;
    public ref GameplayTagMask GameplayTags => ref m_gameplayTags.State;

    public readonly GameplayIntentBuffer IntentBuffer = new GameplayIntentBuffer(16);

    GameplayIntentKind m_pendingActionKind;
    ActionDataSO m_pendingAction;
    bool m_pendingActionArmed;
    bool m_jumpRequestedByIntent;
    ActionDataSO m_graphContextAction;
    LocomotionStateId m_lastActionEndStateHint = LocomotionStateId.None;

    /// <summary>157.2/157.3 — 无 ActiveRoute 时 Graph Resolve 的 Locomotion 上下文 Action（单点写入）。</summary>
    public ActionDataSO GraphContextAction => m_graphContextAction;

    public LocomotionGraphContextBinding LocomotionGraphContext =>
        skillEntryLoadout != null ? skillEntryLoadout.LocomotionGraphContext : default;

    TurnInfo m_currentTurnInfo;
    LocomotionPresentationSnapshot m_locoPresentation;
    IGameModeMovementContext m_movementContext;

    // ─── 公开属性 ───
    public InputReader InputReader => inputReader;
    public PlayerStateManager States => m_stateManager;
    public SkillEntryService SkillEntries => m_skillEntries;
    public SkillEntryLoadoutSO SkillEntryLoadout => skillEntryLoadout;
    public CombatGraphAsset CombatFlowGraph => skillEntryLoadout != null ? skillEntryLoadout.CombatFlow : null;
    /// <summary>158.2 L1：玩家级 Locomotion 行为资产；L2 之前仅占位，无运行时消费方。</summary>
    public LocomotionProfile LocomotionProfile => locomotionProfile;

    /// <summary>165.1 L3：Locomotion End Action 退出后供 LocomotionState 读取并清除。</summary>
    public LocomotionStateId ConsumeLastActionEndStateHint()
    {
        var hint = m_lastActionEndStateHint;
        m_lastActionEndStateHint = LocomotionStateId.None;
        return hint;
    }

    internal void SetLastActionEndStateHint(LocomotionStateId id) => m_lastActionEndStateHint = id;

    /// <summary>158.2 L2：当前位移/朝向控制权归属（仅观测；由 PlayerStateManager 末尾自动写入）。</summary>
    public ControlOwner CurrentControlOwner { get; internal set; } = ControlOwner.Locomotion;
    public InputSemanticResolver InputSemantic => m_inputSemantic;
    public bool DebugInterruptFlow => debugInterruptFlow;
    public bool DebugSkillRoute => debugSkillRoute;
    public bool DebugSkillRouteDodge4 => debugSkillRouteDodge4;
    public bool DebugSkillRouteRoll4 => debugSkillRouteRoll4;
    public bool DebugSkillAbility => debugSkillAbility;
    public bool DebugLocomotion => debugLocomotion;
    public bool DebugLocomotionTrace => debugLocomotionTrace;

    public Vector3 PlanarVelocity => m_motor != null ? m_motor.PlanarVelocity : Vector3.zero;
    public float VerticalSpeed => m_motor != null ? m_motor.VerticalSpeed : 0f;
    public bool IsGrounded => m_motor != null && m_motor.IsGrounded;
    public bool IsAttacking => m_attackTimer > 0f;

    public float Stamina => Resources.GetCurrent(ResourceType.Stamina);
    public float StaminaMax => Resources.GetMax(ResourceType.Stamina);
    public float Mana => Resources.GetCurrent(ResourceType.MP);
    public float ManaMax => Resources.GetMax(ResourceType.MP);

    public float AttackDuration => attackDuration;
    /// <summary>158.2 L3：JumpForce 查询；Tuning 优先 → Player 旧字段。</summary>
    public float JumpForce => locomotionProfile != null && locomotionProfile.Tuning != null
        ? locomotionProfile.Tuning.JumpForce
        : jumpForce;
    /// <summary>158.2 L3：AirMoveMultiplier 查询；Tuning 优先 → Player 旧字段。</summary>
    public float AirMoveMultiplier => locomotionProfile != null && locomotionProfile.Tuning != null
        ? locomotionProfile.Tuning.AirMoveMultiplier
        : airMoveMultiplier;
    /// <summary>158.2 L3：下落重力倍率；Tuning 优先 → 1（无 Profile 时与旧行为一致）。</summary>
    public float FallGravityScale => locomotionProfile != null && locomotionProfile.Tuning != null
        ? locomotionProfile.Tuning.FallGravityScale
        : 1f;
    public float WalkSpeedMultiplier => walkSpeedMultiplier;

    public bool HasMovementIntent => m_movementIntent.sqrMagnitude > 0.0001f;
    public bool WantsRun => m_runIntent;
    public bool RunLatchActive => Time.time < m_runLatchEndTime;
    public bool WantsWalk => HasMovementIntent && !m_runIntent;
    public Vector3 MovementIntent => m_movementIntent;
    public TurnInfo CurrentTurnInfo => m_currentTurnInfo;
    /// <summary>159.1 L2+：Resolver 连续 Clip 表现快照（Strafe 等）。</summary>
    public LocomotionPresentationSnapshot LocomotionPresentation => m_locoPresentation;
    /// <summary>159.1 L2：LockOn 信号；当前为 debug 占位，LockOn 切片接入后改读真实目标锁定。</summary>
    public bool IsLockedOn => debugLockOnLocomotion;

    public void ActivateRunLatch(float seconds) => m_runLatchEndTime = Time.time + Mathf.Max(0.01f, seconds);
    public void SetTurnInfo(in TurnInfo info) => m_currentTurnInfo = info;
    public void SetLocomotionPresentation(in LocomotionPresentationSnapshot snapshot) => m_locoPresentation = snapshot;

    internal void InjectMovementContext(IGameModeMovementContext context) => m_movementContext = context;

    /// <summary>MotionProfile 局部轴 → 世界水平前向（Z 轴）；不读 MovementIntent。</summary>
    public Vector3 ResolveMotionPlanarForward(MotionSpace space)
        => MotionSpaceBasis.ResolvePlanarForward(this, m_movementContext, space);

    public float NormalizedSpeed
    {
        get
        {
            var cap = RuntimeStats.RunSpeed;
            return cap > 0.01f ? Mathf.Clamp01(PlanarVelocity.magnitude / cap) : 0f;
        }
    }

    Transform IEntity.Transform => transform;
    IReadOnlyStatSet IEntity.Stats => Stats;
    IResourcePool IEntity.Resources => Resources;
    GameplayTagContainer ITagOwner.Tags => m_gameplayTags;
    bool IEntity.IsAlive => !IsDead;
    IBuffStack IEffectReceiver.BuffStack => Buffs;
    IReadOnlyStatSet IEffectReceiver.Stats => Stats;
    IResourcePool IEffectReceiver.Resources => Resources;

    // ─── 生命周期 ───

    protected override void Awake()
    {
        base.Awake();
        if (statsBlueprint is PlayerStatsSO ps)
        {
            ps.ApplyLegacyStandaloneMaxStaminaToStats(Stats);
        }
        EnsurePlayerDefaultResourceStats();
        Init();
    }

    protected override void OnEnable() { base.OnEnable(); }

    void EnsurePlayerDefaultResourceStats()
    {
        if (Stats.Get(StatType.MaxStamina) < 1f)
        {
            Stats.SetBase(StatType.MaxStamina, 100f);
        }
    }

    void Init()
    {
        if (m_isInitialized) return;
        m_isInitialized = true;

        m_stateManager = GetComponent<PlayerStateManager>();
        m_motor = GetComponent<PlayerKCCMotor>();
        if (m_motor != null)
        {
            m_motor.Bind(this, m_stateManager, debugInterruptFlow);
            SyncLocomotionMotorTuning();
            m_motor.RefreshInitialGroundedState();
        }

        if (Resources is ResourcePool pool)
        {
            pool.RegisterSlot(
                ResourceType.Stamina,
                maxProvider: () => Stats.Get(StatType.MaxStamina),
                initialCurrent: Stats.Get(StatType.MaxStamina));
            pool.RegisterSlot(
                ResourceType.MP,
                maxProvider: () => Stats.Get(StatType.MaxMana),
                initialCurrent: Stats.Get(StatType.MaxMana));
        }

        m_skillEntries = new SkillEntryService(this);
        m_skillEntries.Rebuild(skillEntryLoadout);

        // Phase B：Semantic Resolver 初始化 + 从 Loadout 拉每槽位阈值
        m_inputSemantic = new InputSemanticResolver(this);
        RefreshSemanticConfigFromLoadout();
    }

    /// <summary>
    /// Phase F：先读 SemanticConfigSO（玩家级独立配置），未配项再回落 ChargeRoute / ComboRoute / PrimaryGroup 存在性。
    /// 任一槽位若两路都拿不到值，对应字段为 0（Resolver 自动跳过该语义分流）。
    /// </summary>
    bool LoadoutHasDirectionalContext(SkillEntrySlot slot)
    {
        var groups = skillEntryLoadout?.ContextGroups;
        if (groups == null)
        {
            return false;
        }

        for (var i = 0; i < groups.Length; i++)
        {
            var g = groups[i];
            if (g == null || !g.RequireDirectional)
            {
                continue;
            }

            if (g.RequiredSlot == SkillEntrySlot.Any || g.RequiredSlot == slot)
            {
                return true;
            }
        }

        return false;
    }

    void RefreshSemanticConfigFromLoadout()
    {
        if (m_inputSemantic == null || skillEntryLoadout?.Bindings == null) return;
        for (var i = 0; i < skillEntryLoadout.Bindings.Length; i++)
        {
            var b = skillEntryLoadout.Bindings[i];
            var entry = b.Entry;
            if (entry == null) continue;

            InputSemanticResolver.PerSlotConfig cfg;
            if (semanticConfig != null && semanticConfig.TryResolve(b.Slot, out cfg))
            {
                // SemanticConfigSO 命中 — 玩家级阈值为权威来源。
            }
            else
            {
                cfg = new InputSemanticResolver.PerSlotConfig
                {
                    TapThreshold = entry.ChargeRoute != null ? entry.ChargeRoute.TapThreshold : 0f,
                    ComboWindow = entry.ComboRoute != null ? entry.ComboRoute.ComboSessionResetTime : 0f,
                    EnableDirectional = entry.PrimaryGroup != null
                        || LoadoutHasDirectionalContext(b.Slot),
                };
            }

            // 116.1：边窗口注入 Resolver（与 SkillEntryService 双点校验，打通全链路）。
            if (entry.ComboRoute is ComboRouteDefinition comboDef)
            {
                cfg.ComboChainLength = comboDef.ChainLength;
                cfg.ComboEdgeTimings = comboDef.BuildTransitionTimingsForResolver();
                if (cfg.ComboWindow <= 0.0001f)
                {
                    cfg.ComboWindow = comboDef.ComboSessionResetTime;
                }
            }
            else
            {
                cfg.ComboChainLength = 0;
                cfg.ComboEdgeTimings = null;
            }

            m_inputSemantic.ConfigureSlot(b.Slot, in cfg);
            m_skillEntries?.SyncComboSemanticConfig(b.Slot);
        }
    }

    // ─── 帧上下文 ───

    /// <summary>单点构建战斗上下文；hitConfirmed 由 SkillEntryService 在 Stage 内维护。</summary>
    public CombatContextSnapshot BuildCombatContext(bool hitConfirmedThisStage, Vector2 moveOverride, bool moveOverrideValid)
    {
        var move = moveOverrideValid ? moveOverride : Vector2.zero;
        if (!moveOverrideValid && inputReader != null)
        {
            move = inputReader.MoveModifierBuffer.GetBufferedMove(Time.time);
            moveOverrideValid = move.sqrMagnitude > 0.0001f;
        }

        var dirType = moveOverrideValid
            ? InputChordResolver.Resolve(move)
            : DirectionalRouteType.Forward;
        return new CombatContextSnapshot
        {
            IsAirborne = !IsGrounded,
            MoveDirection = moveOverrideValid
                ? MoveDirection8Extensions.FromDirectional(dirType)
                : MoveDirection8.None,
            HitConfirmedThisStage = hitConfirmedThisStage,
            SnapshotTime = Time.time,
        };
    }

    public FrameContext BuildFrameContext(float deltaTime)
    {
        var attackHeld = inputReader != null && inputReader.IsAttackHeld;
        var planar = PlanarVelocity;
        return new FrameContext
        {
            Time = Time.time,
            DeltaTime = deltaTime,
            IsGrounded = IsGrounded,
            PlanarVelocity = planar,
            CurrentPlanarSpeed = planar.magnitude,
            VerticalSpeed = VerticalSpeed,
            CurrentTags = GameplayTags,
            CurrentAbilityTags = m_gameplayTags.Ability,
            StaminaCurrent = Stamina,
            StaminaMax = StaminaMax,
            IsPrimaryAttackHeld = attackHeld,
        };
    }

    public void EnqueueGameplayIntent(in GameplayIntent intent) => IntentBuffer.Enqueue(intent);

    /// <summary>157.2/157.3 — 写入 Graph 上下文 Action（Airborne 相位 / 落地 JumpLand 等）。</summary>
    public void SetGraphContextAction(ActionDataSO action, string reason = null)
    {
        if (ReferenceEquals(m_graphContextAction, action))
        {
            return;
        }

        m_graphContextAction = action;
        if (debugSkillRoute && action != null)
        {
            var part = ActionIntentRouting.ResolveGraphParticipation(action);
            SkillRouteDebug.LogGraph(this, $"Ctx SET action={action.name} C={part} reason={reason ?? "-"}");
        }
    }

    public void ClearGraphContextAction(string reason = null)
    {
        if (m_graphContextAction == null)
        {
            return;
        }

        if (debugSkillRoute)
        {
            SkillRouteDebug.LogGraph(
                this,
                m_graphContextAction != null
                    ? $"Ctx CLEAR was={m_graphContextAction.name} reason={reason ?? "-"}"
                    : $"Ctx CLEAR (none) reason={reason ?? "-"}");
        }

        m_graphContextAction = null;
    }

    /// <summary>157.2 — Action 期 Move 入队时探测当前段打断窗口。</summary>
    public bool TryGetActiveActionInterruptProbe(out ActionDataSO action, out float normalizedTime)
    {
        if (States?.Current is PlayerActionState actionState
            && actionState.TryGetInterruptProbe(out action, out normalizedTime))
        {
            return true;
        }

        action = null;
        normalizedTime = 0f;
        return false;
    }

    // ─── PendingAction（Locomotion/Airborne 切到 Action 前的待播）───

    public void ArmPendingAction(GameplayIntentKind kind, ActionDataSO action)
    {
        m_pendingActionArmed = true;
        m_pendingActionKind = kind;
        m_pendingAction = action;
    }

    public ActionDataSO PeekPendingAction() => m_pendingAction;

    /// <summary>仲裁未消费意图时丢弃已装配的 PendingAction，避免下帧误播旧段。</summary>
    public void ClearPendingAction()
    {
        m_pendingActionArmed = false;
        m_pendingAction = null;
    }

    public bool TryTakePendingAction(out GameplayIntentKind kind, out ActionDataSO action)
    {
        if (!m_pendingActionArmed)
        {
            kind = GameplayIntentKind.None;
            action = null;
            return false;
        }
        m_pendingActionArmed = false;
        kind = m_pendingActionKind;
        action = m_pendingAction;
        return true;
    }

    /// <summary>请求 Playables 播放 Action 主 Clip（首段进入 / MultiStage 段内换招）。</summary>
    public void RequestActionPresentation(
        GameplayIntentKind kind,
        ActionDataSO action,
        AnimationClip presentationClip = null)
    {
        if (action == null) return;
        PublishEvent(new PlayerActionPresentationRequestEvent(GetInstanceID(), kind, action, presentationClip));
    }

    /// <summary>164.1 L3：Locomotion 内 IsContinuousLocomotion Action 换片。</summary>
    public void RequestContinuousLocomotionPresentation(ActionDataSO action)
    {
        if (action == null || !action.IsContinuousLocomotion)
        {
            return;
        }

        PublishEvent(new PlayerContinuousLocomotionRequestEvent(GetInstanceID(), action));
    }

    /// <summary>Route 内 Stage 推进后由 SkillEntryService 调用，换段动画与 Motion。</summary>
    public void NotifyRouteStageAction(ActionDataSO action)
    {
        if (action == null || States?.Current is not PlayerActionState actionState)
        {
            return;
        }

        actionState.SwapToStageAction(this, action);
    }

    public void RequestJumpFromIntent() => m_jumpRequestedByIntent = true;

    public bool ConsumeJumpFromIntent()
    {
        if (!m_jumpRequestedByIntent) return false;
        m_jumpRequestedByIntent = false;
        return true;
    }

    public bool HasTag(GameplayTagMask mask)
    {
        var bits = mask.Value;
        return m_gameplayTags.State.HasAll(bits)
               || m_gameplayTags.Status.HasAll(bits)
               || m_gameplayTags.Ability.HasAll(bits)
               || m_gameplayTags.Mechanic.HasAll(bits)
               || m_gameplayTags.Faction.HasAll(bits);
    }

    // ─── IDamageable / IEffectReceiver ───

    public void TakeDamage(DamageInfo info)
    {
        var attacker = ResolveAttackerEntity(info.Source);
        var ctx = new CombatContext(
            attackerAttackPower: attacker != null ? attacker.Stats.Get(StatType.AttackPower) : 0f,
            defenderDefense: Stats.Get(StatType.Defense),
            defenderCurrentHP: Resources.GetCurrent(ResourceType.HP),
            defenderMaxHP: Resources.GetMax(ResourceType.HP),
            attackerTags: ResolveEntityStateTags(attacker),
            defenderTags: GameplayTags.Value);
        var hit = new HitContext(
            baseDamage: info.Amount,
            isCritical: false,
            criticalMultiplier: 1.5f,
            hitPoint: info.HitPoint);
        var result = DamagePipeline.Compute(in ctx, in hit);

        TakeDamage(result.FinalDamage, info.Source);
        if (IsDead) { States.Change<PlayerDeadState>(); }
    }

    public void ReceiveDamage(in DamageResult result, in CombatContext ctx)
    {
        TakeDamage(result.FinalDamage, this);
        if (IsDead) { States.Change<PlayerDeadState>(); }
    }

    static Entity ResolveAttackerEntity(GameObject source)
        => source == null ? null : source.GetComponentInParent<Entity>();

    static ulong ResolveEntityStateTags(Entity entity)
        => entity is Player p ? p.GameplayTags.Value : 0UL;

    // ─── 移动 ───

    public void SetMovementIntent(Vector3 worldDirection, bool wantsRun)
    {
        var planar = new Vector3(worldDirection.x, 0f, worldDirection.z);
        if (planar.sqrMagnitude > 1f) planar.Normalize();
        m_movementIntent = planar;
        m_runIntent = wantsRun && planar.sqrMagnitude > 0.0001f;
    }

    public Vector3 GetMovementDirectionOrForward()
        => HasMovementIntent ? m_movementIntent.normalized : Forward;

    /// <summary>
    /// 158.2 L3：地面 / 空中 Locomotion 位移结算入口。
    /// <para>速度公式（四级）：FinalSpeed = Stats.&lt;Walk/Run&gt;Speed × Tuning.&lt;Walk/Run&gt;Multiplier × externalSpeedMultiplier × (Buff 由 Stats 内部已合）。</para>
    /// <para>Tuning 缺失（locomotionProfile/Tuning 为 null）→ 完全沿用旧路径：Stats 速度 × external，加速度走 Player.moveAcceleration/moveDeceleration。零回归。</para>
    /// </summary>
    public void MoveByLocomotionIntent(float externalSpeedMultiplier, bool wantsRun)
    {
        var tuning = locomotionProfile != null ? locomotionProfile.Tuning : null;

        var input = m_movementIntent;
        var hasInput = input.sqrMagnitude > 0.0001f;
        var inputMag = hasInput ? Mathf.Clamp01(input.magnitude) : 0f;

        // 速度上限：Stats 基础速 × Tuning 倍率（缺失视作 1.0）
        var baseSpeed = wantsRun ? RuntimeStats.RunSpeed : RuntimeStats.WalkSpeed;
        var modeMult = wantsRun
            ? (tuning != null ? Mathf.Max(0f, tuning.RunMultiplier) : 1f)
            : (tuning != null ? Mathf.Max(0f, tuning.WalkMultiplier) : 1f);
        var tuningExternal = tuning != null ? Mathf.Max(0f, tuning.ExternalSpeedMultiplier) : 1f;
        var externalMult = Mathf.Max(0f, externalSpeedMultiplier) * tuningExternal;
        var speedCap = baseSpeed * modeMult * externalMult;
        var targetSpeed = hasInput ? speedCap * inputMag : 0f;

        // 加速度：Tuning > Player 旧字段
        var accel = ResolveMoveAcceleration(tuning, hasInput);

        var planar = PlanarVelocity;
        var currentSpeed = planar.magnitude;
        var newSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * Time.deltaTime);

        if (hasInput)
        {
            SetPlanarVelocity(input.normalized * newSpeed);
            SetMoveDirection(input);
            LookAtDirection(input);
        }
        else
        {
            var dir = currentSpeed > 0.01f ? planar.normalized : Vector3.zero;
            SetPlanarVelocity(dir * newSpeed);
        }
    }

    /// <summary>158.2 L3：加速度查找（Tuning 优先；缺失回落 Player 旧字段）。</summary>
    float ResolveMoveAcceleration(LocomotionTuningSO tuning, bool hasInput)
    {
        if (tuning != null)
        {
            return hasInput ? tuning.GroundAcceleration : tuning.GroundDeceleration;
        }
        return hasInput ? moveAcceleration : moveDeceleration;
    }

    public void StopMove()
    {
        var tuning = locomotionProfile != null ? locomotionProfile.Tuning : null;
        var decel = tuning != null ? tuning.GroundDeceleration : moveDeceleration;
        SetPlanarVelocity(Vector3.MoveTowards(PlanarVelocity, Vector3.zero, decel * Time.deltaTime));
    }

    public void ClearPlanarVelocity() => m_motor?.ClearPlanarVelocity();
    public void SetPlanarVelocity(Vector3 v) => m_motor?.SetPlanarVelocity(v);
    public void SetVerticalSpeed(float vy) => m_motor?.SetVerticalSpeed(vy);

    public void Jump()
    {
        // 158.2 L3：JumpForce Tuning 优先；缺失回落 Player.jumpForce
        var tuning = locomotionProfile != null ? locomotionProfile.Tuning : null;
        var force = tuning != null ? tuning.JumpForce : jumpForce;
        m_motor?.Jump(force);
        PublishEvent(new PlayerJumpEvent(GetInstanceID(), name));
    }

    /// <summary>158.2 L3：把 LocomotionTuning 物理参数写入 Motor（Init 时；Profile 热换后可再调）。</summary>
    public void SyncLocomotionMotorTuning()
    {
        m_motor?.SetFallGravityScale(FallGravityScale);
    }

    public void BeginAttack(float durationOverride = -1f)
    {
        m_attackTimer = durationOverride > 0f ? durationOverride : attackDuration;
        PublishEvent(new PlayerAttackStartedEvent(GetInstanceID(), name));
    }

    public void BeginAttackWithManualCompletion()
    {
        m_attackTimer = float.MaxValue;
        PublishEvent(new PlayerAttackStartedEvent(GetInstanceID(), name));
    }

    public void TickAttackTimer()
    {
        if (m_attackTimer <= 0f) return;
        m_attackTimer -= Time.deltaTime;
        if (m_attackTimer <= 0f)
        {
            m_attackTimer = 0f;
            PublishEvent(new PlayerAttackEndedEvent(GetInstanceID(), name));
        }
    }

    public void ForceEndAttackIfActive()
    {
        if (m_attackTimer <= 0f) return;
        m_attackTimer = 0f;
        PublishEvent(new PlayerAttackEndedEvent(GetInstanceID(), name));
    }

    // ─── KCC Motor forwarder ───

    public bool IsGravitySuspended => m_motor != null && m_motor.IsGravitySuspended;
    public void SuspendGravity() => m_motor?.SuspendGravity();
    public void ReleaseGravity() => m_motor?.ReleaseGravity();
    public void SetActionAirborneLock(bool locked) => m_motor?.SetActionAirborneLock(locked);
    public void BeginActionMotorSession() => m_motor?.BeginActionMotorSession();
    public void EndActionMotorSession() => m_motor?.EndActionMotorSession();
    public void ApplySimpleGravity() => m_motor?.ApplySimpleGravity();
    public void ApplyMotor(in MotorSolveContext ctx) => m_motor?.ApplyMotor(in ctx);
    public void ApplyMotorFromGameplayVelocity(Vector3 v, in MotorSolveContext ctx)
        => m_motor?.ApplyMotorFromGameplayVelocity(v, in ctx);

    public void ApplyMotorFromGameplayVelocity(
        Vector3 v,
        in MotorSolveContext ctx,
        MotionYAxisConfig yAxisConfig,
        bool useMotionComposer)
        => m_motor?.ApplyMotorFromGameplayVelocity(v, in ctx, yAxisConfig, useMotionComposer);
    public MotorSolveContext BuildActionMotorSolveContext()
        => m_motor != null ? m_motor.BuildActionMotorSolveContext() : MotorSolveContext.Locomotion;
    public void TeleportTo(Vector3 worldPos, bool forceAirborne = false)
        => m_motor?.TeleportTo(worldPos, forceAirborne);

    public bool TryProbeGroundHeight(Vector3 worldPos, float maxCastDistance, out float groundWorldY)
    {
        groundWorldY = worldPos.y;
        if (m_motor == null)
        {
            return false;
        }

        return m_motor.TryProbeGroundHeight(worldPos, maxCastDistance, out groundWorldY);
    }
}

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

    // ─── 移动参数 ───
    [Header("Movement")]
    [Tooltip("移动加速度（手感硬度）。")]
    [SerializeField] float moveAcceleration = 18f;
    [Tooltip("停止减速度。")]
    [SerializeField] float moveDeceleration = 22f;
    [Tooltip("跳跃初速。")]
    [SerializeField] float jumpForce = 12f;
    [Tooltip("空中控制乘子 (0..1)。")]
    [SerializeField] float airMoveMultiplier = 0.6f;

    // ─── 攻击参数 ───
    [Header("Attack")]
    [Tooltip("攻击状态时长。")]
    [SerializeField] float attackDuration = 0.45f;
    [Tooltip("攻击/潜行时的移动衰减。")]
    [SerializeField, Range(0f, 1f)] float walkSpeedMultiplier = 0.55f;

    // ─── Skill Entry 装配 ───
    [Header("Skill Entries (Ver4.3.6+)")]
    [Tooltip("槽位 → SkillEntryDefinition 装配。运行时由 SkillEntryService 接管。")]
    [SerializeField] SkillEntryLoadoutSO skillEntryLoadout;

    [Tooltip("Phase F+：玩家级输入语义阈值（TapThreshold/ComboWindow/EnableDirectional）。" +
             "未指派或某槽位未在 SO 中配置时回落到 ChargeRoute / ComboRoute 资产字段。")]
    [SerializeField] SemanticConfigSO semanticConfig;

    [Header("Combat Flow Graph (124.1)")]
    [SerializeField, Tooltip("装配后 SkillEntryService 优先经 Graph 解析 Space 翻滚 / 空中连段等边。")]
    CombatGraphAsset combatFlowGraph;

    // ─── Debug ───
    [Header("Debug")]
    [SerializeField] bool debugInterruptFlow;
    [Tooltip("技能 Route / HUD / Action 支柱诊断日志与 RouteHudDebugOverlay。")]
    [SerializeField] bool debugSkillRoute;

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

    TurnInfo m_currentTurnInfo;

    // ─── 公开属性 ───
    public InputReader InputReader => inputReader;
    public PlayerStateManager States => m_stateManager;
    public SkillEntryService SkillEntries => m_skillEntries;
    public SkillEntryLoadoutSO SkillEntryLoadout => skillEntryLoadout;
    public CombatGraphAsset CombatFlowGraph => combatFlowGraph;
    public InputSemanticResolver InputSemantic => m_inputSemantic;
    public bool DebugInterruptFlow => debugInterruptFlow;
    public bool DebugSkillRoute => debugSkillRoute;

    public Vector3 PlanarVelocity => m_motor != null ? m_motor.PlanarVelocity : Vector3.zero;
    public float VerticalSpeed => m_motor != null ? m_motor.VerticalSpeed : 0f;
    public bool IsGrounded => m_motor != null && m_motor.IsGrounded;
    public bool IsAttacking => m_attackTimer > 0f;

    public float Stamina => Resources.GetCurrent(ResourceType.Stamina);
    public float StaminaMax => Resources.GetMax(ResourceType.Stamina);
    public float Mana => Resources.GetCurrent(ResourceType.MP);
    public float ManaMax => Resources.GetMax(ResourceType.MP);

    public float AttackDuration => attackDuration;
    public float JumpForce => jumpForce;
    public float AirMoveMultiplier => airMoveMultiplier;
    public float WalkSpeedMultiplier => walkSpeedMultiplier;

    public bool HasMovementIntent => m_movementIntent.sqrMagnitude > 0.0001f;
    public bool WantsRun => m_runIntent;
    public bool RunLatchActive => Time.time < m_runLatchEndTime;
    public bool WantsWalk => HasMovementIntent && !m_runIntent;
    public Vector3 MovementIntent => m_movementIntent;
    public TurnInfo CurrentTurnInfo => m_currentTurnInfo;

    public void ActivateRunLatch(float seconds) => m_runLatchEndTime = Time.time + Mathf.Max(0.01f, seconds);
    public void SetTurnInfo(in TurnInfo info) => m_currentTurnInfo = info;

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
        m_skillEntries.AttachGraph(combatFlowGraph);

        // Phase B：Semantic Resolver 初始化 + 从 Loadout 拉每槽位阈值
        m_inputSemantic = new InputSemanticResolver(this);
        RefreshSemanticConfigFromLoadout();
    }

    /// <summary>
    /// Phase F：先读 SemanticConfigSO（玩家级独立配置），未配项再回落 ChargeRoute.TapThreshold / ComboRoute.ComboSessionResetTime / DirectionalRoute 存在性。
    /// 任一槽位若两路都拿不到值，对应字段为 0（Resolver 自动跳过该语义分流）。
    /// </summary>
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
                    EnableDirectional = entry.DirectionalRoute != null,
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
    public void RequestActionPresentation(GameplayIntentKind kind, ActionDataSO action)
    {
        if (action == null) return;
        PublishEvent(new PlayerActionPresentationRequestEvent(GetInstanceID(), kind, action));
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

    public void MoveByLocomotionIntent(float externalSpeedMultiplier, bool wantsRun)
    {
        var input = m_movementIntent;
        var hasInput = input.sqrMagnitude > 0.0001f;
        var inputMag = hasInput ? Mathf.Clamp01(input.magnitude) : 0f;
        var speedCap = wantsRun ? RuntimeStats.RunSpeed : RuntimeStats.WalkSpeed;
        var targetSpeed = hasInput ? speedCap * inputMag * Mathf.Max(0f, externalSpeedMultiplier) : 0f;
        var accel = hasInput ? moveAcceleration : moveDeceleration;
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

    public void StopMove()
        => SetPlanarVelocity(Vector3.MoveTowards(PlanarVelocity, Vector3.zero, moveDeceleration * Time.deltaTime));

    public void ClearPlanarVelocity() => m_motor?.ClearPlanarVelocity();
    public void SetPlanarVelocity(Vector3 v) => m_motor?.SetPlanarVelocity(v);
    public void SetVerticalSpeed(float vy) => m_motor?.SetVerticalSpeed(vy);

    public void Jump()
    {
        m_motor?.Jump(jumpForce);
        PublishEvent(new PlayerJumpEvent(GetInstanceID(), name));
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
        YAxisPolicy yPolicy,
        bool useMotionComposer)
        => m_motor?.ApplyMotorFromGameplayVelocity(v, in ctx, yPolicy, useMotionComposer);
    public MotorSolveContext BuildActionMotorSolveContext()
        => m_motor != null ? m_motor.BuildActionMotorSolveContext() : MotorSolveContext.Locomotion;
    public void TeleportTo(Vector3 worldPos, bool forceAirborne = false)
        => m_motor?.TeleportTo(worldPos, forceAirborne);
}

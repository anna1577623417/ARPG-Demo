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

    [Header("184.1 Facing vs Turn")]
    [Tooltip("模型/Animator 子物体；LogicForward 写 root，Visual 缓追。为空时 Awake 自动取 Animator.transform。")]
    [SerializeField] Transform visualRoot;

    // ─── 运行时 ───
    PlayerStateManager m_stateManager;
    PlayerKCCMotor m_motor;
    PlayerSkillComponent m_skillComponent;
    readonly InputContextResolver m_inputContext = new InputContextResolver();
    float m_attackTimer;
    Vector3 m_movementIntent;
    Vector3 m_facingIntent;
    bool m_hasLocomotionMoveIntent;
    bool m_runIntent;
    float m_runLatchEndTime;
    bool m_isInitialized;

    Vector3 m_logicForward = Vector3.forward;
    VisualFacingDriver m_visualFacing;
    InputTense m_currentInputTense = InputTense.Idle;
    bool m_tapTurnArmPending;
    Vector3 m_tapTurnFromForward = Vector3.forward;
    int m_logicForwardLockCount;
    int m_turnPresentationInterruptGen;
    int m_consumedTurnInterruptGen;
    string m_lastTurnInterruptReason;

    Vector3 m_pendingFacing;
    bool m_hasPendingFacing;

    GameplayTagContainer m_gameplayTags;
    public ref GameplayTagContainer Tags => ref m_gameplayTags;
    public ref GameplayTagMask GameplayTags => ref m_gameplayTags.State;

    public readonly GameplayIntentBuffer IntentBuffer = new GameplayIntentBuffer(16);

    readonly ContextWindowTracker m_contextWindows = new ContextWindowTracker();
    ActionDataSO m_pendingAction;
    bool m_pendingActionArmed;
    GameplayIntentKind m_pendingActionKind;
    float m_pendingActionNormalizedStart;
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

    // 198.x — VelocityDecayState 已删除（167.1 ExitVelocityPolicy 死代码全清；182.1 StopStrategy 唯一权威）

    // ─── 公开属性 ───
    public InputReader InputReader => inputReader;
    public PlayerStateManager States => m_stateManager;
    public SkillEntryService SkillEntries => m_skillComponent?.Service;
    public SkillEntryLoadoutSO SkillEntryLoadout => skillEntryLoadout;

    /// <summary>185.2 — Graph EventWindowCondition 查询。</summary>
    public ContextWindowTracker ContextWindows => m_contextWindows;

    /// <summary>188.3 W9 — CombatObject 生成器（CombatTrack 时间轴触发的 Spawn 容器；运行时由 ActionTimelineRuntime 调用）。</summary>
    public CombatObjectSpawner CombatObjectSpawner { get; } = new CombatObjectSpawner();
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
    public InputSemanticResolver InputSemantic => m_skillComponent?.InputSemantic;
    public InputContextResolver InputContext => m_inputContext;

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

    public bool HasMovementIntent => m_hasLocomotionMoveIntent;
    public bool WantsRun => m_runIntent;
    public bool RunLatchActive => Time.time < m_runLatchEndTime;
    public bool WantsWalk => HasMovementIntent && !m_runIntent;
    /// <summary>Locomotion 位移意图；Tap/Pending 时回落 <see cref="FacingIntent"/> 供 TurnResolver 读角。</summary>
    public Vector3 MovementIntent => m_hasLocomotionMoveIntent ? m_movementIntent : m_facingIntent;
    /// <summary>184.1 — 当前帧方向输入（含 Tap/Pending），不含 Run 语义。</summary>
    public Vector3 FacingIntent => m_facingIntent;
    public InputTense CurrentInputTense => m_currentInputTense;
    /// <summary>184.4 — Transition 期间缓存的 Tap Facing。</summary>
    public bool HasPendingFacing => m_hasPendingFacing;
    public Vector3 PendingFacing => m_pendingFacing;
    /// <summary>184.1 Layer 2 — 逻辑朝向（Gameplay 唯一权威）。</summary>
    public Vector3 LogicForward => m_logicForward;
    public new Vector3 Forward => m_logicForward;
    public Transform VisualRoot => visualRoot;
    public Quaternion VisualRotation =>
        visualRoot != null ? visualRoot.rotation : transform.rotation;
    public TurnInfo CurrentTurnInfo => m_currentTurnInfo;
    /// <summary>159.1 L2+：Resolver 连续 Clip 表现快照（Strafe 等）。</summary>
    public LocomotionPresentationSnapshot LocomotionPresentation => m_locoPresentation;
    /// <summary>159.1 L2：LockOn 信号；Play 验收见 Tools/GameMain/Debug Settings → Simulate LockOn。</summary>
    public bool IsLockedOn => GameMainDebugSettings.SimulateLockOnLocomotion;

    public void ActivateRunLatch(float seconds) => m_runLatchEndTime = Time.time + Mathf.Max(0.01f, seconds);
    public void SetTurnInfo(in TurnInfo info) => m_currentTurnInfo = info;
    public void SetLocomotionPresentation(in LocomotionPresentationSnapshot snapshot) => m_locoPresentation = snapshot;

    internal void InjectMovementContext(IGameModeMovementContext context) => m_movementContext = context;

    /// <summary>MotionProfile 局部轴 → 世界水平前向（Z 轴）；不读 MovementIntent。</summary>
    public Vector3 ResolveMotionPlanarForward(MotionSpace space)
    {
        if (space == MotionSpace.CharacterForward
            && m_inputContext.TryGetDirectionalMotionForward(out var ctxForward))
        {
            return ctxForward;
        }

        return MotionSpaceBasis.ResolvePlanarForward(this, m_movementContext, space);
    }

    /// <summary>Motion 管道专用 LogicForward 写入；绕过 InputContext 冻结与 Stop 锁。</summary>
    public void SetLogicForwardFromMotion(
        Vector3 dir,
        RotationMode mode,
        Vector3 worldDelta,
        string source)
    {
        _ = mode;
        _ = worldDelta;
        _ = source;
        var planar = PlanarizeFacing(dir);
        var prev = m_logicForward;
        if (Vector3.Angle(prev, planar) < 0.05f)
        {
            return;
        }

        m_logicForward = planar;

        if (visualRoot != null && visualRoot != transform)
        {
            var visualWorldRot = visualRoot.rotation;
            transform.rotation = Quaternion.LookRotation(m_logicForward, Vector3.up);
            visualRoot.rotation = visualWorldRot;
        }
        else
        {
            transform.rotation = Quaternion.LookRotation(m_logicForward, Vector3.up);
        }
    }

    static Vector3 PlanarizeFacing(Vector3 dir)
    {
        dir.y = 0f;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
    }

    public bool ShouldSuppressLocomotionRotation()
        => m_inputContext.ShouldSuppressLocomotionRotation(Time.time);

    void OnValidate() => SyncRotationGateDebug();

    public RotationArbitrationPolicy CurrentRotationPolicy()
        => m_inputContext.ResolvePolicy(Time.time);

    public void CommitDirectionalInputContext(Vector2 pulseMoveBuffered)
    {
        var tuning = locomotionProfile != null ? locomotionProfile.Tuning : null;
        var chordWin = tuning != null ? tuning.ChordWindowSec : 0.12f;
        var now = Time.time;
        var holdDur = m_inputContext.MoveHoldDurationSec(now);
        var isChord = m_inputContext.MoveActive
                      && holdDur >= 0f
                      && holdDur <= chordWin;

        // 213.6 — CharacterForward 契约：Commit 仅锁 LogicForward；pulse 只供 Pick / 诊断。
        var commitFwd = PlanarizeFacing(Forward);
        var source = isChord ? "characterForward@Chord" : "liveLogicForward@Motion";

        m_inputContext.CommitDirectionalAbility(
            commitFwd,
            Forward,
            holdDur,
            chordWin,
            source);

        if (tuning != null && tuning.ClearPlanarVelocityOnDirectionalCommit)
        {
            ClearPlanarVelocity();
        }

        if (GameMainDebugSettings.SkillRouteDodge4 || GameMainDebugSettings.SkillRouteRoll4)
        {
            SkillRouteDebug.LogDodge4(
                this,
                "InputCtx",
                $"COMMIT directional fwd=({commitFwd.x:F2},{commitFwd.z:F2}) source={source} " +
                $"pulse=({pulseMoveBuffered.x:F2},{pulseMoveBuffered.y:F2}) holdDur={holdDur:F3}s " +
                $"policy={CurrentRotationPolicy()}");
        }
    }

    public void ClearDirectionalInputContext()
    {
        var liveMove = InputReader != null ? InputReader.MoveInput : Vector2.zero;
        m_inputContext.ClearDirectionalActionContext(liveMove, 0.12f);
    }

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
        SyncRotationGateDebug();
        Init();
    }

    protected override void OnEnable() { base.OnEnable(); }

    /// <summary>188.3 W9 — Entity.LateUpdate 已 Tick BuffStack；本方法追加 CombatObjectSpawner Tick。</summary>
    protected override void LateUpdate()
    {
        base.LateUpdate();
        CombatObjectSpawner?.Tick(Time.deltaTime);
    }

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
            m_motor.Bind(this, m_stateManager, GameMainDebugSettings.InterruptFlow);
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

        m_skillComponent = new PlayerSkillComponent(this);
        m_skillComponent.Initialize(skillEntryLoadout, semanticConfig, m_inputContext);

        InitFacingTurn1841();
    }

    void InitFacingTurn1841()
    {
        var fwd = transform.forward;
        fwd.y = 0f;
        m_logicForward = fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward;
        m_facingIntent = m_logicForward;

        if (visualRoot == null && Animator != null)
        {
            visualRoot = Animator.transform;
        }

        if (visualRoot == null || visualRoot == transform)
        {
            return;
        }

        m_visualFacing = visualRoot.GetComponent<VisualFacingDriver>();
        if (m_visualFacing == null)
        {
            m_visualFacing = visualRoot.gameObject.AddComponent<VisualFacingDriver>();
        }

        var tuning = locomotionProfile != null ? locomotionProfile.Tuning : null;
        var baseSpeed = tuning != null ? tuning.VisualMaxAngularSpeedDeg : 540f;
        var fastTrigger = tuning != null ? tuning.Turn90ThresholdDeg : 60f;
        m_visualFacing.Bind(this, baseSpeed, 1440f, fastTrigger);
    }

    /// <summary>184.1 — 由 PlayerController 每帧写入 Tap/Hold 时态。</summary>
    public void SetCurrentInputTense(InputTense tense) => m_currentInputTense = tense;

    /// <summary>184.1 — Tap 释放边沿：强制 TurnResolver 以 press 前 LogicForward 与当前 LogicForward 判定。</summary>
    public void ArmTapTurnPresentation(in Vector3 fromLogicForward)
    {
        m_tapTurnArmPending = true;
        m_tapTurnFromForward = fromLogicForward.sqrMagnitude > 0.0001f
            ? new Vector3(fromLogicForward.x, 0f, fromLogicForward.z).normalized
            : m_logicForward;
    }

    public bool TryConsumeTapTurnArm(out Vector3 fromLogicForward)
    {
        if (!m_tapTurnArmPending)
        {
            fromLogicForward = default;
            return false;
        }

        m_tapTurnArmPending = false;
        fromLogicForward = m_tapTurnFromForward;
        return true;
    }

    /// <summary>184.1 W9 — Stop 等 Action 期禁止改写 LogicForward。</summary>
    public bool IsLogicForwardLocked => m_logicForwardLockCount > 0;

    public void PushLogicForwardLock() => m_logicForwardLockCount++;

    public void PopLogicForwardLock()
    {
        m_logicForwardLockCount = Mathf.Max(0, m_logicForwardLockCount - 1);
    }

    /// <summary>184.4 — End/Pivot 期间缓存 Tap Facing，Transition 结束时应用。</summary>
    public void RequestPendingFacing(Vector3 newForward)
    {
        var planar = new Vector3(newForward.x, 0f, newForward.z);
        if (planar.sqrMagnitude < 0.0001f)
        {
            return;
        }

        var owner = States?.Current is PlayerActionState actionState ? actionState.CurrentAction : null;
        m_pendingFacing = planar.normalized;
        m_hasPendingFacing = true;
        MotionGrammarProbe.LogFacingCached(this, m_logicForward, m_pendingFacing, owner);
    }

    public bool TryConsumePendingFacing(out Vector3 newForward)
    {
        if (!m_hasPendingFacing)
        {
            newForward = default;
            return false;
        }

        newForward = m_pendingFacing;
        m_hasPendingFacing = false;
        return true;
    }

    public void ClearPendingFacing(string reason = null)
    {
        if (!m_hasPendingFacing)
        {
            return;
        }

        m_hasPendingFacing = false;
        MotionGrammarProbe.LogPendingCleared(this, reason ?? "cleared");
    }

    /// <summary>184.1 W5 — 主动 Intent 打断 Turn 表现（Action / Jump 等）。</summary>
    public void InterruptTurnPresentation(string reason = null)
    {
        if (!m_currentTurnInfo.IsTurning && !m_tapTurnArmPending)
        {
            return;
        }

        SetTurnInfo(default);
        m_tapTurnArmPending = false;
        m_turnPresentationInterruptGen++;
        m_lastTurnInterruptReason = reason;
        GetComponent<PlayerAnimController>()?.InterruptTurnIfAny(reason);
    }

    /// <summary>Locomotion 帧内消费打断请求并清 TurnResolver 锁。</summary>
    public bool ConsumeTurnPresentationInterruptRequest()
    {
        if (m_consumedTurnInterruptGen >= m_turnPresentationInterruptGen)
        {
            return false;
        }

        m_consumedTurnInterruptGen = m_turnPresentationInterruptGen;
        return true;
    }

    /// <summary>相机相对 WASD → 世界水平方向（与 PlayerController 口径一致）。</summary>
    public Vector3 ResolveCameraRelativeWorldDirection(Vector2 input)
    {
        if (input.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        input = Vector2.ClampMagnitude(input, 1f);
        var ctx = m_movementContext ?? GameModeManager.Instance;
        if (ctx != null && !ctx.IsCameraRelativeMovement)
        {
            return new Vector3(input.x, 0f, input.y);
        }

        Quaternion refRotation;
        if (ctx != null)
        {
            refRotation = ctx.GetMovementReferenceRotation();
        }
        else
        {
            var mainCam = Camera.main;
            refRotation = mainCam != null
                ? Quaternion.Euler(0f, mainCam.transform.eulerAngles.y, 0f)
                : Quaternion.identity;
        }

        var forward = refRotation * Vector3.forward;
        var right = refRotation * Vector3.right;
        return forward * input.y + right * input.x;
    }

    /// <summary>
    /// 206.1 / 206.2 — 八向 Skill 方向：Chord 态 camera-relative；Motion 态沿 LogicForward 强制 Forward。
    /// </summary>
    public DirectionalRouteType ResolveDirectionalChord(Vector2 moveBuffered, out bool isMotionMode)
    {
        var tuning = locomotionProfile != null ? locomotionProfile.Tuning : null;
        var chordWin = tuning != null ? tuning.ChordWindowSec : 0.12f;
        var motionWin = tuning != null ? tuning.MotionWindowSec : 0.20f;

        var now = Time.time;
        var holdDur = m_inputContext.MoveHoldDurationSec(now);
        var result = DirectionalDualModeResolver.Resolve(
            moveBuffered, holdDur, chordWin, motionWin, out isMotionMode, out var mode);

        var liveMove = InputReader != null ? InputReader.MoveInput : Vector2.zero;
        HoldMotionDodgeProbe.LogModeResolve(
            now,
            holdDur,
            chordWin,
            motionWin,
            isMotionMode,
            mode,
            m_inputContext.MoveActive,
            m_inputContext.DirectionalCommitted,
            liveMove,
            moveBuffered,
            HoldMotionDodgeProbe.CurrentSpacePulseIndex);

        DirectionalInputDiagProbe.LogMode(
            now,
            moveBuffered,
            holdDur,
            chordWin,
            motionWin,
            isMotionMode,
            mode,
            m_inputContext.MoveActive,
            m_inputContext.DirectionalCommitted,
            liveMove);

        var camFwd = Vector3.forward;
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            camFwd = mainCam.transform.forward;
            camFwd.y = 0f;
            if (camFwd.sqrMagnitude > 0.0001f)
            {
                camFwd.Normalize();
            }
        }

        DodgeChord8Probe.LogAbilityDown(
            now,
            m_inputContext.MoveActiveSince,
            moveBuffered,
            chordWin,
            motionWin,
            mode,
            m_inputContext.MoveActive,
            m_inputContext.DirectionalCommitted,
            LogicForward,
            camFwd);

        if (isMotionMode)
        {
            DodgeChord8Probe.LogMotionResolve(LogicForward, holdDur);
        }
        else
        {
            DodgeChord8Probe.LogChordResolve(moveBuffered, result);
        }

        return result;
    }

    public DirectionalRouteType ResolveDirectionalChord(Vector2 moveBuffered)
        => ResolveDirectionalChord(moveBuffered, out _);

    /// <summary>184.1 Layer 2 — 即时逻辑朝向；VisualRoot 世界 rotation 在存在时保持不变。</summary>
    public void SetLogicForward(Vector3 dir)
    {
        if (IsLogicForwardLocked)
        {
            return;
        }

        var planar = new Vector3(dir.x, 0f, dir.z);
        if (planar.sqrMagnitude < 0.0001f)
        {
            return;
        }

        planar.Normalize();
        var prev = m_logicForward;
        m_logicForward = planar;

        // Probe：Action 状态期间被改写 forward → 候选异常转向源头
        if (States?.Current is PlayerActionState __probeAct)
        {
            InputActionProbe.LogFacingApplied(this, __probeAct.CurrentAction, prev, planar, "Player.SetLogicForward@ActionState");
        }

        if (visualRoot != null && visualRoot != transform)
        {
            var visualWorldRot = visualRoot.rotation;
            transform.rotation = Quaternion.LookRotation(m_logicForward, Vector3.up);
            visualRoot.rotation = visualWorldRot;
        }
        else
        {
            transform.rotation = Quaternion.LookRotation(m_logicForward, Vector3.up);
        }

        if (Vector3.Angle(prev, m_logicForward) > 0.5f)
        {
            TurnProbe.LogFacingEdge(this, m_currentInputTense, prev, m_logicForward, "LogicForward");
        }
    }

    public override void LookAtDirection(Vector3 worldDirection, bool immediate = false)
    {
        SetLogicForward(worldDirection);
    }

    /// <summary>
    /// Phase F：先读 SemanticConfigSO（玩家级独立配置），未配项再回落 ChargeRoute / ComboRoute / PrimaryGroup 存在性。
    /// 208.3 L4 — 实现已迁至 <see cref="PlayerSkillComponent"/>。
    /// </summary>
    void RefreshSemanticConfigFromLoadout() =>
        m_skillComponent?.RefreshSemanticConfigFromLoadout(m_inputContext);

    void SyncRotationGateDebug()
    {
        if (m_inputContext == null) return;
        m_inputContext.DebugRotationGate = GameMainDebugSettings.RotationGate;
        m_inputContext.SetDebugOwnerLabel(name);
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
            ? ResolveDirectionalChord(move)
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
        if (GameMainDebugSettings.SkillRouteGraph && action != null)
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

        if (GameMainDebugSettings.SkillRouteGraph)
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

    public void ArmPendingAction(GameplayIntentKind kind, ActionDataSO action, float normalizedStart = 0f)
    {
        m_pendingActionArmed = true;
        m_pendingActionKind = kind;
        m_pendingAction = action;
        m_pendingActionNormalizedStart = Mathf.Clamp01(normalizedStart);
    }

    /// <summary>167.1 Segment 预留：ActionState OnEnter 消费归一化起播点。</summary>
    public float ConsumePendingActionNormalizedStart()
    {
        var start = m_pendingActionNormalizedStart;
        m_pendingActionNormalizedStart = 0f;
        return start;
    }

    public ActionDataSO PeekPendingAction() => m_pendingAction;

    /// <summary>仲裁未消费意图时丢弃已装配的 PendingAction，避免下帧误播旧段。</summary>
    public void ClearPendingAction()
    {
        m_pendingActionArmed = false;
        m_pendingAction = null;
        m_pendingActionNormalizedStart = 0f;
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
        AnimationClip presentationClip = null,
        float normalizedStart = 0f,
        float playbackAnimSpeedOverride = -1f)
    {
        if (action == null) return;
        PublishEvent(new PlayerActionPresentationRequestEvent(
            GetInstanceID(), kind, action, presentationClip, normalizedStart, playbackAnimSpeedOverride));
    }

    /// <summary>调整 Playable 速率（表现层事件，不直接引用 Animator）。</summary>
    public void RequestPlayablePlaybackSpeed(float speed) =>
        PublishEvent(new PlayablePlaybackSpeedRequestEvent(GetInstanceID(), Mathf.Max(0f, speed)));

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
        if (planar.sqrMagnitude > 1f)
        {
            planar.Normalize();
        }

        m_facingIntent = planar;
        m_hasLocomotionMoveIntent = planar.sqrMagnitude > 0.0001f;
        m_movementIntent = m_hasLocomotionMoveIntent ? planar : Vector3.zero;
        m_runIntent = wantsRun && m_hasLocomotionMoveIntent;
    }

    /// <summary>184.1 Pending/Tap — 仅更新 FacingIntent，不进入 Locomotion 位移。</summary>
    public void SetFacingIntentOnly(Vector3 worldDirection)
    {
        var planar = new Vector3(worldDirection.x, 0f, worldDirection.z);
        if (planar.sqrMagnitude > 1f)
        {
            planar.Normalize();
        }

        m_facingIntent = planar;
        m_hasLocomotionMoveIntent = false;
        m_movementIntent = Vector3.zero;
        m_runIntent = false;
    }

    public void ClearMovementIntent()
    {
        m_movementIntent = Vector3.zero;
        m_facingIntent = Vector3.zero;
        m_hasLocomotionMoveIntent = false;
        m_runIntent = false;
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
            // 198.3 Move 维度守卫：Action 期间默认不允许玩家输入叠加位移
            if (ActionRotationGate.IsAllowed(this, ActionRotationGate.Kind.Move))
            {
                SetPlanarVelocity(input.normalized * newSpeed);
                SetMoveDirection(input);
            }

            // 198.3 Facing 维度守卫（与 Move 独立）
            if (!ShouldSuppressLocomotionRotation()
                && ActionRotationGate.IsAllowed(this, ActionRotationGate.Kind.Facing))
            {
                ActionTurnProbe.Log(this, m_logicForward, input, "Player.MoveByLocomotionIntent");
                SetLogicForward(input);
                MaybeLogLocomotionRotationEdge(LocomotionRotationMode.SnapAlways, immediate: true);
            }
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

    // 184.1 / 183.1：Layer A 旋转探针边沿（仅 DebugLocomotionTrace）
    LocomotionRotationMode m_lastLoggedRotationMode = (LocomotionRotationMode)255;
    bool m_lastLoggedRotationImmediate;

    void MaybeLogLocomotionRotationEdge(LocomotionRotationMode mode, bool immediate)
    {
        if (!GameMainDebugSettings.LocomotionTrace && !GameMainDebugSettings.Locomotion)
        {
            return;
        }

        if (mode == m_lastLoggedRotationMode && immediate == m_lastLoggedRotationImmediate)
        {
            return;
        }

        m_lastLoggedRotationMode = mode;
        m_lastLoggedRotationImmediate = immediate;

        var turnState = m_currentTurnInfo.IsTurning ? $"LOCK{m_currentTurnInfo.Type}" : "UNLOCK";
        LocomotionDebug.Log(
            this,
            LocomotionDebug.CatRotation,
            $"mode={mode} immediate={immediate} spd={PlanarVelocity.magnitude:F2} turn={turnState}");
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

    // 198.x — 167.1 VelocityDecay 全套实现已删除（Begin/Step/Tick/End/Stop 5 个方法）。
    // 182.1 StopStrategy 体系已 1 年完全接管，无任何外部调用方。

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

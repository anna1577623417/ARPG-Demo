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
public class Player : Entity<Player>, IEntity, IIntentHost, IImpulseReceiver, IDamageable, IEffectReceiver, IActionContext, IActionLeaseOwner, IActionIntentCommitter, IAirCycleReadPort
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
    readonly AirCycleRuntime m_airCycle = new AirCycleRuntime();
    float m_attackTimer;
    Vector3 m_movementIntent;
    Vector3 m_facingIntent;
    bool m_hasLocomotionMoveIntent;
    bool m_runIntent;
    float m_runLatchEndTime;
    bool m_isInitialized;

    Vector3 m_logicForward = Vector3.forward;
    Vector3 m_desiredFacing = Vector3.forward;
    readonly DirectionInputHistory m_directionHistory = new DirectionInputHistory();
    readonly FacingCommitGate m_facingCommitGate = new FacingCommitGate();
    readonly OrientationAuthority m_orientation = new OrientationAuthority();
    LocomotionRuntimeContext m_locoContext;
    ActionFacingPolicyResolution m_lastActionFacingResolution;
    Vector3 m_lastHoldRedirectDesired;
    bool m_hasHoldRedirectRequest;
    DirectionalActionEntry m_directionalActionEntry;
    VisualFacingDriver m_visualFacing;
    InputTense m_currentInputTense = InputTense.Idle;
    int m_logicForwardLockCount;
    int m_turnPresentationInterruptGen;
    int m_consumedTurnInterruptGen;
    string m_lastTurnInterruptReason;
    uint m_nextTurnCompensationGeneration;
    TurnCompensationCue m_turnCompensationCue;

    Vector3 m_pendingFacing;
    bool m_hasPendingFacing;

    GameplayTagContainer m_gameplayTags;
    public ref GameplayTagContainer Tags => ref m_gameplayTags;
    public ref GameplayTagMask GameplayTags => ref m_gameplayTags.State;

    public GameplayIntentBuffer IntentBuffer { get; } = new GameplayIntentBuffer(16);

    readonly ContextWindowTracker m_contextWindows = new ContextWindowTracker();
    ActionLease m_pendingActionLease;
    ActionLease m_activeActionLease;
    uint m_nextActionLeaseVersion;
    bool m_hasPendingActionLease;
    bool m_hasActiveActionLease;
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
    ILockTargetProvider m_lockTargetProvider;

    // 198.x — VelocityDecayState 已删除（167.1 ExitVelocityPolicy 死代码全清；182.1 StopStrategy 唯一权威）

    // ─── 公开属性 ───
    public InputReader InputReader => inputReader;
    public PlayerStateManager States => m_stateManager;
    public IAirCycleReadPort AirCycle => m_airCycle;
    public AirCycleSnapshot CurrentAirCycle => m_airCycle.CurrentAirCycle;
    /// <summary>243.9 observation-only view. Zero explicitly means that no Action lease is active.</summary>
    public uint ActiveActionLeaseVersion => m_hasActiveActionLease ? m_activeActionLease.Version : 0u;
    public SkillEntryService SkillEntries => m_skillComponent?.Service;
    public SkillEntryLoadoutSO SkillEntryLoadout => skillEntryLoadout;

    /// <summary>185.2 — Graph EventWindowCondition 查询。</summary>
    public ContextWindowTracker ContextWindows => m_contextWindows;

    /// <summary>223.4-5：实体只持有世界生成端口，不再拥有 Active CombatObject 容器。</summary>
    public ICombatSpawnPort CombatSpawnPort => SpawnedCombatWorldHost.Port;
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
    /// <summary>234.6 — 松开 WalkEnd/RunEnd 当帧快照。Stop 只读，不由 Action 回写。</summary>
    public StopSessionSnapshot LastStopSessionSnapshot { get; internal set; }
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
    /// <summary>237 L5 — Visual 只读 Authority 输出，不再擅自 hard-snap 一份未仲裁 Logic。</summary>
    public Vector3 PresentationFacing => m_orientation.PresentationFacing;
    public OrientationAuthority Orientation => m_orientation;
    /// <summary>237 L1/L2/LA — 当帧期望朝向。CommittedFacing 由 Gate 到期或 HoldRedirect 写入。</summary>
    public Vector3 DesiredFacing => m_desiredFacing;
    public DirectionInputHistory DirectionHistory => m_directionHistory;
    public FacingCommitGate FacingCommit => m_facingCommitGate;
    /// <summary>237.3 LA — 只读运动事实。不驱动动画或状态。</summary>
    public LocomotionRuntimeContext LocomotionRuntime => m_locoContext;
    /// <summary>237.3 LB — 最近一次 Action Enter 的作者 Policy 与生效 Policy。</summary>
    public ActionFacingPolicyResolution LastActionFacingResolution => m_lastActionFacingResolution;
    public DirectionalActionEntry FrozenDirectionalEntry => m_directionalActionEntry;
    public DirectionalIntent CurrentDirectionalIntent =>
        new DirectionalIntent(InputReader != null ? InputReader.MoveInput : Vector2.zero, m_desiredFacing, (InputReader != null ? InputReader.MoveInput : Vector2.zero).magnitude);
    public new Vector3 Forward => m_logicForward;
    public Transform VisualRoot => visualRoot;
    public Quaternion VisualRotation =>
        visualRoot != null ? visualRoot.rotation : transform.rotation;
    public TurnInfo CurrentTurnInfo => m_currentTurnInfo;
    public TurnCompensationCue CurrentTurnCompensationCue => m_turnCompensationCue;
    /// <summary>159.1 L2+：Resolver 连续 Clip 表现快照（Strafe 等）。</summary>
    public LocomotionPresentationSnapshot LocomotionPresentation => m_locoPresentation;
    /// <summary>
    /// 锁定 Locomotion 信号。正式路径由 Targeting Runtime 注入；Debug 开关仅保留旧 Strafe 回归验收。
    /// Debug 模式不会伪造目标方向，因此 MotionSpace.LockTarget 仍安全回退到角色前方。
    /// </summary>
    public bool IsLockedOn => (m_lockTargetProvider != null && m_lockTargetProvider.HasValidLock)
                              || GameMainDebugSettings.SimulateLockOnLocomotion;

    public void ActivateRunLatch(float seconds) => m_runLatchEndTime = Time.time + Mathf.Max(0.01f, seconds);
    public void SetTurnInfo(in TurnInfo info)
    {
        var previous = m_currentTurnInfo;
        m_currentTurnInfo = info;
        CameraTurn233Probe.ObserveTurnInfo(this, in previous, in info, "Player.SetTurnInfo");
        CharacterTurnDisplacement233Probe.ObserveTurnInfo(this, in info);
    }

    /// <summary>
    /// 235.2 / 237 L2：PlayerController 在写入当帧 MovementIntent 前调用。
    /// L2 起 Down 当帧不再提交 Gameplay Facing，也不发 Turn Cue。
    /// Cue 改到 FacingCommitGate 到期与 Commit 同点；速度响应与 KCC 仍当帧结算。
    /// </summary>
    public void SubmitTurnCompensationCommand(Vector3 worldCommand, bool hasMoveInput)
    {
        if (!hasMoveInput)
        {
            return;
        }

        if (!CanStartTurnCompensationFromCurrentState(out var stateSource))
        {
            var command = PlanarizeFacing(worldCommand);
            var signed = Vector3.SignedAngle(m_logicForward, command, Vector3.up);
            var rejected = TurnIntentBuilder.CreateNonTurning(Mathf.Abs(signed), signed);
            LocomotionTurnPresentation235Probe.ObserveDecision(
                this,
                in rejected,
                $"TurnCompensationResolver.state_ineligible:{stateSource}");
            return;
        }

        var tuning = locomotionProfile != null ? locomotionProfile.Tuning : null;
        var settings = States.LocomotionTurnSettings;
        var enabled = settings.EnableTurnInPlacePresentation
                      && (tuning == null || tuning.EnableMovingTurnCompensation);
        var nextGeneration = m_nextTurnCompensationGeneration + 1U;
        var suppressedByMode = !enabled || IsLockedOn || m_inputContext.DirectionalCommitted;
        if (suppressedByMode && m_turnCompensationCue.IsTurning)
        {
            var command = PlanarizeFacing(worldCommand);
            var signed = Vector3.SignedAngle(m_logicForward, command, Vector3.up);
            m_nextTurnCompensationGeneration = nextGeneration;
            m_turnCompensationCue = new TurnCompensationCue(
                nextGeneration,
                TurnType.None,
                0,
                Mathf.Abs(signed),
                signed,
                Time.frameCount);
            var cancelInfo = m_turnCompensationCue.ToTurnInfo();
            LocomotionTurnPresentation235Probe.ObserveDecision(
                this,
                in cancelInfo,
                IsLockedOn ? "TurnCompensationResolver.cancel_lock_on" : "TurnCompensationResolver.cancel_directional");
            LocomotionTurnPresentation235Probe.ObserveEnd(
                this,
                IsLockedOn ? "lock_on_suppressed" : "directional_suppressed");
            return;
        }
    }

    public bool TryGetTurnCompensationCueAfter(uint consumedGeneration, out TurnCompensationCue cue)
    {
        cue = m_turnCompensationCue;
        return cue.IsValid && cue.Generation > consumedGeneration;
    }

    public void ClearTurnCompensationCue(string reason = null)
    {
        var generation = m_turnCompensationCue.Generation;
        var endReason = reason ?? "cue_cleared";
        m_turnCompensationCue = default;
        if (generation != 0)
        {
            LocomotionTurnPresentation235Probe.ObserveEnd(this, endReason);
        }
    }

    bool CanStartTurnCompensationFromCurrentState(out string source)
    {
        if (States?.Current is PlayerLocomotionState)
        {
            source = "Locomotion";
            return true;
        }

        if (States?.Current is PlayerActionState actionState
            && actionState.CurrentAction != null
            && actionState.CurrentAction.IsLocomotionRecovery)
        {
            source = $"Recovery:{actionState.CurrentAction.name}";
            return true;
        }

        source = States?.Current != null ? States.Current.StateId.ToString() : "NoState";
        return false;
    }

    void TryFireTurnCompensationAfterGateCommit(Vector3 prevCommitted, Vector3 newCommitted, int token)
    {
        if (token > 0
            && m_directionHistory.TryGetOwner(token, out var owner)
            && owner == DirectionTokenOwner.SkillChord)
        {
            return;
        }

        if (!CanStartTurnCompensationFromCurrentState(out _))
        {
            return;
        }

        var tuning = locomotionProfile != null ? locomotionProfile.Tuning : null;
        var settings = States != null ? States.LocomotionTurnSettings : TurnSettings.Default;
        var enabled = settings.EnableTurnInPlacePresentation
                      && (tuning == null || tuning.EnableMovingTurnCompensation);
        if (!enabled || IsLockedOn || m_inputContext.DirectionalCommitted)
        {
            return;
        }

        var nextGeneration = m_nextTurnCompensationGeneration + 1U;
        if (!TurnCompensationResolver.TryResolve(
                prevCommitted,
                newCommitted,
                enabled,
                IsLockedOn,
                m_inputContext.DirectionalCommitted,
                tuning != null ? tuning.Turn90ThresholdDeg : 70f,
                tuning != null ? tuning.Turn180ThresholdDeg : 135f,
                nextGeneration,
                Time.frameCount,
                tuning != null && tuning.Turn90PresentationLease > 0.0001f
                    ? tuning.Turn90PresentationLease
                    : 0.16f,
                tuning != null && tuning.Turn180PresentationLease > 0.0001f
                    ? tuning.Turn180PresentationLease
                    : 0.24f,
                out var cue)
            || !cue.IsTurning)
        {
            return;
        }

        m_nextTurnCompensationGeneration = nextGeneration;
        m_turnCompensationCue = cue;
        SkillGroupTurn237Probe.ObserveCue(this, in cue, "FacingCommitGate.Expire");
        DirectionAuthority237Probe.ObserveTurnCue(this, in cue, "FacingCommitGate.Expire");
        LocomotionTurnPresentation235Probe.ObserveDecision(
            this,
            cue.ToTurnInfo(),
            "TurnCompensationResolver.gate_expire");
    }
    public void SetLocomotionPresentation(in LocomotionPresentationSnapshot snapshot) => m_locoPresentation = snapshot;

    internal void InjectMovementContext(IGameModeMovementContext context) => m_movementContext = context;

    /// <summary>
    /// 由 Player Targeting Bridge 在 RuntimeReady 后注入。
    /// Player 不持有 Targeting Session，也不搜索 Entity；只消费其稳定方向快照。
    /// </summary>
    public void BindLockTargetProvider(ILockTargetProvider provider) => m_lockTargetProvider = provider;

    /// <summary>供 MotionSpace.LockTarget 使用；无有效 Targeting Runtime 时返回 false 走既有朝向回退。</summary>
    public bool TryGetLockTargetPlanarForward(out Vector3 forward)
    {
        forward = Vector3.forward;
        if (m_lockTargetProvider == null || !m_lockTargetProvider.HasValidLock
            || !m_lockTargetProvider.TryGetPlanarDirection(out var direction))
        {
            return false;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        forward = direction.normalized;
        return true;
    }

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

    /// <summary>237 L6 — 进入 Action 时冻结 Motion 参考系。Tick 不得再解析 live Transform。</summary>
    public MotionFrameSnapshot BuildMotionFrame(ActionDataSO action, SkillGroupDefinition group)
    {
        var profile = action != null ? action.MotionProfile : null;
        var space = group != null
            ? group.ResolveMotionCurveBasis(profile)
            : profile != null ? profile.MotionSpace : MotionSpace.CharacterForward;

        Vector3 forward;
        if (TryGetFrozenDirectionalEntry(out var entry) && entry.IsValid)
        {
            switch (space)
            {
                case MotionSpace.CameraForward:
                    forward = PlanarizeFacing(
                        entry.WorldDir.sqrMagnitude > 0.0001f ? entry.WorldDir : entry.BasisFacing);
                    break;
                case MotionSpace.WorldSpace:
                    forward = Vector3.forward;
                    break;
                case MotionSpace.LockTarget:
                    forward = TryGetLockTargetPlanarForward(out var lockFwd)
                        ? lockFwd
                        : PlanarizeFacing(entry.BasisFacing);
                    break;
                default:
                    forward = PlanarizeFacing(entry.BasisFacing);
                    break;
            }
        }
        else
        {
            forward = ResolveMotionPlanarForward(space);
        }

        var frame = MotionFrameSnapshot.Freeze(forward, space);
        if (!frame.IsValid)
        {
            DirectionAuthority237Probe.ObserveMotionFail(this, "no_frame");
        }

        return frame;
    }

    /// <summary>Motion 管道朝向请求。Action 租约内不再改 Committed；FaceMotion 只在 Enter 提交一次。</summary>
    public void SetLogicForwardFromMotion(
        Vector3 dir,
        RotationMode mode,
        Vector3 worldDelta,
        string source)
    {
        _ = mode;
        _ = worldDelta;
        if (m_orientation.HasActionLease)
        {
            return;
        }

        RequestFacing(FacingLeaseOwner.Action, dir, source ?? "Motion");
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

    /// <summary>237 L1/L2 — MoveDown：写入 History Token 与 DesiredFacing，打开 Commit Gate，不写 CommittedFacing。</summary>
    public int NotifyDirectionIntentDown(Vector2 raw, Vector3 worldDir, Vector3 basisFacing, float cameraYaw)
    {
        var desired = PlanarizeFacing(worldDir);
        m_desiredFacing = desired;
        var token = m_directionHistory.PushDown(raw, desired, PlanarizeFacing(basisFacing), cameraYaw);
        var timing = ResolveDirectionalTiming();
        var delay = m_facingCommitGate.Open(token, desired, timing.FacingCommitDelaySec, out var clamped);
        if (clamped)
        {
            DirectionAuthority237Probe.ObserveDelayClamp(this, timing.FacingCommitDelaySec, delay);
        }

        m_hasHoldRedirectRequest = false;
        m_lastHoldRedirectDesired = Vector3.zero;
        RefreshLocomotionRuntimeContext(raw, desired);
        return token;
    }

    /// <summary>
    /// 237 L1 / 237.3 LA — 持有期间刷新 DesiredTravel/DesiredFacing。
    /// Gate pending 只 RefreshDesired；Gate 已关且夹角足够则 RequestFacing(HoldRedirect)，不 PushDown、不开 Gate、不打 Turn Cue。
    /// </summary>
    public void TickDesiredFacing(Vector2 raw, Vector3 worldDir, float deadzone)
    {
        if (raw.sqrMagnitude <= deadzone * deadzone)
        {
            RefreshLocomotionRuntimeContext(raw, Vector3.zero);
            return;
        }

        var desiredTravel = PlanarizeFacing(worldDir);
        m_desiredFacing = desiredTravel;
        RefreshLocomotionRuntimeContext(raw, desiredTravel);

        if (m_facingCommitGate.IsPending)
        {
            m_facingCommitGate.RefreshDesired(m_desiredFacing);
            return;
        }

        TryRequestHoldRedirectFacing();
    }

    /// <summary>237.3 LA — 每帧填充五列事实。worldDir 有输入时即 DesiredTravel。</summary>
    public void RefreshLocomotionRuntimeContext(Vector2 raw, Vector3 worldDir)
    {
        var desiredTravel = raw.sqrMagnitude > 0.0001f
            ? PlanarizeFacing(worldDir)
            : Vector3.zero;
        var vel = PlanarVelocity;
        vel.y = 0f;
        var actualTravel = vel.sqrMagnitude > 0.0001f ? vel.normalized : Vector3.zero;
        m_locoContext = new LocomotionRuntimeContext(
            raw,
            desiredTravel,
            actualTravel,
            m_desiredFacing,
            m_logicForward);
    }

    void TryRequestHoldRedirectFacing()
    {
        var minDelta = ResolveDirectionalTiming().RedirectFacingMinDeltaDeg;
        if (minDelta < 0.05f)
        {
            minDelta = 1f;
        }

        if (Vector3.Angle(m_desiredFacing, m_logicForward) + 0.0001f < minDelta)
        {
            return;
        }

        if (m_hasHoldRedirectRequest
            && Vector3.Angle(m_desiredFacing, m_lastHoldRedirectDesired) + 0.0001f < minDelta)
        {
            return;
        }

        m_hasHoldRedirectRequest = true;
        m_lastHoldRedirectDesired = m_desiredFacing;
        RequestFacing(FacingLeaseOwner.Locomotion, m_desiredFacing, "HoldRedirect");
    }

    public void ClearDirectionCandidate()
    {
        m_desiredFacing = m_logicForward;
        m_directionHistory.Reset();
        m_facingCommitGate.Clear();
        m_hasHoldRedirectRequest = false;
        m_lastHoldRedirectDesired = Vector3.zero;
        ClearFrozenDirectionalEntry();
    }

    /// <summary>237 L3 — Trigger 选槽冻结。同一 Intent Peek 与 Action 内禁止再 PICK。</summary>
    public void CaptureDirectionalActionEntry(in DirectionalActionEntry entry)
    {
        m_directionalActionEntry = entry;
    }

    public void BindDirectionalActionEntryToAction()
    {
        if (!m_directionalActionEntry.IsValid)
        {
            return;
        }

        m_directionalActionEntry = m_directionalActionEntry.BindToAction();
        ClaimSkillDirectionToken(m_directionalActionEntry.Token);
    }

    /// <summary>237 L4 — 八向成功进入后 Claim 本次 Edge，取消 pending Gate / Cue。</summary>
    public void ClaimSkillDirectionToken(int token)
    {
        if (token <= 0)
        {
            DirectionAuthority237Probe.ObserveClaimOpen(this, token, "empty_token");
            return;
        }

        if (!m_directionHistory.TryClaim(token, DirectionTokenOwner.SkillChord, out var existing))
        {
            DirectionAuthority237Probe.ObserveClaimFail(this, token, existing);
            if (existing == DirectionTokenOwner.SkillChord)
            {
                m_facingCommitGate.Clear();
                ClearTurnCompensationCue("skill_claim_already");
            }

            return;
        }

        m_facingCommitGate.Clear();
        ClearTurnCompensationCue("skill_claim");
        DirectionAuthority237Probe.ObserveClaim(
            this, token, DirectionTokenOwner.SkillChord, cancelTurn: true);
    }

    public void NotifyDirectionalActionEnd()
    {
        var leftoverCue = m_turnCompensationCue.IsTurning;
        var claimed = DirectionTokenOwner.None;
        var token = m_directionalActionEntry.IsValid
            ? m_directionalActionEntry.Token
            : m_directionHistory.LastToken;
        if (token > 0)
        {
            m_directionHistory.TryGetOwner(token, out claimed);
        }

        if (claimed == DirectionTokenOwner.SkillChord && leftoverCue)
        {
            ClearTurnCompensationCue("action_end_claimed");
        }

        DirectionAuthority237Probe.ObserveActionEnd(this, leftoverCue, claimed);
    }

    public void ClearFrozenDirectionalEntry()
    {
        m_directionalActionEntry = default;
        DirectionAuthority237Probe.ResetMatchKey();
    }

    public bool TryGetFrozenDirectionalEntry(out DirectionalActionEntry entry)
    {
        entry = m_directionalActionEntry;
        return entry.IsValid;
    }

    /// <summary>237 L2/L4 — Skill Resolve 之后：未 Claim 的 Gate 到期才 Commit，并先 Claim 再发 Cue。HoldRedirect 不走本方法。</summary>
    public void TickFacingCommitGate()
    {
        if (!m_facingCommitGate.IsPending)
        {
            return;
        }

        var gateToken = m_facingCommitGate.Token;
        if (gateToken > 0
            && m_directionHistory.TryGetOwner(gateToken, out var claimed)
            && claimed == DirectionTokenOwner.SkillChord)
        {
            m_facingCommitGate.Clear();
            return;
        }

        if (States?.Current is PlayerActionState)
        {
            if (m_facingCommitGate.AgeUnscaled + 0.0001f >= m_facingCommitGate.DelaySec)
            {
                DirectionAuthority237Probe.ObserveFacingReq(
                    this,
                    FacingLeaseOwner.Locomotion,
                    m_orientation.ActionPolicy,
                    granted: false,
                    deny: "ActionLease",
                    source: "FacingCommitGate.Expire");
            }

            return;
        }

        if (IsLogicForwardLocked)
        {
            return;
        }

        if (m_facingCommitGate.AgeUnscaled + 0.0001f < m_facingCommitGate.DelaySec)
        {
            return;
        }

        var expireOwner = ClassifyExpiredTokenOwner(m_facingCommitGate.AgeUnscaled);
        if (!m_facingCommitGate.TryExpire(out var commitDir, out var token))
        {
            return;
        }

        if (token > 0)
        {
            if (!m_directionHistory.TryClaim(token, expireOwner, out var existing))
            {
                DirectionAuthority237Probe.ObserveClaimFail(this, token, existing);
                if (existing == DirectionTokenOwner.SkillChord)
                {
                    return;
                }
            }
            else
            {
                DirectionAuthority237Probe.ObserveClaim(this, token, expireOwner, cancelTurn: false);
            }
        }

        var prev = m_logicForward;
        if (!RequestFacing(FacingLeaseOwner.Locomotion, commitDir, "FacingCommitGate.Expire"))
        {
            return;
        }

        TryFireTurnCompensationAfterGateCommit(prev, m_logicForward, token);
    }

    DirectionTokenOwner ClassifyExpiredTokenOwner(float ageUnscaled)
    {
        var tapMax = ResolveDirectionalTiming().TurnTapMaxDurationSec;
        if (tapMax < 0.0001f)
        {
            tapMax = 0.14f;
        }

        var stillHeld = InputReader != null && InputReader.MoveInput.sqrMagnitude > 0.0001f;
        if (stillHeld && ageUnscaled > tapMax + 0.0001f)
        {
            return DirectionTokenOwner.Locomotion;
        }

        return DirectionTokenOwner.TurnTap;
    }

    public DirectionalTimingSnapshot ResolveDirectionalTiming(SkillContextGroupDefinition contextGroup = null)
    {
        var tuning = locomotionProfile != null ? locomotionProfile.Tuning : null;
        return DirectionalTimingProfileSO.Resolve(contextGroup, tuning);
    }

    public void CommitDirectionalInputContext(Vector2 pulseMoveBuffered)
    {
        var tuning = locomotionProfile != null ? locomotionProfile.Tuning : null;
        var chordWin = tuning != null ? tuning.ChordWindowSec : 0.12f;
        var now = Time.time;
        var holdDur = m_inputContext.MoveHoldDurationSec(now);
        var isChord = m_inputContext.MoveActive
                      && holdDur >= 0f
                      && holdDur <= chordWin;

        // 234.5：Chord 消费 MoveDown 不可变快照；Motion 使用已经持续移动后的 live LogicForward。
        var liveForward = PlanarizeFacing(Forward);
        var hasMoveDownBasis = m_inputContext.TryGetMoveDownPlanarForward(out var moveDownForward);
        var commitFwd = isChord && hasMoveDownBasis ? moveDownForward : liveForward;
        var captureFwd = hasMoveDownBasis ? moveDownForward : liveForward;
        var source = isChord && hasMoveDownBasis
            ? "moveDownSnapshot@Chord"
            : "liveLogicForward@Motion";

        m_inputContext.CommitDirectionalAbility(
            commitFwd,
            liveForward,
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

        SkillGroupTurn237Probe.ObserveCommit(this, pulseMoveBuffered, holdDur, chordWin, source, captureFwd, liveForward, commitFwd);
    }

    public void ClearDirectionalInputContext()
    {
        var liveMove = InputReader != null ? InputReader.MoveInput : Vector2.zero;
        m_inputContext.ClearDirectionalActionContext(liveMove, 0.12f);
        ClearFrozenDirectionalEntry();
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
    Entity ISkillHost.Entity => this;
    SkillEntryLoadoutSO ISkillHost.SkillEntryLoadout => skillEntryLoadout;
    GameplayTagContainer ISkillHost.Tags => m_gameplayTags;
    InputSemanticResolver ISkillHost.InputSemantic => m_skillComponent?.InputSemantic;
    float ISkillHost.SkillTime => Time.time;
    CombatContextSnapshot ISkillHost.BuildCombatContext(
        bool hitConfirmedThisStage,
        Vector2 moveOverride,
        bool moveOverrideValid)
        => BuildCombatContext(hitConfirmedThisStage, moveOverride, moveOverrideValid);
    void ISkillHost.ArmPendingAction(
        GameplayIntentKind kind,
        ActionDataSO action,
        float normalizedStart)
        => ArmPendingAction(kind, action, normalizedStart);
    ActionDataSO ISkillHost.PeekPendingAction() => PeekPendingAction();
    void ISkillHost.ClearPendingAction() => ClearPendingAction();
    void ISkillHost.NotifyRouteStageAction(ActionDataSO action) => NotifyRouteStageAction(action);
    void ISkillHost.RemoveTag(TagCategory category, ulong bits) => m_gameplayTags.Remove(category, bits);

    public bool TryCommitActionIntent(
        in GameplayIntent intent,
        in ArbitrationDecision decision,
        out string reason)
    {
        if (!decision.IsResolved)
        {
            reason = "missing-route";
            return false;
        }

        if (decision.FirstAction == null)
        {
            reason = "route-without-action";
            return true;
        }

        var lease = CreateActionLease(intent.Kind, decision.FirstAction, SkillEntries?.ActiveRoute);
        if (!TryArm(in lease))
        {
            reason = "action-lease-arm-failed";
            return false;
        }

        reason = "action-lease-armed";
        return true;
    }

    Transform IActionContext.Transform => transform;
    Animator IActionContext.Animator => base.Animator;
    IEntityMotor IActionContext.Motor => m_motor;
    LocalEventBus IActionContext.EventBus => base.EventBus;
    ICombatSpawnPort IActionContext.CombatSpawnPort => CombatSpawnPort;
    void IActionContext.PublishActionPresentation(ActionTimelineMarkerKind kind, string payload)
        => PublishEvent(new EntityActionPresentationEvent(GetInstanceID(), kind, payload));
    void IActionContext.PublishTeleported(Vector3 worldPosition)
        => PublishEvent(new EntityTeleportedEvent(GetInstanceID(), name, worldPosition));
    GameplayTagContainer ITagOwner.Tags => m_gameplayTags;
    bool IEntity.IsAlive => !IsDead;
    IBuffStack IEffectReceiver.BuffStack => Buffs;
    IReadOnlyStatSet IEffectReceiver.Stats => Stats;
    IResourcePool IEffectReceiver.Resources => Resources;

    public ImpulseApplyResult TryApplyImpulse(in ImpulseRequest request)
    {
        if (IsDead)
        {
            return ImpulseApplyResult.RejectedDead;
        }

        if (m_motor == null)
        {
            return ImpulseApplyResult.RejectedNoMotor;
        }

        var applied = false;
        var planarDirection = request.Direction;
        planarDirection.y = 0f;
        var currentPlanarVelocity = m_motor.PlanarVelocity;
        var requestedPlanarVelocity = Vector3.zero;
        var alignmentDot = 0f;
        var alignment = "None";
        if (request.Force > 0.01f && planarDirection.sqrMagnitude > 0.0001f)
        {
            requestedPlanarVelocity = planarDirection.normalized * request.Force;
            if (currentPlanarVelocity.sqrMagnitude > 0.0001f)
            {
                alignmentDot = Vector3.Dot(currentPlanarVelocity.normalized, requestedPlanarVelocity.normalized);
                alignment = alignmentDot < -0.5f
                    ? "Opposed"
                    : alignmentDot > 0.5f ? "Aligned" : "Crossed";
            }
        }

        if (GameMainDebugSettings.ReactionDirection2206Log)
        {
            Debug.Log(
                $"[Reaction2206] channel=PlayerImpulse phase=BeforeApply frame={Time.frameCount} " +
                $"target={name} state={States?.Current?.StateId ?? "(none)"} " +
                $"currentPlanar={currentPlanarVelocity.ToString("F2")} " +
                $"requestedPlanar={requestedPlanarVelocity.ToString("F2")} " +
                $"currentSpeed={currentPlanarVelocity.magnitude:F2} force={request.Force:F2} " +
                $"alignment={alignment} dot={alignmentDot:F2} log=220.6");
        }

        if (GameMainDebugSettings.ReactionDirection2206Log)
        {
            m_motor.BeginReactionDirection2206SpeedProbe();
        }

        if (request.Force > 0.01f && planarDirection.sqrMagnitude > 0.0001f)
        {
            SetPlanarVelocity(requestedPlanarVelocity);
            applied = true;
        }

        if (request.LaunchUpSpeed > 0.01f)
        {
            SetVerticalSpeed(Mathf.Max(VerticalSpeed, request.LaunchUpSpeed));
            applied = true;
        }

        var result = applied ? ImpulseApplyResult.Applied : ImpulseApplyResult.IgnoredByProfile;
        if (GameMainDebugSettings.ReactionDirection2206Log)
        {
            Debug.Log(
                $"[Reaction2206] channel=PlayerImpulse phase=AfterApply frame={Time.frameCount} " +
                $"target={name} result={result} postPlanar={m_motor.PlanarVelocity.ToString("F2")} " +
                $"verticalSpeed={m_motor.VerticalSpeed:F2} log=220.6");
        }

        return result;
    }

    // ─── 生命周期 ───

    protected override void Awake()
    {
        base.Awake();
        m_gameplayTags.Faction.Set((ulong)FactionTag.Player);
        if (statsBlueprint is PlayerStatsSO ps)
        {
            ps.ApplyLegacyStandaloneMaxStaminaToStats(Stats);
        }
        EnsurePlayerDefaultResourceStats();
        SyncRotationGateDebug();
        Init();
    }

    protected override void OnEnable() { base.OnEnable(); }

    protected override void OnDisable()
    {
        ClearDirectionCandidate();
        CancelAirCycle(AirCycleCancelReason.EntityDisabled, RuntimeTracePhase.LogicEnd);
        base.OnDisable();
    }

    internal AirCycleTransitionResult EnsureAirCycle(AirCycleCause cause, RuntimeTracePhase phase)
    {
        var stamp = CaptureRuntimeStep(phase);
        return m_airCycle.EnsureActive(cause, in stamp);
    }

    internal AirCycleTransitionResult MarkAirCycleFalling(RuntimeTracePhase phase)
    {
        var stamp = CaptureRuntimeStep(phase);
        return m_airCycle.MarkFalling(in stamp);
    }

    internal AirCycleTransitionResult MarkAirCycleLandingRouted(RuntimeTracePhase phase)
    {
        var stamp = CaptureRuntimeStep(phase);
        return m_airCycle.MarkLandingRouted(in stamp);
    }

    internal AirCycleTransitionResult CloseAirCycle(RuntimeTracePhase phase)
    {
        var stamp = CaptureRuntimeStep(phase);
        return m_airCycle.Close(in stamp);
    }

    internal AirCycleTransitionResult CancelAirCycle(AirCycleCancelReason reason, RuntimeTracePhase phase)
    {
        var stamp = CaptureRuntimeStep(phase);
        return m_airCycle.Cancel(reason, in stamp);
    }

    RuntimeStepStamp CaptureRuntimeStep(RuntimeTracePhase phase) =>
        m_stateManager != null ? m_stateManager.CaptureRuntimeStep(phase) : default;

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
        m_desiredFacing = m_logicForward;
        m_facingIntent = m_logicForward;
        m_orientation.BindInitial(m_logicForward);
        m_directionHistory.Reset();
        m_facingCommitGate.Clear();
        m_hasHoldRedirectRequest = false;
        m_lastHoldRedirectDesired = Vector3.zero;
        m_directionalActionEntry = default;

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
    public void InterruptTurn(TurnInterruptReason reason, string detail = null)
    {
        var hasLegacyTurn = m_currentTurnInfo.IsTurning;
        var hasCompensationCue = m_turnCompensationCue.IsValid;
        if (!hasLegacyTurn && !hasCompensationCue)
        {
            return;
        }

        var previousTurnType = hasLegacyTurn ? m_currentTurnInfo.Type : m_turnCompensationCue.Type;
        SetTurnInfo(default);
        ClearTurnCompensationCue($"interrupt:{reason}");
        m_turnPresentationInterruptGen++;
        m_lastTurnInterruptReason = detail ?? reason.ToString();
        var step = CaptureRuntimeStep(RuntimeTracePhase.StateLogicEnd);
        PublishEvent(new TurnPresentationInterruptedEvent(
            GetInstanceID(), in step, (uint)m_turnPresentationInterruptGen, previousTurnType, reason));
    }

    [System.Obsolete("Use InterruptTurn(TurnInterruptReason, string).")]
    public void InterruptTurnPresentation(string reason = null) =>
        InterruptTurn(TurnInterruptReason.External, reason);

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

    /// <summary>237 L5 — 生产朝向提交。外部 SetLogicForward 走本入口；失败打 Log，禁止旁路写 Logic。</summary>
    public bool RequestFacing(FacingLeaseOwner owner, Vector3 dir, string source)
    {
        if (m_orientation == null)
        {
            DirectionAuthority237Probe.ObserveAuthorityMissing(this, source);
            return false;
        }

        if (IsLogicForwardLocked)
        {
            CameraTurn233Probe.ObserveLogicForwardRejected(this, dir, source);
            CharacterTurnDisplacement233Probe.ObserveLogicForwardRejected(this, dir, source);
            DirectionAuthority237Probe.ObserveFacingReq(
                this, owner, m_orientation.ActionPolicy, granted: false, deny: "LogicLock", source: source, requestedDir: dir);
            return false;
        }

        var request = new FacingRequest(owner, dir, source, m_orientation.ActionPolicy);
        if (!m_orientation.TryCommit(in request, out var committed, out var deny))
        {
            DirectionAuthority237Probe.ObserveFacingReq(
                this, owner, m_orientation.ActionPolicy, granted: false, deny: deny, source: source, requestedDir: dir);
            return false;
        }

        DirectionAuthority237Probe.ObserveFacingReq(
            this, owner, m_orientation.ActionPolicy, granted: true, deny: null, source: source, requestedDir: dir);
        ApplyCommittedFacing(committed, source);
        return true;
    }

    /// <summary>237 L5/LB — Action Enter 吃 Resolver 的 EffectivePolicy。PreserveEntry 不改 Committed。不按 slot 写脸。</summary>
    public void BeginActionFacingLease(ActionDataSO action, Vector3 motionFacing)
    {
        var authored = action != null
            ? action.FacingPolicy
            : ActionFacingPolicy.PreserveEntryFacing;
        m_lastActionFacingResolution = ActionFacingPolicyResolver.Resolve(
            authored,
            FacingPolicyGameplayContext.Unwired);

        if (m_orientation == null)
        {
            DirectionAuthority237Probe.ObserveAuthorityMissing(this, "ActionEnter");
            return;
        }

        if (action == null)
        {
            DirectionAuthority237Probe.ObservePolicyMissing(this);
        }

        var effective = m_lastActionFacingResolution.EffectivePolicy;
        var denyReason = m_lastActionFacingResolution.TrackTargetOpen ? "TrackTargetOpen" : null;
        if (!m_orientation.TryBeginActionLease(
                effective,
                m_logicForward,
                motionFacing,
                out var committed,
                out var leaseDeny))
        {
            DirectionAuthority237Probe.ObserveFacingReq(
                this,
                FacingLeaseOwner.Action,
                effective,
                granted: false,
                deny: leaseDeny ?? "ActionLease",
                source: "ActionEnter",
                requestedDir: motionFacing);
            return;
        }

        if (!string.IsNullOrEmpty(leaseDeny) && string.IsNullOrEmpty(denyReason))
        {
            denyReason = leaseDeny;
        }

        var requested = effective == ActionFacingPolicy.FaceMotionAtEntry
            ? motionFacing
            : committed;
        DirectionAuthority237Probe.ObserveFacingReq(
            this,
            FacingLeaseOwner.Action,
            effective,
            granted: true,
            deny: denyReason,
            source: "ActionEnter",
            requestedDir: requested);

        if (!IsLogicForwardLocked)
        {
            ApplyCommittedFacing(committed, "ActionFacing.Enter");
        }
    }

    public void EndActionFacingLease()
    {
        if (m_orientation == null || !m_orientation.HasActionLease)
        {
            return;
        }

        m_orientation.EndActionLease();
        DirectionAuthority237Probe.ObserveVisualAuth(this, FacingLeaseOwner.Locomotion);
        ReevaluateHeldFacingAfterAction();
    }

    void ReevaluateHeldFacingAfterAction()
    {
        if (InputReader == null || InputReader.MoveInput.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        RequestFacing(FacingLeaseOwner.Locomotion, m_desiredFacing, "ActionExit.Held");
    }

    /// <summary>184.1 Layer 2 — 包装 OrientationAuthority。生产路径禁止旁路写 m_logicForward。</summary>
    public void SetLogicForward(Vector3 dir, string source = null)
    {
        RequestFacing(OrientationAuthority.ClassifyOwner(source), dir, source);
    }

    void ApplyCommittedFacing(Vector3 dir, string source)
    {
        var planar = PlanarizeFacing(dir);
        var prev = m_logicForward;
        if (Vector3.Angle(prev, planar) < 0.05f)
        {
            m_logicForward = planar;
            return;
        }

        var rootYawBefore = transform.eulerAngles.y;
        var visualYawBefore = visualRoot != null ? visualRoot.eulerAngles.y : rootYawBefore;
        m_logicForward = planar;

        if (States?.Current is PlayerActionState probeAct)
        {
            InputActionProbe.LogFacingApplied(this, probeAct.CurrentAction, prev, planar, "Player.ApplyCommittedFacing@ActionState");
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

        CameraTurn233Probe.ObserveLogicForwardWrite(
            this,
            prev,
            m_logicForward,
            source,
            rootYawBefore,
            transform.eulerAngles.y,
            visualYawBefore,
            visualRoot != null ? visualRoot.eulerAngles.y : transform.eulerAngles.y);
        CharacterTurnDisplacement233Probe.ObserveLogicForwardWrite(
            this,
            prev,
            m_logicForward,
            source,
            rootYawBefore,
            transform.eulerAngles.y,
            visualYawBefore,
            visualRoot != null ? visualRoot.eulerAngles.y : transform.eulerAngles.y);
        SkillGroupTurn237Probe.ObserveLogicSnap(this, prev, m_logicForward, source);
        DirectionAuthority237Probe.ObserveFacingCommit(this, prev, m_logicForward, source);

        if (Vector3.Angle(prev, m_logicForward) > 0.5f)
        {
            TurnProbe.LogFacingEdge(this, m_currentInputTense, prev, m_logicForward, "LogicForward");
        }
    }

    public override void LookAtDirection(Vector3 worldDirection, bool immediate = false)
    {
        RequestFacing(FacingLeaseOwner.Locomotion, worldDirection, "Player.LookAtDirection");
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

    Entity IIntentHost.Owner => this;

    public IntentEnqueueResult TryEnqueue(in GameplayIntent intent)
    {
        IntentEnqueueResult result;
        if (IsDead)
        {
            result = IntentEnqueueResult.RejectedOwnerDead;
        }
        else
        {
            IntentBuffer.Enqueue(in intent);
            result = IntentEnqueueResult.Accepted;
        }

        if (GameMainDebugSettings.IntentArbitration || GameMainDebugSettings.InterruptFlow)
        {
            Debug.Log(
                $"[Intent] channel=Enqueue result={result} host=Player kind={intent.Kind} " +
                $"timestamp={intent.TimeStamp:F3} expire={intent.ExpireTime:F3}");
        }

        return result;
    }

    public void FlushExpiredIntents(float now) => IntentBuffer.FlushExpired(now);

    public void EnqueueGameplayIntent(in GameplayIntent intent) => TryEnqueue(in intent);

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

    // ─── ActionLease（Locomotion/Airborne/Combat 切到 Action 前的待播）───

    public void ArmPendingAction(GameplayIntentKind kind, ActionDataSO action, float normalizedStart = 0f)
    {
        var lease = CreateActionLease(kind, action, SkillEntries?.ActiveRoute, normalizedStart);
        TryArm(in lease);
    }

    public ActionLease CreateActionLease(
        GameplayIntentKind kind,
        ActionDataSO action,
        SkillRouteRuntime route,
        float normalizedStart = 0f)
    {
        return new ActionLease(
            ++m_nextActionLeaseVersion,
            kind,
            action,
            route,
            Mathf.Clamp01(normalizedStart));
    }

    public bool TryArm(in ActionLease lease)
    {
        if (lease.Version == 0 || lease.Action == null)
        {
            return false;
        }

        m_pendingActionLease = lease;
        m_hasPendingActionLease = true;
        return true;
    }

    public bool TryConsumePendingAction(out ActionLease lease)
    {
        if (!m_hasPendingActionLease)
        {
            lease = default;
            return false;
        }

        return TryConsume(m_pendingActionLease.Version, out lease);
    }

    public bool TryConsume(uint version, out ActionLease lease)
    {
        if (!m_hasPendingActionLease || m_pendingActionLease.Version != version)
        {
            lease = default;
            return false;
        }

        lease = m_pendingActionLease;
        m_pendingActionLease = default;
        m_hasPendingActionLease = false;
        m_activeActionLease = lease;
        m_hasActiveActionLease = true;
        return true;
    }

    public void CompleteActionLease(uint version)
    {
        if (m_hasActiveActionLease && m_activeActionLease.Version == version)
        {
            m_activeActionLease = default;
            m_hasActiveActionLease = false;
        }
    }

    public void CancelActionLease(uint version, ActionCancelReason reason)
    {
        if (m_hasPendingActionLease && m_pendingActionLease.Version == version)
        {
            m_pendingActionLease = default;
            m_hasPendingActionLease = false;
        }

        if (m_hasActiveActionLease && m_activeActionLease.Version == version)
        {
            m_activeActionLease = default;
            m_hasActiveActionLease = false;
        }
    }

    public void CancelActive(ActionCancelReason reason)
    {
        m_pendingActionLease = default;
        m_activeActionLease = default;
        m_hasPendingActionLease = false;
        m_hasActiveActionLease = false;
    }

    /// <summary>兼容旧调用方：当前活动租约的归一化起播点。</summary>
    public float ConsumePendingActionNormalizedStart()
    {
        return m_hasActiveActionLease ? m_activeActionLease.NormalizedStart : 0f;
    }

    public ActionDataSO PeekPendingAction()
        => m_hasPendingActionLease ? m_pendingActionLease.Action : null;

    /// <summary>仲裁未消费意图时丢弃已装配的 PendingAction，避免下帧误播旧段。</summary>
    public void ClearPendingAction()
    {
        if (m_hasPendingActionLease)
        {
            CancelActionLease(m_pendingActionLease.Version, ActionCancelReason.Replaced);
        }
    }

    /// <summary>兼容旧调用方；新动作状态应直接消费 ActionLease。</summary>
    public bool TryTakePendingAction(out GameplayIntentKind kind, out ActionDataSO action)
    {
        if (!TryConsumePendingAction(out var lease))
        {
            kind = GameplayIntentKind.None;
            action = null;
            return false;
        }

        kind = lease.Kind;
        action = lease.Action;
        return true;
    }

    /// <summary>请求 Playables 播放 Action 主 Clip（首段进入 / MultiStage 段内换招）。</summary>
    public void RequestActionPresentation(
        GameplayIntentKind kind,
        ActionDataSO action,
        AnimationClip presentationClip = null,
        float normalizedStart = 0f,
        float playbackAnimSpeedOverride = -1f,
        uint actionLeaseVersion = 0)
    {
        if (action == null) return;
        PublishEvent(new PlayerActionPresentationRequestEvent(
            GetInstanceID(), kind, action, presentationClip, normalizedStart,
            playbackAnimSpeedOverride, actionLeaseVersion));
    }

    /// <summary>调整 Playable 速率（表现层事件，不直接引用 Animator）。</summary>
    public void RequestPlayablePlaybackSpeed(float speed) =>
        PublishEvent(new PlayablePlaybackSpeedRequestEvent(GetInstanceID(), Mathf.Max(0f, speed)));

    /// <summary>164.1 L3 / 227.5.1：State Policy 允许连续槽，Action flag 显式授权接管。</summary>
    public void RequestContinuousLocomotionPresentation(
        ActionDataSO action,
        LocomotionStateId resolvedState,
        LocomotionExecutionPolicy executionPolicy)
    {
        if (action == null
            || !action.IsContinuousLocomotion
            || executionPolicy != LocomotionExecutionPolicy.ContinuousPresentation)
        {
            return;
        }

        PublishEvent(new PlayerContinuousLocomotionRequestEvent(
            GetInstanceID(),
            action,
            resolvedState,
            executionPolicy));
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

        var planar = PlanarVelocity;
        var currentSpeed = planar.magnitude;

        if (hasInput)
        {
            // 198.3 Move 维度守卫：Action 期间默认不允许玩家输入叠加位移
            if (ActionRotationGate.IsAllowed(this, ActionRotationGate.Kind.Move))
            {
                Vector3 resolvedVelocity;
                if (tuning != null && tuning.UseVectorVelocityResponse)
                {
                    var responseSettings = new LocomotionVelocityResponse.Settings(
                        wantsRun ? tuning.RunRiseTime : tuning.WalkRiseTime,
                        tuning.ReleaseStopTime,
                        tuning.DirectionTurnResponseTime,
                        tuning.ReverseResponseTime,
                        tuning.StartSpeedFloorRatio);
                    var response = LocomotionVelocityResponse.Resolve(
                        planar,
                        input,
                        targetSpeed,
                        Time.deltaTime,
                        in responseSettings);
                    resolvedVelocity = response.Velocity;
                }
                else
                {
                    // 227.4.3 兼容：未迁移 Tuning 仍保留旧标量响应。
                    var accel = ResolveMoveAcceleration(tuning, hasInput: true);
                    var newSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * Time.deltaTime);
                    resolvedVelocity = input.normalized * newSpeed;
                }

                SetPlanarVelocity(resolvedVelocity);
                if (resolvedVelocity.sqrMagnitude > 0.0001f)
                {
                    SetMoveDirection(resolvedVelocity.normalized);
                }
            }
        }
        else
        {
            if (tuning != null && tuning.UseVectorVelocityResponse)
            {
                var responseSettings = new LocomotionVelocityResponse.Settings(
                    wantsRun ? tuning.RunRiseTime : tuning.WalkRiseTime,
                    tuning.ReleaseStopTime,
                    tuning.DirectionTurnResponseTime,
                    tuning.ReverseResponseTime,
                    tuning.StartSpeedFloorRatio);
                var response = LocomotionVelocityResponse.Resolve(
                    planar,
                    Vector3.zero,
                    0f,
                    Time.deltaTime,
                    in responseSettings);
                SetPlanarVelocity(response.Velocity);
            }
            else
            {
                var accel = ResolveMoveAcceleration(tuning, hasInput: false);
                var newSpeed = Mathf.MoveTowards(currentSpeed, 0f, accel * Time.deltaTime);
                var dir = currentSpeed > 0.01f ? planar.normalized : Vector3.zero;
                SetPlanarVelocity(dir * newSpeed);
            }
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
        if (tuning != null && tuning.UseVectorVelocityResponse)
        {
            var responseSettings = new LocomotionVelocityResponse.Settings(
                m_runIntent ? tuning.RunRiseTime : tuning.WalkRiseTime,
                tuning.ReleaseStopTime,
                tuning.DirectionTurnResponseTime,
                tuning.ReverseResponseTime,
                tuning.StartSpeedFloorRatio);
            var response = LocomotionVelocityResponse.Resolve(
                PlanarVelocity,
                Vector3.zero,
                0f,
                Time.deltaTime,
                in responseSettings);
            SetPlanarVelocity(response.Velocity);
            return;
        }

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
        EnsureAirCycle(AirCycleCause.Jump, RuntimeTracePhase.IntentResolved);
        m_motor?.Jump(force);
        var step = CaptureRuntimeStep(RuntimeTracePhase.StateLogicEnd);
        PublishEvent(new PlayerJumpEvent(GetInstanceID(), name, CurrentAirCycle, step));
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
    {
        CancelAirCycle(AirCycleCancelReason.Teleport, RuntimeTracePhase.IntentResolved);
        m_motor?.TeleportTo(worldPos, forceAirborne);
    }

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

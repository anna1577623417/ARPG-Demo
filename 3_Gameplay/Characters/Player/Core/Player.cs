using UnityEngine;

/// <summary>
/// 玩家实体（"士兵肉体"层）— 纯能力执行器。
///
/// ═══ 2.0 职责 ═══
///
/// Player 只负责"做"：移动、跳跃、重力、地面检测等物理能力。
/// "何时做"由 4 支柱状态机（Locomotion / Airborne / Action / Dead）决定。
/// "做什么"由 ActionDataSO 数据资产驱动。
///
/// ═══ 决策链路 ═══
///
/// InputReader → PlayerController（语义意图）→ IntentBuffer（环形队列）
///   → TransitionResolver（标签仲裁）→ 当前状态.TryConsumeGameplayIntent
///   → ArmPendingAction → PlayerActionState.OnEnter → 读取 ActionDataSO → 推进时间轴 + 抛标签
///
/// ═══ 表现解耦 ═══
///
/// Player 不持有 Animator 引用。核心位移与逻辑不经 EventBus；表现层可通过 LocalEventBus 接收少量展示类通知（跳转/落地/动作剪辑等）。
/// </summary>
[RequireComponent(typeof(PlayerStateManager))]
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(PlayerKCCMotor))]
public class Player : Entity<Player>, IDamageable {
    // ─── 输入 ───

    [Header("Input")]
    [SerializeField] private InputReader inputReader;

    // ─── 移动参数 ───

    [Header("Movement")]
    [Tooltip("移动时的加速度。\n增大：起步极快，手感偏“硬”、“干脆”。\n减小：起步有缓冲，手感偏“滑”、“溜冰”。")]
    [SerializeField] private float moveAcceleration = 18f;

    [Tooltip("停止时的减速度。\n增大：松手即停，指哪打哪。\n减小：松手后会往前滑行一段距离。")]
    [SerializeField] private float moveDeceleration = 22f;

    [Tooltip("跳跃时的初始向上速度。\n增大：跳得更高。\n减小：跳得更矮。")]
    [SerializeField] private float jumpForce = 12f;

    [Tooltip("空中移动控制力的乘数 (0~1)。\n增大：在空中可以灵活改变方向（像超级马里奥）。\n减小：起跳后几乎无法改变落点（偏写实）。")]
    [SerializeField] private float airMoveMultiplier = 0.6f;

    // KCC 相关参数（gravity / pivotToFootOffset / groundLayers / EdgeSlip / motorSettings 等）
    // 已迁移至 PlayerKCCMotor 组件，由 RequireComponent 自动挂载并独立序列化。

    // ─── 闪避 / 剑冲：Moveset 未返回 ActionDataSO 时的墙钟兜底；Gameplay 不施加程序化位移，仅占位时长 ───

    [Header("Fallback — Dodge (no ActionDataSO)")]
    [SerializeField] private float fallbackDodgeDurationSeconds = 0.35f;

    [Tooltip("翻滚冷却（全局平衡，可保留在 Player）。")]
    [SerializeField] private float dodgeCooldown = 0.8f;

    [Header("Fallback — SwordDash (no ActionDataSO)")]
    [SerializeField] private float fallbackSwordDashDurationSeconds = 0.35f;

    [SerializeField] private float swordDashCooldown = 1.1f;

    // ─── 攻击参数 ───

    [Header("Attack")]
    [Tooltip("攻击状态的持续时间。\n增大：攻击后摇变长，角色会被硬直更久。\n减小：攻击极快，可迅速接下一个动作。")]
    [SerializeField] private float attackDuration = 0.45f;

    [Tooltip("攻击或潜行时的移动速度衰减倍率。\n增大：攻击时仍能保持高速移动。\n减小：攻击时几乎原地站桩。")]
    [SerializeField, Range(0f, 1f)] private float walkSpeedMultiplier = 0.55f;

    [Header("Debug")]
    [Tooltip("在 Console 打印蓄力相关：输入按住、Action 是否启用 CanCharge、蓄力微阶段与归一化进度。")]
    [SerializeField] private bool debugChargeAttackFlow;

    [Tooltip("在 Console 打印意图仲裁与 Action 打断窗口判定日志。")]
    [SerializeField] private bool debugInterruptFlow;

    // (debugGroundProbe 已迁至 PlayerKCCMotor 组件)

    // ─── 运行时状态 ───

    private PlayerStateManager m_stateManager;
    /// <summary>R2：物理状态/方法已迁出到独立组件，Player 仅持引用做转发。</summary>
    private PlayerKCCMotor m_motor;
    private float m_attackTimer;
    private float m_dodgeCooldownTimer;
    private float m_swordDashCooldownTimer;
    private Vector3 m_movementIntent;
    private bool m_runIntent;
    private float m_runLatchEndTime;
    private bool m_isInitialized;

    // ─── ARPG 决策链：标签 + 意图缓冲（零 GC 路径，仅 struct 入队）───

    GameplayTagContainer m_gameplayTags;

    /// <summary>五轨语义容器（State / Status / Ability / Mechanic / Faction）。须通过本 ref 属性访问以免 struct 拷贝丢改。</summary>
    public ref GameplayTagContainer Tags => ref m_gameplayTags;

    /// <summary>State 轨别名：与历史代码及 <see cref="FrameContext.CurrentTags"/> 一致，存 <see cref="StateTag"/> 位。</summary>
    public ref GameplayTagMask GameplayTags => ref m_gameplayTags.State;

    /// <summary>离散意图队列（输入经语义化后入队，由状态机与状态消费）。</summary>
    public readonly GameplayIntentBuffer IntentBuffer = new GameplayIntentBuffer(16);

    [Header("Resources")]
    [SerializeField] private float maxStamina = 100f;

    [Header("Combat Data")]
    [Tooltip("当前武器招式表；轻/重/蓄力/翻滚/剑冲动作数据均从此注入。")]
    [SerializeField] private WeaponMovesetSO weaponMoveset;

    private float m_stamina;
    private int m_lightComboIndex;
    private int m_heavyComboIndex;
    private int m_chargedComboIndex;
    private GameplayIntentKind m_pendingActionKind;
    private ActionDataSO m_pendingAction;
    private bool m_pendingActionArmed;
    private bool m_jumpRequestedByIntent;

    // ─── 公开属性 ───

    public InputReader InputReader => inputReader;
    public PlayerStateManager States => m_stateManager;

    public Vector3 PlanarVelocity => m_motor != null ? m_motor.PlanarVelocity : Vector3.zero;
    public float VerticalSpeed => m_motor != null ? m_motor.VerticalSpeed : 0f;
    public bool IsGrounded => m_motor != null && m_motor.IsGrounded;
    public bool IsAttacking => m_attackTimer > 0f;

    public float Stamina => m_stamina;
    public float StaminaMax => maxStamina;
    public bool CanDodge => m_dodgeCooldownTimer <= 0f;
    public bool CanSwordDash => m_swordDashCooldownTimer <= 0f;
    public WeaponMovesetSO WeaponMoveset => weaponMoveset;
    public float AttackDuration => attackDuration;
    public float FallbackDodgeDurationSeconds => fallbackDodgeDurationSeconds;
    public float FallbackSwordDashDurationSeconds => fallbackSwordDashDurationSeconds;
    public float JumpForce => jumpForce;
    public float AirMoveMultiplier => airMoveMultiplier;
    public float WalkSpeedMultiplier => walkSpeedMultiplier;
    public bool HasMovementIntent => m_movementIntent.sqrMagnitude > 0.0001f;
    public bool WantsRun => m_runIntent;

    /// <summary>双击 W 等触发的短时跑步锁，与离散剑冲无关。</summary>
    public bool RunLatchActive => Time.time < m_runLatchEndTime;

    public void ActivateRunLatch(float durationSeconds)
    {
        m_runLatchEndTime = Time.time + Mathf.Max(0.01f, durationSeconds);
    }
    public bool WantsWalk => HasMovementIntent && !m_runIntent;
    public Vector3 MovementIntent => m_movementIntent;
    public bool DebugInterruptFlow => debugInterruptFlow;

    /// <summary>
    /// 原地转身表现层快照，由 PlayerLocomotionState 每帧通过 <see cref="SetTurnInfo"/> 写入；
    /// PlayerAnimController 根据其值决定是否切到 TurnLeft/Right 90/180 动画切片。
    /// </summary>
    private TurnInfo m_currentTurnInfo;
    public TurnInfo CurrentTurnInfo => m_currentTurnInfo;
    public void SetTurnInfo(in TurnInfo info) => m_currentTurnInfo = info;

    public float NormalizedSpeed
    {
        get
        {
            var cap = RuntimeStats.RunSpeed;
            return cap > 0.01f ? Mathf.Clamp01(PlanarVelocity.magnitude / cap) : 0f;
        }
    }

    // ─── 生命周期 ───

    protected override void Awake() {
        base.Awake();
        if (statsBlueprint is PlayerStatsSO ps) {
            maxStamina = Mathf.Max(1f, ps.MaxStamina);
        }
        Init();
    }

    private void Init() {
        if (m_isInitialized) return;

        m_isInitialized = true;
        m_stateManager = GetComponent<PlayerStateManager>();
        m_motor = GetComponent<PlayerKCCMotor>();
        if (m_motor != null)
        {
            m_motor.Bind(this, m_stateManager, debugInterruptFlow);
            m_motor.RefreshInitialGroundedState();
        }

        m_stamina = maxStamina;
    }

    void OnEnable()
    {

    }

    /// <summary>构建本帧只读上下文，供意图仲裁与状态查询。</summary>
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
            StaminaCurrent = m_stamina,
            StaminaMax = maxStamina,
            IsPrimaryAttackHeld = attackHeld,
        };
    }

    public void EnqueueGameplayIntent(in GameplayIntent intent)
    {
        IntentBuffer.Enqueue(intent);
    }

    /// <summary>由 Locomotion / Airborne 在切换 Action 支柱前写入待播动作。</summary>
    public void ArmPendingAction(GameplayIntentKind kind, ActionDataSO action)
    {
        m_pendingActionArmed = true;
        m_pendingActionKind = kind;
        m_pendingAction = action;
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

    public void RequestJumpFromIntent()
    {
        m_jumpRequestedByIntent = true;
    }

    public bool ConsumeJumpFromIntent()
    {
        if (!m_jumpRequestedByIntent)
        {
            return false;
        }

        m_jumpRequestedByIntent = false;
        return true;
    }

    // ─── IDamageable ───

    public void TakeDamage(DamageInfo info) {
        TakeDamage(info.Amount, info.Source);
        if (IsDead) {
            States.Change<PlayerDeadState>();
        }
    }

    // ─── 移动意图（由 PlayerController 设置） ───

    public void SetMovementIntent(Vector3 worldDirection, bool wantsRun) {
        var planar = new Vector3(worldDirection.x, 0f, worldDirection.z);
        if (planar.sqrMagnitude > 1f) planar.Normalize();

        m_movementIntent = planar;
        m_runIntent = wantsRun && planar.sqrMagnitude > 0.0001f;
    }

    public Vector3 GetMovementDirectionOrForward() {
        return HasMovementIntent ? m_movementIntent.normalized : Forward;
    }

    // ─── 移动能力 ───

    /// <summary>
    /// 地面/空中通用：按 <see cref="RuntimeEntityStats"/> 的走跑上限与输入幅度求目标速，再经加速度曲线逼近。
    /// </summary>
    /// <param name="externalSpeedMultiplier">空中控制、攻击减速等额外乘子。</param>
    /// <param name="wantsRun">为真使用 RunSpeed 上限，否则 WalkSpeed。</param>
    public void MoveByLocomotionIntent(float externalSpeedMultiplier, bool wantsRun) {
        var input = m_movementIntent;
        var hasInput = input.sqrMagnitude > 0.0001f;
        var inputMag = hasInput ? Mathf.Clamp01(input.magnitude) : 0f;
        var speedCap = wantsRun ? RuntimeStats.RunSpeed : RuntimeStats.WalkSpeed;
        var targetSpeed = hasInput ? speedCap * inputMag * Mathf.Max(0f, externalSpeedMultiplier) : 0f;
        var accel = hasInput ? moveAcceleration : moveDeceleration;

        var planar = PlanarVelocity;
        var currentSpeed = planar.magnitude;
        var newSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * Time.deltaTime);

        if (hasInput) {
            SetPlanarVelocity(input.normalized * newSpeed);
            SetMoveDirection(input);
            LookAtDirection(input);
        } else {
            var dir = currentSpeed > 0.01f ? planar.normalized : Vector3.zero;
            SetPlanarVelocity(dir * newSpeed);
        }
    }

    public void StopMove() {
        SetPlanarVelocity(Vector3.MoveTowards(PlanarVelocity, Vector3.zero, moveDeceleration * Time.deltaTime));
    }

    /// <summary>清空平面惯性（forwarder → PlayerKCCMotor）。</summary>
    public void ClearPlanarVelocity() => m_motor?.ClearPlanarVelocity();

    /// <summary>覆盖水平速度（IPlayerMotor forwarder）。</summary>
    public void SetPlanarVelocity(Vector3 planar) => m_motor?.SetPlanarVelocity(planar);

    /// <summary>覆盖垂直速度（IPlayerMotor forwarder）。</summary>
    public void SetVerticalSpeed(float vy) => m_motor?.SetVerticalSpeed(vy);

    // ─── 跳跃能力 ───

    public void Jump() {
        m_motor?.Jump(jumpForce);
        PublishEvent(new PlayerJumpEvent(GetInstanceID(), name));
    }

    // ─── 闪避与攻击能力 (略去重复的 Timer 代码，保持不变) ───

    public void StartDodgeCooldown() => m_dodgeCooldownTimer = dodgeCooldown;

    public void StartSwordDashCooldown() => m_swordDashCooldownTimer = swordDashCooldown;

    /// <summary>翻滚与剑冲冷却在同一帧递减，避免状态里重复写计时逻辑。</summary>
    public void TickMobilityCooldowns()
    {
        if (m_dodgeCooldownTimer > 0f)
        {
            m_dodgeCooldownTimer -= Time.deltaTime;
        }

        if (m_swordDashCooldownTimer > 0f)
        {
            m_swordDashCooldownTimer -= Time.deltaTime;
        }
    }

    public ActionDataSO ResolveDodgeActionFromMoveset()
    {
        return weaponMoveset != null ? weaponMoveset.DodgeAction : null;
    }

    public ActionDataSO ResolveSwordDashActionFromMoveset()
    {
        return weaponMoveset != null ? weaponMoveset.SwordDashAction : null;
    }

    public ActionDataSO ResolveLightAttackForCombo()
    {
        if (weaponMoveset == null || weaponMoveset.LightAttacks == null || weaponMoveset.LightAttacks.Length == 0)
        {
            return null;
        }

        return weaponMoveset.LightAttacks[m_lightComboIndex % weaponMoveset.LightAttacks.Length];
    }

    public ActionDataSO ResolveHeavyAttackForCombo()
    {
        if (weaponMoveset == null || weaponMoveset.HeavyAttacks == null || weaponMoveset.HeavyAttacks.Length == 0)
        {
            return null;
        }

        return weaponMoveset.HeavyAttacks[m_heavyComboIndex % weaponMoveset.HeavyAttacks.Length];
    }

    public ActionDataSO ResolveChargedAttackForCombo()
    {
        if (weaponMoveset == null || weaponMoveset.ChargedAttacks == null || weaponMoveset.ChargedAttacks.Length == 0)
        {
            return null;
        }

        return weaponMoveset.ChargedAttacks[m_chargedComboIndex % weaponMoveset.ChargedAttacks.Length];
    }

    /// <summary>轻攻击完整播完后调用以推进连段索引。</summary>
    public void AdvanceLightComboIndex()
    {
        if (weaponMoveset == null || weaponMoveset.LightAttacks == null || weaponMoveset.LightAttacks.Length == 0)
        {
            return;
        }

        m_lightComboIndex = (m_lightComboIndex + 1) % weaponMoveset.LightAttacks.Length;
    }

    /// <summary>蓄力攻击完整播完后推进连段索引。</summary>
    public void AdvanceChargedComboIndex()
    {
        if (weaponMoveset == null || weaponMoveset.ChargedAttacks == null || weaponMoveset.ChargedAttacks.Length == 0)
        {
            return;
        }

        m_chargedComboIndex = (m_chargedComboIndex + 1) % weaponMoveset.ChargedAttacks.Length;
    }

    public void ResetComboIndices()
    {
        m_lightComboIndex = 0;
        m_heavyComboIndex = 0;
        m_chargedComboIndex = 0;
    }

    /// <param name="durationOverride">小于 0 时使用 Inspector 中的 attackDuration。</param>
    public void BeginAttack(float durationOverride = -1f)
    {
        m_attackTimer = durationOverride > 0f ? durationOverride : attackDuration;
        PublishEvent(new PlayerAttackStartedEvent(GetInstanceID(), name));
    }

    /// <summary>
    /// 攻击结束由逻辑层（如归一化时间到 1）调用 <see cref="ForceEndAttackIfActive"/>；本帧不跑 <see cref="TickAttackTimer"/>。
    /// </summary>
    public void BeginAttackWithManualCompletion()
    {
        m_attackTimer = float.MaxValue;
        PublishEvent(new PlayerAttackStartedEvent(GetInstanceID(), name));
    }

    public void TickAttackTimer() {
        if (m_attackTimer <= 0f) return;
        m_attackTimer -= Time.deltaTime;
        if (m_attackTimer <= 0f) {
            m_attackTimer = 0f;
            PublishEvent(new PlayerAttackEndedEvent(GetInstanceID(), name));
        }
    }

    /// <summary>被外部打断（如死亡切状态）时强制结束攻击段并补发结束事件。</summary>
    public void ForceEndAttackIfActive()
    {
        if (m_attackTimer <= 0f)
        {
            return;
        }

        m_attackTimer = 0f;
        PublishEvent(new PlayerAttackEndedEvent(GetInstanceID(), name));
    }

    // ─── 物理驱动（R2：实现迁出到 PlayerKCCMotor，本节仅保留 forwarder API）───

    public bool IsGravitySuspended => m_motor != null && m_motor.IsGravitySuspended;

    /// <summary>动作 OnEnter 按 MotionProfile.GravityBehavior 决定挂起，OnExit 释放。幂等。</summary>
    public void SuspendGravity() => m_motor?.SuspendGravity();

    /// <summary>释放重力——必须由 OnExit 等成对调用。幂等。</summary>
    public void ReleaseGravity() => m_motor?.ReleaseGravity();

    public void SetActionAirborneLock(bool locked) => m_motor?.SetActionAirborneLock(locked);

    public void BeginActionMotorSession() => m_motor?.BeginActionMotorSession();

    public void EndActionMotorSession() => m_motor?.EndActionMotorSession();

    public void ApplySimpleGravity() => m_motor?.ApplySimpleGravity();

    public void ApplyMotor(in MotorSolveContext context) => m_motor?.ApplyMotor(in context);

    public void ApplyMotorFromGameplayVelocity(Vector3 gameplayWorldVelocity, in MotorSolveContext context)
        => m_motor?.ApplyMotorFromGameplayVelocity(gameplayWorldVelocity, in context);

    public MotorSolveContext BuildActionMotorSolveContext()
        => m_motor != null ? m_motor.BuildActionMotorSolveContext() : MotorSolveContext.Locomotion;

    /// <summary>
    /// 离散瞬移：可选障碍物截断；默认俯视射线贴地与 Locomotion 接地刷新（forwarder → PlayerKCCMotor）。
    /// </summary>
    /// <param name="forceAirborne">true：跳过贴地射线、保留 vy、用 Airborne 上下文（无 Hard Snap）。</param>
    public void TeleportTo(Vector3 worldPosition, bool forceAirborne = false)
        => m_motor?.TeleportTo(worldPosition, forceAirborne);

}

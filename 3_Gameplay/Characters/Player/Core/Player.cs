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

    [Tooltip("重力加速度。\n增大：下落极快，跳跃手感更“沉重”。\n减小：下落缓慢，有“月球漫步”的漂浮感。")]
    [SerializeField] private float gravity = 30f;

    [Tooltip("跳跃时的初始向上速度。\n增大：跳得更高。\n减小：跳得更矮。")]
    [SerializeField] private float jumpForce = 12f;

    [Tooltip("空中移动控制力的乘数 (0~1)。\n增大：在空中可以灵活改变方向（像超级马里奥）。\n减小：起跳后几乎无法改变落点（偏写实）。")]
    [SerializeField] private float airMoveMultiplier = 0.6f;

    [Tooltip("角色 Pivot (中心点) 到脚底的精确垂直距离。\n增大：系统会认为你的脚更长，角色会整体悬浮在空中。\n减小：系统认为你的脚更短，角色小腿会陷入地里。")]
    [SerializeField] private float pivotToFootOffset = 0.4f;

    [Tooltip("向下探测地面的额外距离。\n增大：下楼梯或下坡时更容易吸附在地面上，不易触发腾空。\n减小：稍微有一点坡度就会被判定为离地腾空。")]
    [SerializeField] private float groundCheckDistance = 0.35f;

    [Tooltip("球形射线的半径。\n增大：不容易漏掉地面的小坑洼或细小边缘，但容易卡住墙角。\n减小：探测更精准，但如果刚好踩在两条缝隙中间可能会漏判。")]
    [SerializeField] private float groundProbeRadius = 0.25f;

    [Tooltip("哪些层级被判定为地面。必须正确设置，否则永远浮空！")]
    [SerializeField] private LayerMask groundLayers = ~0;

    [Header("Kinematic Motor (optional)")]
    [Tooltip("非空：CapsuleSweep + Collide-and-Slide；地面策略由 MotorSolveContext + 合法坡角门控。\n留空：v3.2.8 Transform 直加 + 始终硬吸附（含凸角振荡风险）。")] [SerializeField]
    private MotorSettingsSO motorSettings;

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

    [Tooltip("在 Console 打印地面探针：主命中、二次稳定下探、最终 IsGrounded（受节流，避免刷屏）。")]
    [SerializeField] private bool debugGroundProbe;

    // ─── 运行时状态 ───

    private PlayerStateManager m_stateManager;
    private Vector3 m_planarVelocity;
    private float m_verticalSpeed;
    private float m_attackTimer;
    private float m_dodgeCooldownTimer;
    private float m_swordDashCooldownTimer;
    private Vector3 m_movementIntent;
    private bool m_runIntent;
    private float m_runLatchEndTime;
    private bool m_isInitialized;
    private float m_nextGroundProbeLogTime;

    private Vector3 m_lastGroundNormal = Vector3.up;

    /// <summary>上一逻辑帧结束时是否接地（用于马达二次下探稳定，减轻低台阶蹭起后的单帧 IsGrounded 抖动）。</summary>
    private bool m_wasGroundedLastFrame;

    /// <summary>Legacy 双轨：未挂 <see cref="MotorSettingsSO"/> 时用固定最大坡角拒绝「凸角尖」当支撑面。</summary>
    private const float MaxSlopeDegreesLegacy = 50f;

    // ─── ARPG 决策链：标签 + 意图缓冲（零 GC 路径，仅 struct 入队）───

    /// <summary>当前帧用于仲裁与窗口判定的标签快照（各状态在 OnLogicUpdate 内维护）。</summary>
    public GameplayTagMask GameplayTags;

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

    public Vector3 PlanarVelocity => m_planarVelocity;
    public float VerticalSpeed => m_verticalSpeed;
    public bool IsGrounded { get; private set; }
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
    public bool UsesKinematicMotorPipeline => motorSettings != null;
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

    public float NormalizedSpeed
    {
        get
        {
            var cap = RuntimeStats.RunSpeed;
            return cap > 0.01f ? Mathf.Clamp01(m_planarVelocity.magnitude / cap) : 0f;
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

        m_stamina = maxStamina;
        RefreshGroundedState(MotorSolveContext.Locomotion);
    }

    void OnEnable()
    {

    }

    /// <summary>构建本帧只读上下文，供意图仲裁与状态查询。</summary>
    public FrameContext BuildFrameContext(float deltaTime)
    {
        var attackHeld = inputReader != null && inputReader.IsAttackHeld;

        return new FrameContext
        {
            Time = Time.time,
            DeltaTime = deltaTime,
            IsGrounded = IsGrounded,
            PlanarVelocity = m_planarVelocity,
            CurrentPlanarSpeed = m_planarVelocity.magnitude,
            VerticalSpeed = m_verticalSpeed,
            CurrentTags = GameplayTags,
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

        var currentSpeed = m_planarVelocity.magnitude;
        var newSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * Time.deltaTime);

        if (hasInput) {
            m_planarVelocity = input.normalized * newSpeed;
            SetMoveDirection(input);
            LookAtDirection(input);
        } else {
            var dir = currentSpeed > 0.01f ? m_planarVelocity.normalized : Vector3.zero;
            m_planarVelocity = dir * newSpeed;
        }
    }

    public void StopMove() {
        m_planarVelocity = Vector3.MoveTowards(m_planarVelocity, Vector3.zero, moveDeceleration * Time.deltaTime);
    }

    /// <summary>
    /// 清空平面惯性，避免因上一帧跑动残留速度与「仅播动画」的 Dodge/SwordDash 语义打架。
    /// </summary>
    public void ClearPlanarVelocity() {
        m_planarVelocity = Vector3.zero;
    }

    // ─── 跳跃能力 ───

    public void Jump() {
        m_verticalSpeed = jumpForce;
        // 起跳瞬间强制离地，防止由于吸附机制被瞬间拉回地面
        IsGrounded = false;
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

    // ─── 物理驱动 ───

    /// <summary>
    /// 重力挂起标志：由 PlayerActionState 在动作 OnEnter 时按 MotionProfile.GravityBehavior 决定，
    /// 在 OnExit 时强制释放（成对配对，幂等）。
    /// 设计原则：Player 只暴露开关与读写口，不感知是哪个动作触发的——保持能力执行器的纯粹性。
    /// </summary>
    private bool m_gravitySuspended;

    public bool IsGravitySuspended => m_gravitySuspended;

    /// <summary>
    /// 挂起重力：垂直速度立即清零（防止已积累的下落速度在解除瞬间表现为瞬移）。
    /// 已挂起时再次调用是幂等的。
    /// </summary>
    public void SuspendGravity()
    {
        if (m_gravitySuspended)
        {
            if (debugInterruptFlow)
            {
                Debug.Log($"[GravitySuspend] Skip (already suspended) | y={transform.position.y:F3}", this);
            }
            return;
        }
        m_gravitySuspended = true;
        m_verticalSpeed = 0f;
        if (debugInterruptFlow)
        {
            Debug.Log($"[GravitySuspend] Applied | y={transform.position.y:F3} | grounded={IsGrounded}", this);
        }
    }

    /// <summary>释放重力——必须由 OnExit 等成对调用确保不会卡住飞行状态。幂等。</summary>
    public void ReleaseGravity()
    {
        if (debugInterruptFlow)
        {
            Debug.Log($"[GravitySuspend] Release | wasSuspended={m_gravitySuspended} | y={transform.position.y:F3} | grounded={IsGrounded}", this);
        }
        m_gravitySuspended = false;
    }

    public void ApplySimpleGravity() {
        // 重力被高层挂起：垂直速度强制为 0，跳过本帧重力累加
        if (m_gravitySuspended) {
            m_verticalSpeed = 0f;
            return;
        }
        // 如果在地上且没有向上运动的趋势，锁定垂直速度为0
        if (IsGrounded && m_verticalSpeed <= 0f) {
            m_verticalSpeed = 0f;
        } else {
            m_verticalSpeed -= gravity * Time.deltaTime;
        }
    }

    public void ApplyMotor(in MotorSolveContext context)
    {
        ApplySimpleGravity();
        ClampTerminalVelocityVertical();

        var velocity = new Vector3(m_planarVelocity.x, m_verticalSpeed, m_planarVelocity.z);

        if (UsesKinematicMotorPipeline)
        {
            velocity = MaybeProjectVelocityOntoWalkableSlope(velocity, context);
            var displacement = velocity * Time.deltaTime;
            var delta = KinematicMotorSolver.SolveDisplacementFromPivot(
                transform.position,
                pivotToFootOffset,
                motorSettings,
                displacement,
                motorSettings.ObstacleLayers);
            transform.position += delta;
        }
        else
        {
            transform.position += velocity * Time.deltaTime;
        }

        RefreshGroundedState(in context);
    }

    public void ApplyMotorFromGameplayVelocity(Vector3 gameplayWorldVelocity, in MotorSolveContext context)
    {
        ApplySimpleGravity();
        ClampTerminalVelocityVertical();

        var velocity = gameplayWorldVelocity;
        if (!m_gravitySuspended)
        {
            if (Mathf.Abs(gameplayWorldVelocity.y) < 0.001f)
            {
                velocity = new Vector3(gameplayWorldVelocity.x, m_verticalSpeed, gameplayWorldVelocity.z);
            }
        }
        else
        {
            m_verticalSpeed = 0f;
        }

        if (UsesKinematicMotorPipeline)
        {
            velocity = MaybeProjectVelocityOntoWalkableSlope(velocity, context);
            var displacement = velocity * Time.deltaTime;
            var delta = KinematicMotorSolver.SolveDisplacementFromPivot(
                transform.position,
                pivotToFootOffset,
                motorSettings,
                displacement,
                motorSettings.ObstacleLayers);
            transform.position += delta;
        }
        else
        {
            transform.position += velocity * Time.deltaTime;
        }

        RefreshGroundedState(in context);
    }

    public MotorSolveContext BuildActionMotorSolveContext()
    {
        return IsGravitySuspended
            ? MotorSolveContext.ActionSuspendedPhysics
            : IsGrounded
                ? MotorSolveContext.Locomotion
                : MotorSolveContext.Airborne;
    }

    private void ClampTerminalVelocityVertical()
    {
        if (motorSettings == null || m_gravitySuspended)
        {
            return;
        }

        m_verticalSpeed = Mathf.Max(m_verticalSpeed, -Mathf.Abs(motorSettings.TerminalVelocity));
    }

    private Vector3 MaybeProjectVelocityOntoWalkableSlope(Vector3 worldVelocity, in MotorSolveContext context)
    {
        if (!context.AllowsHardGroundSnap || motorSettings == null || !motorSettings.EnablesSlopeProjection)
        {
            return worldVelocity;
        }

        if (!IsGrounded || m_lastGroundNormal.sqrMagnitude < 0.25f)
        {
            return worldVelocity;
        }

        var frameDisp = worldVelocity * Time.deltaTime;
        var projectedDisp = KinematicMotorSolver.ProjectDisplacementOntoGroundPlaneIfWalkable(
            frameDisp,
            m_lastGroundNormal,
            motorSettings);
        return projectedDisp / Mathf.Max(Time.deltaTime, 1e-5f);
    }

    /// <summary>
    /// 离散瞬移：俯视射线落合法坡；随后按行走语义刷新 grounded（双轨）。
    /// </summary>
    public void TeleportTo(Vector3 worldPosition)
    {
        var destination = worldPosition;

        if (motorSettings != null && motorSettings.TeleportRayMaxDistance > 0.001f)
        {
            if (KinematicMotorSolver.ResolvePivotHeightFromAbove(
                    destination.x,
                    destination.z,
                    destination.y,
                    motorSettings.TeleportRayStartAbove,
                    motorSettings.TeleportRayMaxDistance,
                    pivotToFootOffset,
                    groundLayers,
                    motorSettings,
                    out var yResolved))
            {
                destination.y = yResolved;
            }
        }

        transform.position = destination;
        m_planarVelocity = Vector3.zero;
        m_verticalSpeed = 0f;
        RefreshGroundedState(MotorSolveContext.Locomotion);
        PublishEvent(new PlayerTeleportedEvent(GetInstanceID(), name, destination));
    }

    // ─── 内部辅助 ───

    private void RefreshGroundedState(in MotorSolveContext context)
    {
        var footPos = transform.position - Vector3.up * pivotToFootOffset;
        var probeRadius = Mathf.Max(groundProbeRadius, motorSettings != null ? motorSettings.Radius : 0f);

        var startOffset = probeRadius + 0.1f;
        var origin = footPos + Vector3.up * startOffset;

        var castReach = motorSettings != null && context.AllowsHardGroundSnap
            ? Mathf.Max(groundCheckDistance, motorSettings.GroundSnapDistance)
            : groundCheckDistance;
        var castDistance = 0.1f + castReach;

        RaycastHit hit = default;
        var hitGround = Physics.SphereCast(
            origin,
            probeRadius,
            Vector3.down,
            out hit,
            castDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore);

        var usedExtraStabilizationProbe = false;
        if (!hitGround
            && motorSettings != null
            && motorSettings.EnableExtraGroundStabilizationProbe
            && context.AllowsHardGroundSnap
            && m_wasGroundedLastFrame
            && m_verticalSpeed <= motorSettings.GroundSnapMaxUpwardVelocity)
        {
            var extDist = castDistance + motorSettings.ExtraGroundProbeExtension;
            hitGround = Physics.SphereCast(
                origin,
                probeRadius,
                Vector3.down,
                out hit,
                extDist,
                groundLayers,
                QueryTriggerInteraction.Ignore);
            usedExtraStabilizationProbe = hitGround;
        }

        Debug.DrawRay(origin, Vector3.down * castDistance, Color.red);
        if (hitGround)
        {
            Debug.DrawLine(origin, hit.point, usedExtraStabilizationProbe ? new Color(1f, 0.45f, 0.1f) : Color.green);
        }

        if (debugGroundProbe && Time.unscaledTime >= m_nextGroundProbeLogTime)
        {
            m_nextGroundProbeLogTime = Time.unscaledTime + 0.15f;
            Debug.Log(
                $"[GroundProbe] hit={hitGround} extraProbe={usedExtraStabilizationProbe} " +
                $"wasGrounded={m_wasGroundedLastFrame} vy={m_verticalSpeed:F3} cast={castDistance:F3} " +
                $"snapPolicy={context.AllowsHardGroundSnap} pt={(hitGround ? hit.point.ToString() : "-")}",
                this);
        }

        var airborneSlopBand = motorSettings != null ? motorSettings.AirborneGroundContactSlop : float.MaxValue;
        var groundedByProximity = motorSettings != null && !context.AllowsHardGroundSnap
            ? hitGround && hit.distance <= airborneSlopBand
            : hitGround;

        var useHardSnapLegacy = motorSettings == null || context.AllowsHardGroundSnap;

        if (!groundedByProximity || m_verticalSpeed > 0f)
        {
            IsGrounded = false;
            if (!hitGround)
            {
                m_lastGroundNormal = Vector3.up;
            }

            m_wasGroundedLastFrame = IsGrounded;
            return;
        }

        if (motorSettings != null && hitGround && motorSettings.IsSlopeTooSteep(hit.normal))
        {
            IsGrounded = false;
            m_lastGroundNormal = Vector3.up;
            m_wasGroundedLastFrame = IsGrounded;
            return;
        }

        if (motorSettings == null && hitGround &&
            Vector3.Angle(Vector3.up, hit.normal) > MaxSlopeDegreesLegacy)
        {
            IsGrounded = false;
            m_lastGroundNormal = Vector3.up;
            m_wasGroundedLastFrame = IsGrounded;
            return;
        }

        m_lastGroundNormal = hitGround ? hit.normal : Vector3.up;
        IsGrounded = true;
        m_verticalSpeed = 0f;

        if (!useHardSnapLegacy)
        {
            m_wasGroundedLastFrame = IsGrounded;
            return;
        }

        var targetY = hit.point.y + pivotToFootOffset;

        transform.position = new Vector3(
            transform.position.x,
            targetY,
            transform.position.z);

        m_wasGroundedLastFrame = IsGrounded;
    }
}
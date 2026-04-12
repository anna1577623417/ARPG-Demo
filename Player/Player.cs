using UnityEngine;

/// <summary>
/// 玩家实体。
/// 纯 Transform 驱动的移动系统。逻辑（状态机）与表现（动画管理器）解耦。
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

    // ─── 闪避参数 ───

    [Header("Dodge")]
    [Tooltip("闪避时的移动速度。\n增大：闪避位移更远、更迅猛。\n减小：闪避位移短，像碎步。")]
    [SerializeField] private float dodgeSpeed = 15f;

    [Tooltip("闪避动作的持续时间（秒）。\n增大：无敌帧或位移时间变长，动作变慢。\n减小：闪避极其短促。")]
    [SerializeField] private float dodgeDuration = 0.3f;

    [Tooltip("连续两次闪避之间的冷却时间。\n增大：无法连续频繁翻滚，限制机动性。\n减小：可以疯狂连续翻滚。")]
    [SerializeField] private float dodgeCooldown = 0.8f;

    // ─── 攻击参数 ───

    [Header("Attack")]
    [Tooltip("攻击状态的持续时间。\n增大：攻击后摇变长，角色会被硬直更久。\n减小：攻击极快，可迅速接下一个动作。")]
    [SerializeField] private float attackDuration = 0.45f;

    [Tooltip("攻击或潜行时的移动速度衰减倍率。\n增大：攻击时仍能保持高速移动。\n减小：攻击时几乎原地站桩。")]
    [SerializeField, Range(0f, 1f)] private float walkSpeedMultiplier = 0.55f;

    // ─── 运行时状态 ───

    private PlayerStateManager m_stateManager;
    private Vector3 m_planarVelocity;
    private float m_verticalSpeed;
    private float m_attackTimer;
    private float m_dodgeCooldownTimer;
    private bool m_isMoving;
    private Vector3 m_movementIntent;
    private bool m_runIntent;
    private bool m_attackIntent;
    private bool m_isInitialized;

    // ─── ARPG 决策链：标签 + 意图缓冲（零 GC 路径，仅 struct 入队）───

    /// <summary>当前帧用于仲裁与窗口判定的标签快照（各状态在 OnLogicUpdate 内维护）。</summary>
    public GameplayTagMask GameplayTags;

    /// <summary>离散意图队列（输入经语义化后入队，由状态机与状态消费）。</summary>
    public readonly GameplayIntentBuffer IntentBuffer = new GameplayIntentBuffer(16);

    [Header("Resources")]
    [SerializeField] private float maxStamina = 100f;

    private float m_stamina;
    private GameplayIntentKind m_pendingActionKind;
    private ActionDataSO m_pendingAction;
    private bool m_pendingActionArmed;
    private bool m_jumpRequestedByIntent;

    // ─── 公开属性 ───

    public InputReader InputReader => inputReader;
    public PlayerStateManager States => m_stateManager;

    public Vector3 PlanarVelocity => m_planarVelocity;
    public bool IsGrounded { get; private set; }
    public bool IsAttacking => m_attackTimer > 0f;

    public float Stamina => m_stamina;
    public float StaminaMax => maxStamina;
    public bool CanDodge => m_dodgeCooldownTimer <= 0f;
    public float AttackDuration => attackDuration;
    public float DodgeSpeed => dodgeSpeed;
    public float DodgeDuration => dodgeDuration;
    public float JumpForce => jumpForce;
    public float AirMoveMultiplier => airMoveMultiplier;
    public float WalkSpeedMultiplier => walkSpeedMultiplier;
    public bool HasMovementIntent => m_movementIntent.sqrMagnitude > 0.0001f;
    public bool WantsRun => m_runIntent;
    public bool WantsWalk => HasMovementIntent && !m_runIntent;
    public Vector3 MovementIntent => m_movementIntent;

    public float NormalizedSpeed => BaseMoveSpeed > 0.01f
        ? m_planarVelocity.magnitude / BaseMoveSpeed
        : 0f;

    // ─── 生命周期 ───

    protected override void Awake() {
        base.Awake();
        Init();
    }

    private void Init() {
        if (m_isInitialized) return;

        m_isInitialized = true;
        m_stateManager = GetComponent<PlayerStateManager>();

        m_stamina = maxStamina;
        RefreshGroundedState();
    }

    /// <summary>构建本帧只读上下文，供意图仲裁与状态查询。</summary>
    public FrameContext BuildFrameContext(float deltaTime)
    {
        return new FrameContext
        {
            Time = Time.time,
            DeltaTime = deltaTime,
            IsGrounded = IsGrounded,
            PlanarVelocity = m_planarVelocity,
            VerticalSpeed = m_verticalSpeed,
            CurrentTags = GameplayTags,
            StaminaCurrent = m_stamina,
            StaminaMax = maxStamina,
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

    public void SetAttackIntent(bool pressed) {
        if (pressed) m_attackIntent = true;
    }

    public bool ConsumeAttackIntent() {
        if (!m_attackIntent) return false;
        m_attackIntent = false;
        return true;
    }

    // ─── 移动能力 ───

    public void MoveByInput(float speedMultiplier = 1f) {
        var input = m_movementIntent;
        var hasInput = input.sqrMagnitude > 0.0001f;
        var targetSpeed = hasInput ? BaseMoveSpeed * Mathf.Max(0f, speedMultiplier) : 0f;
        var accel = hasInput ? moveAcceleration : moveDeceleration;

        // ─── 速度大小：平滑插值（起步/刹车有惯性感）───
        var currentSpeed = m_planarVelocity.magnitude;
        var newSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * Time.deltaTime);

        // ─── 移动方向：立即跟随输入（不做方向延迟）───
        // 转向视觉由 LookAtDirection 的 RotateTowards 平滑处理，
        // 但实际位移方向立即响应，避免"身体朝左、脚往斜前滑"的割裂感。
        if (hasInput) {
            m_planarVelocity = input.normalized * newSpeed;
            SetMoveDirection(input);
            LookAtDirection(input);
        } else {
            // 无输入：保持当前方向匀减速（自然滑停）
            var dir = currentSpeed > 0.01f ? m_planarVelocity.normalized : Vector3.zero;
            m_planarVelocity = dir * newSpeed;
        }

        PublishMoveEvents(hasInput);
    }

    public void StopMove() {
        m_planarVelocity = Vector3.MoveTowards(m_planarVelocity, Vector3.zero, moveDeceleration * Time.deltaTime);
        PublishMoveEvents(false);
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
    public void TickDodgeCooldown() { if (m_dodgeCooldownTimer > 0f) m_dodgeCooldownTimer -= Time.deltaTime; }

    /// <param name="durationOverride">小于 0 时使用 Inspector 中的 attackDuration。</param>
    public void BeginAttack(float durationOverride = -1f)
    {
        m_attackTimer = durationOverride > 0f ? durationOverride : attackDuration;
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

    public void ApplySimpleGravity() {
        // 如果在地上且没有向上运动的趋势，锁定垂直速度为0
        if (IsGrounded && m_verticalSpeed <= 0f) {
            m_verticalSpeed = 0f;
        } else {
            m_verticalSpeed -= gravity * Time.deltaTime;
        }
    }

    public void ApplyMotor() {
        ApplySimpleGravity();

        Vector3 velocity = new Vector3(m_planarVelocity.x, m_verticalSpeed, m_planarVelocity.z);
        transform.position += velocity * Time.deltaTime;

        // 移动完毕后立刻进行地面检测与吸附修复
        RefreshGroundedState();
    }

    public void ApplyDodgeMotor(Vector3 direction) {
        ApplySimpleGravity();
        var velocity = direction * dodgeSpeed + Vector3.up * m_verticalSpeed;
        transform.position += velocity * Time.deltaTime;
        RefreshGroundedState();
    }

    // ─── 内部辅助 ───

    private void PublishMoveEvents(bool nowMoving) {
        if (nowMoving == m_isMoving) return;
        m_isMoving = nowMoving;

        if (m_isMoving)
            PublishEvent(new PlayerMoveStartedEvent(GetInstanceID(), name));
        else
            PublishEvent(new PlayerMoveStoppedEvent(GetInstanceID(), name));
    }

    /// <summary>
    /// 核心修复：坚如磐石的地面检测与吸附。
    /// 抛弃平滑移动，抛弃复杂的 Bounds 计算。使用直接的位置覆写。
    /// </summary>
    private void RefreshGroundedState() {
        // 1. 计算理论上的脚底中心点
        Vector3 footPos = transform.position - Vector3.up * pivotToFootOffset;

        // 2. SphereCast 起点：把球体稍微往上提一点点 (半径 + 0.1f安全距离)，防止球体直接刷新在地面以下导致检测失效
        float startOffset = groundProbeRadius + 0.1f;
        Vector3 origin = footPos + Vector3.up * startOffset;

        // 3. 射线长度：从起点往下，打穿安全距离，再多打一个探测距离
        float castDistance = 0.1f + groundCheckDistance;

        bool hitGround = Physics.SphereCast(
            origin,
            groundProbeRadius,
            Vector3.down,
            out RaycastHit hit,
            castDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore);

        // Debug 辅助线：红色为探测起点和方向，绿色为命中点
        Debug.DrawRay(origin, Vector3.down * castDistance, Color.red);
        if (hitGround) Debug.DrawLine(origin, hit.point, Color.green);

        // 如果什么都没打到，或者正在起跳上升(垂直速度 > 0)，则处于浮空状态
        if (!hitGround || m_verticalSpeed > 0f) {
            IsGrounded = false;
            return;
        }

        // 走到这里说明：打到地面了，并且当前是下落或平移状态 (m_verticalSpeed <= 0)
        IsGrounded = true;
        m_verticalSpeed = 0f;

        // 核心：直接进行硬吸附（Hard Snap）。
        // Transform 驱动不要做平滑吸附，直接赋予准确的 Y 值才是消除抖动和陷地的唯一正解。
        // hit.point.y 即为地面绝对高度。
        float targetY = hit.point.y + pivotToFootOffset;

        transform.position = new Vector3(
            transform.position.x,
            targetY,
            transform.position.z
        );
    }
}
using UnityEngine;

/// <summary>
/// 把 Stage R1 抽出的 <see cref="IPlayerMotor"/> 实现真正落到独立组件——KCC 物理状态/方法/Inspector 输入的唯一拥有者。
///
/// ═══ Stage R2 设计原则 ═══
///   · 9 道闸门（墙面消毒 / 下半球 / 半身墙 / 凹角 / 凸角 / 接地下沉锁 / Edge Slip /
///     中心射线兜底 / Action 物理脱地）保持原样迁入；行为零变化。
///   · Player 通过 RequireComponent 持有；所有马达 SerializeField 直接序列化在本组件上，
///     与 Player 完全分离。Player.Awake 仅传 (Player, States, debugFlag) 引用 / 触发首帧探针。
///   · MotionExecutor / 状态机仍走 Player 的 forwarder API；改路径不在本阶段。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerKCCMotor : MonoBehaviour, IPlayerMotor
{
    // ─── Inspector：物理 / 接地探针 ──────────────────────────────────────
    [Header("Gravity")]
    [Tooltip("重力加速度。\n增大：下落极快，跳跃手感更“沉重”。\n减小：下落缓慢，有“月球漫步”的漂浮感。")]
    [SerializeField] float gravity = 30f;

    [Header("Foot / Ground Probe")]
    [Tooltip("角色 Pivot (中心点) 到脚底的精确垂直距离。\n增大：系统会认为你的脚更长，角色会整体悬浮在空中。\n减小：系统认为你的脚更短，角色小腿会陷入地里。")]
    [SerializeField] float pivotToFootOffset = 0.4f;

    [Tooltip("向下探测地面的额外距离。\n增大：下楼梯或下坡时更容易吸附在地面上，不易触发腾空。\n减小：稍微有一点坡度就会被判定为离地腾空。")]
    [SerializeField] float groundCheckDistance = 0.35f;

    [Tooltip("球形射线的半径。\n增大：不容易漏掉地面的小坑洼或细小边缘，但容易卡住墙角。\n减小：探测更精准，但如果刚好踩在两条缝隙中间可能会漏判。")]
    [SerializeField] float groundProbeRadius = 0.25f;

    [Tooltip("哪些层级被判定为地面。\n不要用 Everything：探针起点常落在自身胶囊/子碰撞体内，会误命中自己 → Hard Snap 错位 → 陷地/抖动。\n推荐：仅勾选 Ground/Environment，角色所在 Layer 不要勾选。")]
    [SerializeField] LayerMask groundLayers = ~0;

    [Header("Edge grounding recovery")]
    [Tooltip("允许将'命中但法线过陡'且落点位于脚底中心附近的接触，当作边缘落地放行，避免顶点法线导致的假滞空。")]
    [SerializeField] bool enableEdgeGroundTolerance = true;

    [Tooltip("边缘落地放行时，命中点到脚底中心的水平容差（相对探针半径）。")]
    [SerializeField, Range(0.5f, 1.5f)] float edgeGroundToleranceRadiusScale = 1.05f;

    [Tooltip("边缘落地放行时，命中点高于脚底平面的最大容差（米）。")]
    [SerializeField, Range(0f, 0.1f)] float edgeGroundToleranceVerticalSlop = 0.03f;

    [Header("Airborne Edge Slip")]
    [Tooltip("反滞空：空中且垂直速度应下落却被碰撞吃掉时，沿边缘法线施加微小水平推离速度。")]
    [SerializeField] bool enableAirborneEdgeSlip = true;

    [Tooltip("反滞空滑落的水平推离速度（m/s）。")]
    [SerializeField, Range(0f, 2f)] float edgeSlipPushSpeed = 0.5f;

    [Tooltip("触发反滞空滑落所需的最小下落速度（m/s）。")]
    [SerializeField, Range(0f, 3f)] float edgeSlipMinFallingSpeed = 0.08f;

    [Tooltip("触发反滞空滑落时，求解后垂直速度阈值。高于该值（接近 0）视为'下落被吃掉'。")]
    [SerializeField, Range(-1f, 0.2f)] float edgeSlipSolvedVerticalSpeedThreshold = -0.02f;

    [Tooltip("连续多少帧检测到滞空死锁后，启用强制位移脱困（直接写入 Transform）。")]
    [SerializeField, Range(2, 20)] int edgeSlipForceUnstickFrameThreshold = 4;

    [Tooltip("强制脱困时，每帧的水平外推距离（米）。")]
    [SerializeField, Range(0f, 0.2f)] float edgeSlipForceUnstickHorizontalStep = 0.04f;

    [Tooltip("强制脱困时，每帧的向下位移（米），打破竖直死锁。")]
    [SerializeField, Range(0f, 0.2f)] float edgeSlipForceUnstickDownStep = 0.05f;

    [Header("Kinematic Motor (optional)")]
    [Tooltip("非空：CapsuleSweep + Collide-and-Slide；地面策略由 MotorSolveContext + 合法坡角门控。\n留空：v3.2.8 Transform 直加 + 始终硬吸附（含凸角振荡风险）。")]
    [SerializeField] MotorSettingsSO motorSettings;

    [Header("Debug")]
    [Tooltip("在 Console 打印地面探针：主命中、二次稳定下探、最终 IsGrounded（受节流，避免刷屏）。")]
    [SerializeField] bool debugGroundProbe;

    [Tooltip("开启后：进入 Play 起前若干帧打印 Pivot / 胶囊 / 探针 / 反推 offset（见 Console [MotorCalib]）。用于重测 Pivot To Foot Offset；测完务必关闭。")]
    [SerializeField] bool debugMotorCalibration;

    [SerializeField, Range(1, 120)] int debugMotorCalibrationLogFrames = 30;

    [Tooltip("勾选后：不写回 Transform（KCC 位移增量、HardSnap、去穿插挤出、EdgeSlip 强制位移均跳过）。仍会跑接地判定与 Log。\n用于静止读 [MotorCalib]；勿长期开启。")]
    [SerializeField] bool debugFreezeMotorDisplacement;

    [Header("Debug · Vertical authority (vy pipeline)")]
    [Tooltip("每个 ApplyMotor / ApplyMotorFromGameplayVelocity 打一帧 [VertAuthority]，用于看谁改了 vy。")]
    [SerializeField, InspectorName("VertAuthority · Enable")]
    bool debugVerticalAuthority;

    [Tooltip(
        "勾选：仅在当前状态为 PlayerActionState 时打印（站立 / 走路 / PlayerAirborneState 不会打印，看起来像「没日志」）。\n" +
        "取消勾选：Locomotion / Airborne 也会每帧打印（刷屏，仅排查时用）。")]
    [SerializeField, InspectorName("VertAuthority · Action State Only")]
    bool debugVerticalAuthorityActionOnly = true;

    // ─── 行为常量（与原 Player.cs 同名同值）────────────────────────────────
    const float MaxSlopeDegreesLegacy = 50f;
    const float ActionFallBreakGroundedVy = -0.01f;
    const float ActionEdgeSnapMaxHitDistance = 0.1f;
    const float ActionMotorAirborneVyThreshold = -0.1f;

    static readonly Collider[] s_groundSnapOverlapBuffer =
        new Collider[KinematicMotorSolver.DefaultOverlapBufferSize];

    /// <summary>地面 SphereCast / Raycast 非托管缓冲（避免 GC）；命中会在代码侧过滤掉自身层级。</summary>
    static readonly RaycastHit[] s_groundProbeHits = new RaycastHit[24];

    // ─── 引用（Player.Awake 通过 Bind 注入）────────────────────────────────
    Player _player;
    PlayerStateManager _states;
    bool _debugInterruptFlow;
    bool _bound;

    // ─── 物理状态 ──────────────────────────────────────────────────────────
    Vector3 _planarVelocity;
    float _verticalSpeed;
    bool _isGrounded;
    bool _gravitySuspended;

    /// <summary>动作支柱「空中起手锁」：true 期间禁止把 IsGrounded 写真、禁止清 vy、上下文恒 Airborne。</summary>
    bool _actionAirborneLock;

    /// <summary>动作持续期内冻结的 <see cref="MotorSolveContext"/>，避免每帧重算 context。</summary>
    bool _actionMotorSessionActive;

    MotorSolveContext _frozenActionMotorContext;
    Vector3 _lastGroundNormal = Vector3.up;
    bool _wasGroundedLastFrame;
    bool _hasSteepGroundHitForEdgeSlip;
    Vector3 _lastSteepGroundHitPoint;
    Vector3 _lastSteepGroundHitNormal = Vector3.up;
    int _edgeSlipStuckFrameCount;
    float _nextGroundProbeLogTime;

    // ─── IPlayerMotor 查询 ────────────────────────────────────────────────
    public bool IsGrounded => _isGrounded;
    public float VerticalSpeed => _verticalSpeed;
    public Vector3 PlanarVelocity => _planarVelocity;
    public bool IsGravitySuspended => _gravitySuspended;
    public Vector3 LastGroundNormal => _lastGroundNormal;
    public bool HasSteepGroundHitForEdgeSlip => _hasSteepGroundHitForEdgeSlip;

    /// <summary>由 Player.Init 注入引用与日志开关；不传任何马达参数（自身序列化）。</summary>
    public void Bind(Player player, PlayerStateManager states, bool debugInterruptFlow)
    {
        _player = player;
        _states = states;
        _debugInterruptFlow = debugInterruptFlow;
        _bound = true;
    }

    /// <summary>Player.Init 完成后调用一次，让首帧 IsGrounded 正确。</summary>
    public void RefreshInitialGroundedState()
    {
        if (!_bound) return;
        if (debugMotorCalibration && Time.frameCount < debugMotorCalibrationLogFrames)
        {
            Debug.Log(
                $"[MotorCalib] RefreshInitialGroundedState (spawn) id={GetInstanceID()} pivotY={transform.position.y:F4} offset={pivotToFootOffset:F4} freeze={debugFreezeMotorDisplacement}\n" +
                "  提示：Pivot 应在「脚底贴地」时高于地面约 pivotToFootOffset；若 Pivot 与地面对齐在同一 Y，逻辑脚底会在地面下方 → elevReject / 永不接地。",
                this);
        }

        RefreshGroundedState(MotorSolveContext.Locomotion);
    }

    // ─── IPlayerMotor 写口 ────────────────────────────────────────────────
    public void SetPlanarVelocity(Vector3 planar)
    {
        _planarVelocity = new Vector3(planar.x, 0f, planar.z);
    }

    public void SetVerticalSpeed(float vy)
    {
        _verticalSpeed = vy;
    }

    public void ClearPlanarVelocity()
    {
        _planarVelocity = Vector3.zero;
    }

    /// <summary>跳跃：写垂直速度并强制脱地（防止吸附拉回）。</summary>
    public void Jump(float jumpForce)
    {
        _verticalSpeed = jumpForce;
        _isGrounded = false;
    }

    public void SuspendGravity()
    {
        if (_gravitySuspended)
        {
            if (_debugInterruptFlow)
            {
                Debug.Log($"[GravitySuspend] Skip (already suspended) | y={transform.position.y:F3}", this);
            }
            return;
        }

        _gravitySuspended = true;
        _verticalSpeed = 0f;
        if (_debugInterruptFlow)
        {
            Debug.Log($"[GravitySuspend] Applied | y={transform.position.y:F3} | grounded={_isGrounded}", this);
        }
    }

    public void ReleaseGravity()
    {
        if (_debugInterruptFlow)
        {
            Debug.Log($"[GravitySuspend] Release | wasSuspended={_gravitySuspended} | y={transform.position.y:F3} | grounded={_isGrounded}", this);
        }

        _gravitySuspended = false;
    }

    public void SetActionAirborneLock(bool locked)
    {
        if (_actionAirborneLock == locked) return;
        _actionAirborneLock = locked;
        if (_debugInterruptFlow)
        {
            Debug.Log($"[ActionAirborneLock] {(locked ? "ENGAGE" : "RELEASE")} | y={transform.position.y:F3} | vy={_verticalSpeed:F3} | grounded={_isGrounded}", this);
        }
    }

    public void BeginActionMotorSession()
    {
        EndActionMotorSession();
        _frozenActionMotorContext = ComputeActionMotorSolveContext();
        _actionMotorSessionActive = true;
        if (_debugInterruptFlow)
        {
            Debug.Log(
                $"[ActionMotorSession] BEGIN snap={_frozenActionMotorContext.GroundingPolicy} allowHardSnap={_frozenActionMotorContext.AllowsHardGroundSnap} vy={_verticalSpeed:F3} grounded={_isGrounded}",
                this);
        }
    }

    public void EndActionMotorSession()
    {
        if (!_actionMotorSessionActive) return;
        _actionMotorSessionActive = false;
        if (_debugInterruptFlow)
        {
            Debug.Log($"[ActionMotorSession] END vy={_verticalSpeed:F3} grounded={_isGrounded}", this);
        }
    }

    /// <summary>
    /// 接地刷新后把会话快照与 <see cref="ComputeActionMotorSolveContext"/> 对齐。
    /// 修「起手接地 → 快照 Locomotion → 后半段已在空中仍 HardSnap」：离地后此处会把快照改为 PhysicsPassThrough。
    /// </summary>
    void RefreshFrozenActionMotorContextIfSession()
    {
        if (!_actionMotorSessionActive) return;
        _frozenActionMotorContext = ComputeActionMotorSolveContext();
    }

    // ─── IPlayerMotor 触发 ────────────────────────────────────────────────
    public void ApplyMotor(in MotorSolveContext context)
    {
        var vyEnter = _verticalSpeed;
        ApplySimpleGravity();
        var vyAfterGravity = _verticalSpeed;
        ClampTerminalVelocityVertical();
        var vyAfterClamp = _verticalSpeed;

        // 会话内：用瞬时推导上下文做本帧求解，勿用调用方传入的快照（可能仍是上一帧的 Locomotion）。
        var solveCtx = _actionMotorSessionActive ? ComputeActionMotorSolveContext() : context;

        var velocity = new Vector3(_planarVelocity.x, _verticalSpeed, _planarVelocity.z);
        var solvedDelta = velocity * Time.deltaTime;

        if (motorSettings != null)
        {
            velocity = MaybeProjectVelocityOntoWalkableSlope(velocity, in solveCtx);
            var displacement = velocity * Time.deltaTime;
            var applySlideYLock = ShouldApplyGroundedSlideDownwardLock(in solveCtx);
            solvedDelta = KinematicMotorSolver.SolveDisplacementFromPivot(
                transform.position,
                pivotToFootOffset,
                motorSettings,
                displacement,
                motorSettings.ObstacleLayers,
                applySlideYLock);
            if (!debugFreezeMotorDisplacement)
            {
                transform.position += solvedDelta;
            }
        }
        else
        {
            if (!debugFreezeMotorDisplacement)
            {
                transform.position += velocity * Time.deltaTime;
            }
        }

        ResolveMotorOverlapsIfNeeded();
        var vyAfterOverlap = _verticalSpeed;
        RefreshGroundedState(in solveCtx);
        RefreshFrozenActionMotorContextIfSession();
        var vyAfterGrounded = _verticalSpeed;
        ApplyAirborneEdgeSlipIfStuck(in solvedDelta);
        LogVerticalAuthorityIfEnabled("ApplyMotor", vyEnter, vyAfterGravity, vyAfterClamp, solvedDelta.y, vyAfterOverlap, vyAfterGrounded, in solveCtx);
    }

    public void ApplyMotorFromGameplayVelocity(Vector3 gameplayWorldVelocity, in MotorSolveContext context)
    {
        var vyEnter = _verticalSpeed;
        ApplySimpleGravity();
        var vyAfterGravity = _verticalSpeed;
        ClampTerminalVelocityVertical();
        var vyAfterClamp = _verticalSpeed;

        var velocity = gameplayWorldVelocity;
        var solvedDelta = velocity * Time.deltaTime;
        var gameplayDrivenVy = false;
        if (!_gravitySuspended)
        {
            if (Mathf.Abs(gameplayWorldVelocity.y) < 0.001f)
            {
                velocity = new Vector3(gameplayWorldVelocity.x, _verticalSpeed, gameplayWorldVelocity.z);
            }
            else
            {
                gameplayDrivenVy = true;
            }
        }
        else
        {
            _verticalSpeed = 0f;
        }
        var vyAfterGameplay = _verticalSpeed;

        var solveCtx = _actionMotorSessionActive ? ComputeActionMotorSolveContext() : context;

        // PhysicsPassThrough（滞空 / 不接 HardSnap）：垂直分量只用马达 vy，丢弃 Motion 曲线里的 world Y。
        // 否则 sampled Y 与重力积分反向 → solver dY 与 expY 符号相反、eatenRatio≈1、与其它帧交替 → 抖动。
        // 重力挂起（Suspended）仍允许曲线驱动 Y。
        if (!solveCtx.AllowsHardGroundSnap && !_gravitySuspended)
        {
            velocity = new Vector3(velocity.x, _verticalSpeed, velocity.z);
            gameplayDrivenVy = false;
        }

        if (motorSettings != null)
        {
            velocity = MaybeProjectVelocityOntoWalkableSlope(velocity, in solveCtx);
            var displacement = velocity * Time.deltaTime;
            var applySlideYLock = ShouldApplyGroundedSlideDownwardLock(in solveCtx);
            solvedDelta = KinematicMotorSolver.SolveDisplacementFromPivot(
                transform.position,
                pivotToFootOffset,
                motorSettings,
                displacement,
                motorSettings.ObstacleLayers,
                applySlideYLock);
            if (!debugFreezeMotorDisplacement)
            {
                transform.position += solvedDelta;
            }
        }
        else
        {
            if (!debugFreezeMotorDisplacement)
            {
                transform.position += velocity * Time.deltaTime;
            }
        }

        ResolveMotorOverlapsIfNeeded();
        var vyAfterOverlap = _verticalSpeed;
        RefreshGroundedState(in solveCtx);
        RefreshFrozenActionMotorContextIfSession();
        var vyAfterGrounded = _verticalSpeed;
        ApplyAirborneEdgeSlipIfStuck(in solvedDelta);
        LogVerticalAuthorityIfEnabled(
            gameplayDrivenVy ? "ApplyMotorFromGameplay[Y-driven]" : "ApplyMotorFromGameplay",
            vyEnter, vyAfterGravity, vyAfterClamp, solvedDelta.y, vyAfterOverlap, vyAfterGrounded, in solveCtx,
            gameplayDrivenVy ? gameplayWorldVelocity.y : (float?)null,
            vyAfterGameplay);
    }

    void LogVerticalAuthorityIfEnabled(
        string entry,
        float vyEnter,
        float vyAfterGravity,
        float vyAfterClamp,
        float solvedDeltaY,
        float vyAfterOverlap,
        float vyAfterGrounded,
        in MotorSolveContext context,
        float? gameplayWorldVy = null,
        float vyAfterGameplay = 0f)
    {
        if (!debugVerticalAuthority) return;
        var inAction = _states != null && _states.Current is PlayerActionState;
        if (debugVerticalAuthorityActionOnly && !inAction) return;

        var dt = Mathf.Max(Time.deltaTime, 1e-5f);
        var solvedVy = solvedDeltaY / dt;
        var expectedY = vyAfterClamp * dt;
        var verticalEatenRatio = Mathf.Abs(expectedY) > 1e-5f ? (1f - Mathf.Clamp01(solvedDeltaY / expectedY)) : 0f;
        var vyAfterEdge = _verticalSpeed;

        var sb = new System.Text.StringBuilder(384);
        var ctxLine = _actionMotorSessionActive ? _frozenActionMotorContext : context;
        sb.Append("[VertAuthority] f=").Append(Time.frameCount).Append(' ').Append(entry)
          .Append(" ctx=").Append(ctxLine.GroundingPolicy)
          .Append(" hardSnap=").Append(ctxLine.AllowsHardGroundSnap)
          .Append(" frozen=").Append(_actionMotorSessionActive)
          .Append(" airLock=").Append(_actionAirborneLock)
          .Append(" suspended=").Append(_gravitySuspended).Append('\n')
          .Append("  vy enter=").Append(vyEnter.ToString("F4"))
          .Append(" → gravity=").Append(vyAfterGravity.ToString("F4"))
          .Append(" → clamp=").Append(vyAfterClamp.ToString("F4"));
        if (gameplayWorldVy.HasValue)
        {
            sb.Append(" → gameplay(Y=").Append(gameplayWorldVy.Value.ToString("F4")).Append(")=")
              .Append(vyAfterGameplay.ToString("F4"));
        }
        sb.Append('\n')
          .Append("  solver dY=").Append(solvedDeltaY.ToString("F5"))
          .Append(" expY=").Append(expectedY.ToString("F5"))
          .Append(" eatenRatio=").Append(verticalEatenRatio.ToString("F2"))
          .Append(" solvedVy=").Append(solvedVy.ToString("F4")).Append('\n')
          .Append("  vy → overlap=").Append(vyAfterOverlap.ToString("F4"))
          .Append(" → grounded=").Append(vyAfterGrounded.ToString("F4"))
          .Append(" → edgeSlip=").Append(vyAfterEdge.ToString("F4"))
          .Append(" grounded=").Append(_isGrounded);

        var changedByOverlap = !Mathf.Approximately(vyAfterClamp, vyAfterOverlap)
                               && (!gameplayWorldVy.HasValue || !Mathf.Approximately(vyAfterGameplay, vyAfterOverlap));
        var changedByGrounded = !Mathf.Approximately(vyAfterOverlap, vyAfterGrounded);
        var changedByEdge = !Mathf.Approximately(vyAfterGrounded, vyAfterEdge);
        if (changedByOverlap || changedByGrounded || changedByEdge)
        {
            sb.Append("  ★ vy mutated by:");
            if (changedByOverlap) sb.Append(" Overlap");
            if (changedByGrounded) sb.Append(" Grounded");
            if (changedByEdge) sb.Append(" EdgeSlip");
        }
        Debug.Log(sb.ToString(), this);
    }

    public void TeleportTo(Vector3 worldPosition, bool forceAirborne = false)
    {
        var destination = worldPosition;

        if (motorSettings != null)
        {
            var desiredDisplacement = destination - transform.position;
            var clamped = KinematicMotorSolver.ClampDisplacementByObstacleSweep(
                transform.position,
                pivotToFootOffset,
                motorSettings,
                desiredDisplacement,
                motorSettings.ObstacleLayers);
            destination = transform.position + clamped;
        }

        if (!forceAirborne
            && motorSettings != null
            && motorSettings.TeleportRayMaxDistance > 0.001f)
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
        ResolveMotorOverlapsIfNeeded();
        _planarVelocity = Vector3.zero;
        if (!forceAirborne)
        {
            _verticalSpeed = 0f;
        }

        RefreshGroundedState(forceAirborne ? MotorSolveContext.Airborne : MotorSolveContext.Locomotion);

        if (_player != null)
        {
            _player.PublishEvent(new PlayerTeleportedEvent(_player.GetInstanceID(), _player.name, destination));
        }
    }

    public MotorSolveContext BuildActionMotorSolveContext()
    {
        if (_actionMotorSessionActive)
        {
            return _frozenActionMotorContext;
        }

        return ComputeActionMotorSolveContext();
    }

    /// <summary>按当前瞬时状态推导动作上下文（无会话冻结时由 <see cref="BuildActionMotorSolveContext"/> 调用）。</summary>
    MotorSolveContext ComputeActionMotorSolveContext()
    {
        if (_gravitySuspended)
        {
            return MotorSolveContext.ActionSuspendedPhysics;
        }

        // 空中起手锁：整个动作期内强制 Airborne，禁 HardSnap / 坡面投影 / 强耦合吸附。
        if (_actionAirborneLock)
        {
            return MotorSolveContext.Airborne;
        }

        if (_verticalSpeed < ActionMotorAirborneVyThreshold || !_isGrounded)
        {
            return MotorSolveContext.Airborne;
        }

        return MotorSolveContext.Locomotion;
    }

    // ─── 内部 ──────────────────────────────────────────────────────────────

    public void ApplySimpleGravity()
    {
        if (_gravitySuspended)
        {
            _verticalSpeed = 0f;
            return;
        }

        // 动作空中锁 / 动作态（非重力挂起）：始终累计重力，防"边缘探针把 vy 反复清零 → 永远等不到下落"的死锁。
        if (_actionAirborneLock || (_states != null && _states.Current is PlayerActionState))
        {
            _verticalSpeed -= gravity * Time.deltaTime;
            return;
        }

        if (_isGrounded && _verticalSpeed <= 0f)
        {
            _verticalSpeed = 0f;
        }
        else
        {
            _verticalSpeed -= gravity * Time.deltaTime;
        }
    }

    void ClampTerminalVelocityVertical()
    {
        if (motorSettings == null || _gravitySuspended) return;
        _verticalSpeed = Mathf.Max(_verticalSpeed, -Mathf.Abs(motorSettings.TerminalVelocity));
    }

    bool ShouldApplyGroundedSlideDownwardLock(in MotorSolveContext context)
    {
        if (motorSettings == null || !motorSettings.ClampGroundedSlideRemainingY) return false;
        if (!context.AllowsHardGroundSnap || !_isGrounded) return false;
        return _verticalSpeed <= motorSettings.GroundSnapMaxUpwardVelocity;
    }

    Vector3 MaybeProjectVelocityOntoWalkableSlope(Vector3 worldVelocity, in MotorSolveContext context)
    {
        if (!context.AllowsHardGroundSnap || motorSettings == null || !motorSettings.EnablesSlopeProjection) return worldVelocity;
        if (!_isGrounded || _lastGroundNormal.sqrMagnitude < 0.25f) return worldVelocity;

        var frameDisp = worldVelocity * Time.deltaTime;
        var projectedDisp = KinematicMotorSolver.ProjectDisplacementOntoGroundPlaneIfWalkable(
            frameDisp, _lastGroundNormal, motorSettings);
        return projectedDisp / Mathf.Max(Time.deltaTime, 1e-5f);
    }

    void ResolveMotorOverlapsIfNeeded()
    {
        if (motorSettings == null || motorSettings.ObstacleLayers.value == 0) return;

        if (KinematicMotorSolver.ResolveOverlapsAtPivot(
                transform.position,
                pivotToFootOffset,
                motorSettings,
                motorSettings.ObstacleLayers,
                transform,
                out var resolvedPivot))
        {
            if (!debugFreezeMotorDisplacement)
            {
                transform.position = resolvedPivot;
            }
        }
    }

    void RefreshGroundedState(in MotorSolveContext context)
    {
        // 默认与调用方一致；空中锁在本函数内自动落地后会刷新 _frozenActionMotorContext，再同步到 effective。
        var effectiveContext = context;

        var pivotAtRefreshEntry = transform.position;
        var footPos = transform.position - Vector3.up * pivotToFootOffset;
        float probeRadius;
        Vector3 origin;
        if (motorSettings != null)
        {
            KinematicMotorSolver.GetCapsuleWorld(
                transform.position,
                pivotToFootOffset,
                motorSettings,
                out var bottomSphere,
                out _,
                out var capsuleRadius);
            var skin = Mathf.Max(0.0005f, motorSettings.SkinWidth);
            probeRadius = Mathf.Max(0.01f, capsuleRadius - skin);
            origin = bottomSphere;
        }
        else
        {
            probeRadius = Mathf.Max(groundProbeRadius, 0.01f);
            var startOffset = probeRadius + 0.1f;
            origin = footPos + Vector3.up * startOffset;
        }

        var castReach = motorSettings != null && effectiveContext.AllowsHardGroundSnap
            ? Mathf.Max(groundCheckDistance, motorSettings.GroundSnapDistance)
            : groundCheckDistance;
        var castDistance = Mathf.Max(0.1f, castReach);

        RaycastHit hit = default;
        var hitGround = TryPickNearestGroundSphereCast(origin, probeRadius, castDistance, out hit);

        var primaryHitIsWall = hitGround && IsGroundNormalTooSteep(hit.normal);

        var usedExtraStabilizationProbe = false;
        if ((!hitGround || primaryHitIsWall)
            && motorSettings != null
            && motorSettings.EnableExtraGroundStabilizationProbe
            && effectiveContext.AllowsHardGroundSnap
            && _wasGroundedLastFrame
            && _verticalSpeed <= motorSettings.GroundSnapMaxUpwardVelocity)
        {
            var extDist = castDistance + motorSettings.ExtraGroundProbeExtension;
            if (TryPickNearestGroundSphereCast(origin, probeRadius, extDist, out var extraHit)
                && !IsGroundNormalTooSteep(extraHit.normal))
            {
                hit = extraHit;
                hitGround = true;
                primaryHitIsWall = false;
                usedExtraStabilizationProbe = true;
            }
        }

        var usedCenterRayFallback = false;
        if ((!hitGround || primaryHitIsWall)
            && (motorSettings == null || motorSettings.EnableCenterRayGroundFallback)
            && groundLayers.value != 0)
        {
            var centerOrigin = new Vector3(transform.position.x, origin.y, transform.position.z);
            var centerDist = castDistance + probeRadius;
            if (TryPickNearestGroundRaycast(centerOrigin, centerDist, out var centerHit)
                && !IsGroundNormalTooSteep(centerHit.normal))
            {
                hit = centerHit;
                hitGround = true;
                primaryHitIsWall = false;
                usedCenterRayFallback = true;
            }
        }

        MaybeLogMotorCalibrationAfterProbes(
            in effectiveContext,
            pivotAtRefreshEntry,
            origin,
            probeRadius,
            castDistance,
            hitGround,
            in hit,
            usedExtraStabilizationProbe,
            usedCenterRayFallback);

        if (motorSettings != null && motorSettings.DrawGroundProbeInSceneView)
        {
            var rayLen = castDistance * Mathf.Max(0.1f, motorSettings.GroundProbeRayLengthScale);
            Debug.DrawRay(origin, Vector3.down * rayLen, motorSettings.GroundProbeMainRayColor);
            if (hitGround)
            {
                Debug.DrawLine(origin, hit.point,
                    usedExtraStabilizationProbe
                        ? motorSettings.GroundProbeExtraHitLineColor
                        : motorSettings.GroundProbeHitLineColor);
            }
        }
        else if (motorSettings == null)
        {
            Debug.DrawRay(origin, Vector3.down * castDistance, Color.red);
            if (hitGround)
            {
                Debug.DrawLine(origin, hit.point, Color.green);
            }
        }

        if (debugGroundProbe && Time.unscaledTime >= _nextGroundProbeLogTime)
        {
            _nextGroundProbeLogTime = Time.unscaledTime + 0.15f;
            Debug.Log(
                $"[GroundProbe] hit={hitGround} extraProbe={usedExtraStabilizationProbe} centerRay={usedCenterRayFallback} " +
                $"wasGrounded={_wasGroundedLastFrame} vy={_verticalSpeed:F3} cast={castDistance:F3} " +
                $"snapPolicy={effectiveContext.AllowsHardGroundSnap} pt={(hitGround ? hit.point.ToString() : "-")} " +
                $"n={(hitGround ? hit.normal.ToString("F3") : "-")}",
                this);
        }

        if (hitGround && IsGroundNormalTooSteep(hit.normal))
        {
            _hasSteepGroundHitForEdgeSlip = true;
            _lastSteepGroundHitPoint = hit.point;
            _lastSteepGroundHitNormal = hit.normal;
        }
        else
        {
            _hasSteepGroundHitForEdgeSlip = false;
        }

        var inActionState = _states != null && _states.Current is PlayerActionState;

        // 动作空中锁：整段动作内默认强制脱地，绝不允许 IsGrounded=true / vy 清零。
        // 自动释放：探针 walkable + 距离极小 + 下行 vy → 真实落地，主动解锁并走下方正常接地流程，
        // 防"动作时长长于落地时刻"导致穿地。
        if (_actionAirborneLock)
        {
            var landingBand = motorSettings != null
                ? Mathf.Max(motorSettings.AirborneGroundContactSlop, motorSettings.SkinWidth * 4f)
                : 0.05f;
            var landed = hitGround
                         && !IsGroundNormalTooSteep(hit.normal)
                         && hit.distance <= landingBand
                         && _verticalSpeed <= 0.01f;
            if (!landed)
            {
                _isGrounded = false;
                _lastGroundNormal = hitGround && !IsGroundNormalTooSteep(hit.normal) ? hit.normal : Vector3.up;
                _wasGroundedLastFrame = false;
                _hasSteepGroundHitForEdgeSlip = hitGround && IsGroundNormalTooSteep(hit.normal);
                if (_hasSteepGroundHitForEdgeSlip)
                {
                    _lastSteepGroundHitPoint = hit.point;
                    _lastSteepGroundHitNormal = hit.normal;
                }
                MaybeLogMotorCalibrationEarlyExit("actionAirborneLock", pivotAtRefreshEntry);
                return;
            }

            // 真实落地：解锁，让下方正常逻辑处理 IsGrounded=true / HardSnap；并刷新冻结的 Action context（Airborne → Locomotion）。
            _actionAirborneLock = false;
            RefreshFrozenActionMotorContextIfSession();
            if (_debugInterruptFlow)
            {
                Debug.Log($"[ActionAirborneLock] AUTO-RELEASE on real landing | y={transform.position.y:F3} | hitDist={hit.distance:F3} | vy={_verticalSpeed:F3}", this);
            }
        }

        if (_actionMotorSessionActive)
        {
            effectiveContext = _frozenActionMotorContext;
        }

        // 动作中已开始下落：仅当探针确未近接地面才宣布脱地（修 Bug 3 假滞空闪现）
        if (inActionState && !_gravitySuspended && _verticalSpeed < ActionFallBreakGroundedVy)
        {
            var proximityBand = motorSettings != null
                ? Mathf.Max(motorSettings.AirborneGroundContactSlop, motorSettings.SkinWidth * 4f)
                : 0.05f;
            var probeShowsCloseGround = hitGround
                                        && !IsGroundNormalTooSteep(hit.normal)
                                        && hit.distance <= proximityBand;
            if (!probeShowsCloseGround)
            {
                _isGrounded = false;
                _lastGroundNormal = hitGround ? hit.normal : Vector3.up;
                _wasGroundedLastFrame = false;
                MaybeLogMotorCalibrationEarlyExit("actionProximityBreak", pivotAtRefreshEntry);
                return;
            }
        }

        var airborneSlopBand = motorSettings != null ? motorSettings.AirborneGroundContactSlop : float.MaxValue;
        var groundedByProximity = motorSettings != null && !effectiveContext.AllowsHardGroundSnap
            ? hitGround && hit.distance <= airborneSlopBand
            : hitGround;

        var useHardSnapLegacy = motorSettings == null || effectiveContext.AllowsHardGroundSnap;

        var upwardDeadzone = motorSettings != null ? motorSettings.GroundSnapMaxUpwardVelocity : 0.05f;
        if (!groundedByProximity || _verticalSpeed > upwardDeadzone)
        {
            _isGrounded = false;
            if (!hitGround) _lastGroundNormal = Vector3.up;
            _wasGroundedLastFrame = _isGrounded;
            MaybeLogMotorCalibrationEarlyExit("notGroundedByProximityOrVyDeadzone", pivotAtRefreshEntry);
            return;
        }

        // 动作中禁用 Edge Tolerance（防踏出台沿被陡边角放行 → Hard Snap 拽回）
        var canEdgeTolerate = enableEdgeGroundTolerance && _wasGroundedLastFrame && !inActionState;
        if (motorSettings != null && hitGround && motorSettings.IsSlopeTooSteep(hit.normal))
        {
            var allowEdgeGrounding = canEdgeTolerate && IsSteepHitWithinFootTolerance(hit, probeRadius);
            if (!allowEdgeGrounding)
            {
                _isGrounded = false;
                _lastGroundNormal = Vector3.up;
                _wasGroundedLastFrame = _isGrounded;
                MaybeLogMotorCalibrationEarlyExit("steepSlopeNoEdgeTol", pivotAtRefreshEntry);
                return;
            }
        }

        if (motorSettings == null && hitGround &&
            Vector3.Angle(Vector3.up, hit.normal) > MaxSlopeDegreesLegacy)
        {
            var allowEdgeGrounding = canEdgeTolerate && IsSteepHitWithinFootTolerance(hit, probeRadius);
            if (!allowEdgeGrounding)
            {
                _isGrounded = false;
                _lastGroundNormal = Vector3.up;
                _wasGroundedLastFrame = _isGrounded;
                MaybeLogMotorCalibrationEarlyExit("legacySteepNoEdgeTol", pivotAtRefreshEntry);
                return;
            }
        }

        // 二次校验：命中点高于脚底过多 → 仅探针擦到上方棱角，按斜坡几何放宽阈值。
        const float groundedHitElevationGuardFlat = 0.04f;
        var groundedHitElevationGuard = groundedHitElevationGuardFlat;
        if (hitGround && hit.normal.y > 0.001f)
        {
            var capR = motorSettings != null ? motorSettings.Radius : groundProbeRadius;
            var ny = Mathf.Clamp(hit.normal.y, 0f, 1f);
            groundedHitElevationGuard = Mathf.Max(groundedHitElevationGuardFlat, capR * (1f - ny) + 0.02f);
        }

        var footPosForCheck = transform.position - Vector3.up * pivotToFootOffset;
        if (hitGround && hit.point.y > footPosForCheck.y + groundedHitElevationGuard)
        {
            if (debugMotorCalibration && Time.frameCount < debugMotorCalibrationLogFrames)
            {
                var thresh = footPosForCheck.y + groundedHitElevationGuard;
                Debug.Log(
                    $"[MotorCalib][elevReject 蹭顶/棱角校验失败 · 也常用于「生成点 Pivot 过低」] f={Time.frameCount}\n" +
                    $"  hit.point.y={hit.point.y:F4}  逻辑脚底平面 footPlaneY=pivotY-offset={footPosForCheck.y:F4}  guard={groundedHitElevationGuard:F4}  阈值={thresh:F4}\n" +
                    $"  判定：hitY > 阈值 → 视为探针擦到高于脚底的上表面。\n" +
                    $"  ★ 若地面实为平面、脚底应踩在 hitY 上：Pivot Y 应约为 hitY + pivotToFootOffset（例 hitY=0、offset=0.19 → Pivot Y≈0.19），不要把 Pivot 放在与地面对齐的 Y=0。\n" +
                    $"  当前 pivotY={transform.position.y:F4}",
                    this);
            }

            _isGrounded = false;
            _lastGroundNormal = Vector3.up;
            _wasGroundedLastFrame = _isGrounded;
            MaybeLogMotorCalibrationEarlyExit("hitElevationTooHighVsFoot", pivotAtRefreshEntry);
            return;
        }

        _lastGroundNormal = hitGround ? hit.normal : Vector3.up;
        _isGrounded = true;
        // 垂直速度不由接地探针写入：避免与 ApplySimpleGravity / MotionProfile 路径竞争同一帧多次改 vy。
        // 落地帧 vy 的归零由下一帧 ApplySimpleGravity（Locomotion/Airborne 分支）统一处理。

        if (!useHardSnapLegacy)
        {
            _wasGroundedLastFrame = _isGrounded;
            MaybeLogMotorCalibrationEarlyExit("noHardSnapPolicy", pivotAtRefreshEntry);
            return;
        }

        var targetY = hit.point.y + pivotToFootOffset;
        var snapPosition = new Vector3(transform.position.x, targetY, transform.position.z);
        var allowSnapY = true;

        // Hard Snap 只允许小幅向上拉（抗陷地）；大幅向上视作蹭顶举升 → 拒。
        const float snapUpwardSafetyEpsilon = 0.005f;
        if (targetY > transform.position.y + snapUpwardSafetyEpsilon)
        {
            var pull = targetY - transform.position.y;
            var maxUp = motorSettings != null ? motorSettings.MaxGroundSnapUpwardPull : 0f;
            allowSnapY = maxUp > 1e-5f && pull <= maxUp;
        }

        if (allowSnapY && motorSettings != null && motorSettings.GroundSnapOverlapGuard)
        {
            var overlapMask = motorSettings.GroundSnapOverlapLayers.value != 0
                ? motorSettings.GroundSnapOverlapLayers
                : motorSettings.ObstacleLayers;
            if (KinematicMotorSolver.OverlapCapsuleAtPivotBlocks(
                    snapPosition, pivotToFootOffset, motorSettings, overlapMask, transform, s_groundSnapOverlapBuffer))
            {
                allowSnapY = false;
            }
        }

        // 动作中探针命中"脚下远处的地面"（滚落边缘）：禁止吸附回台沿。
        if (allowSnapY && inActionState && hitGround && hit.distance > ActionEdgeSnapMaxHitDistance)
        {
            allowSnapY = false;
        }

        if (allowSnapY && !debugFreezeMotorDisplacement)
        {
            transform.position = snapPosition;
        }

        if (debugMotorCalibration && Time.frameCount < debugMotorCalibrationLogFrames)
        {
            var dy = transform.position.y - pivotAtRefreshEntry.y;
            Debug.Log(
                $"[MotorCalib][end f={Time.frameCount}] grounded={_isGrounded} allowSnap={allowSnapY} targetY={targetY:F4} " +
                $"pivotY_after={transform.position.y:F4} ΔY_this_refresh={dy:F4} freeze={debugFreezeMotorDisplacement}",
                this);
        }

        _wasGroundedLastFrame = _isGrounded;
    }

    void MaybeLogMotorCalibrationAfterProbes(
        in MotorSolveContext context,
        Vector3 pivotWorld,
        Vector3 origin,
        float probeRadius,
        float castDistance,
        bool hitGround,
        in RaycastHit hit,
        bool usedExtraStabilizationProbe,
        bool usedCenterRayFallback)
    {
        if (!debugMotorCalibration || Time.frameCount >= debugMotorCalibrationLogFrames) return;

        var footY = pivotWorld.y - Mathf.Max(0f, pivotToFootOffset);
        var sb = new System.Text.StringBuilder(1024);
        sb.Append("[MotorCalib] 探针已解析 f=").Append(Time.frameCount)
            .Append(" policy=").Append(context.GroundingPolicy)
            .Append(" allowHardSnap=").Append(context.AllowsHardGroundSnap).Append('\n');
        sb.Append("  pivotWorld ").Append(pivotWorld.ToString("F4")).Append("  pivotToFootOffset=").Append(pivotToFootOffset.ToString("F4"))
            .Append("  → footY(逻辑胶囊最底)=").Append(footY.ToString("F4")).Append('\n');

        if (motorSettings != null)
        {
            KinematicMotorSolver.GetCapsuleWorld(
                pivotWorld, pivotToFootOffset, motorSettings, out var bottomSphere, out _, out var capR);
            var lowestY = bottomSphere.y - capR;
            sb.Append("  MotorSettings R=").Append(capR.ToString("F4")).Append(" H_eff=").Append(motorSettings.EffectiveHeight.ToString("F4"))
                .Append(" Skin=").Append(motorSettings.SkinWidth.ToString("F4")).Append('\n');
            sb.Append("  bottomSphereCenterY=").Append(bottomSphere.y.ToString("F4")).Append("  lowestCaps≈").Append(lowestY.ToString("F4")).Append('\n');
        }

        sb.Append("  probe originY=").Append(origin.y.ToString("F4")).Append(" probeR=").Append(probeRadius.ToString("F4"))
            .Append(" castDist=").Append(castDistance.ToString("F4")).Append(" extra=").Append(usedExtraStabilizationProbe)
            .Append(" centerRay=").Append(usedCenterRayFallback).Append('\n');

        if (hitGround)
        {
            var implied = pivotWorld.y - hit.point.y;
            sb.Append("  HIT dist=").Append(hit.distance.ToString("F4")).Append(" hitY=").Append(hit.point.y.ToString("F4"))
                .Append(" n=").Append(hit.normal.ToString("F3")).Append('\n');
            sb.Append("       collider=").Append(hit.collider != null ? hit.collider.name : "?").Append(" layer=");
            if (hit.collider != null)
            {
                var ln = LayerMask.LayerToName(hit.collider.gameObject.layer);
                sb.Append(string.IsNullOrEmpty(ln) ? hit.collider.gameObject.layer.ToString() : ln);
            }

            sb.Append('\n');
            sb.Append("  ★ impliedPivotMinusHitY=").Append(implied.ToString("F4"))
                .Append("  ← 脚底若贴在该命中面上，PivotToFootOffset 应对齐此量级（再按网格/骨骼微调）\n");
        }
        else
        {
            sb.Append("  HIT=FALSE — 检查 Ground Layers / 场景碰撞 / 起点是否在几何体内\n");
        }

        sb.Append("  debugFreezeMotorDisplacement=").Append(debugFreezeMotorDisplacement)
            .Append(" vy=").Append(_verticalSpeed.ToString("F4")).Append('\n');

        Debug.Log(sb.ToString(), this);
    }

    void MaybeLogMotorCalibrationEarlyExit(string tag, Vector3 pivotAtRefreshEntry)
    {
        if (!debugMotorCalibration || Time.frameCount >= debugMotorCalibrationLogFrames) return;

        Debug.Log(
            $"[MotorCalib][exit:{tag}] f={Time.frameCount} grounded={_isGrounded} pivotY={transform.position.y:F4} " +
            $"ΔVsRefreshStart={(transform.position.y - pivotAtRefreshEntry.y):F4}",
            this);
    }

    bool IsSteepHitWithinFootTolerance(in RaycastHit hit, float probeRadius)
    {
        var footPos = transform.position - Vector3.up * pivotToFootOffset;
        var toHit = hit.point - footPos;
        var horizontalSq = toHit.x * toHit.x + toHit.z * toHit.z;
        var horizontalLimit = Mathf.Max(0.01f, probeRadius * edgeGroundToleranceRadiusScale);
        if (horizontalSq > horizontalLimit * horizontalLimit) return false;
        if (hit.point.y > footPos.y + edgeGroundToleranceVerticalSlop) return false;
        return true;
    }

    void ApplyAirborneEdgeSlipIfStuck(in Vector3 solvedDelta)
    {
        if (!enableAirborneEdgeSlip || motorSettings == null || _isGrounded)
        {
            _edgeSlipStuckFrameCount = 0;
            return;
        }

        // 动作（含 MotionProfile / Burst / Charge）自管理速度，edge slip 横向脉冲会污染。
        if (_states != null && _states.Current is PlayerActionState)
        {
            _edgeSlipStuckFrameCount = 0;
            return;
        }

        if (!_hasSteepGroundHitForEdgeSlip || _verticalSpeed > -edgeSlipMinFallingSpeed)
        {
            _edgeSlipStuckFrameCount = 0;
            return;
        }

        var dt = Mathf.Max(Time.deltaTime, 1e-5f);
        var solvedVerticalSpeed = solvedDelta.y / dt;
        if (solvedVerticalSpeed <= edgeSlipSolvedVerticalSpeedThreshold)
        {
            _edgeSlipStuckFrameCount = 0;
            return;
        }

        var away = new Vector3(_lastSteepGroundHitNormal.x, 0f, _lastSteepGroundHitNormal.z);
        if (away.sqrMagnitude < 1e-6f)
        {
            away = transform.position - _lastSteepGroundHitPoint;
            away.y = 0f;
        }

        if (away.sqrMagnitude < 1e-6f) return;

        away.Normalize();
        _planarVelocity += away * edgeSlipPushSpeed;

        _edgeSlipStuckFrameCount++;
        if (_edgeSlipStuckFrameCount < edgeSlipForceUnstickFrameThreshold) return;

        var horizontal = away * edgeSlipForceUnstickHorizontalStep;
        var down = Vector3.down * edgeSlipForceUnstickDownStep;
        if (!debugFreezeMotorDisplacement)
        {
            transform.position += horizontal + down;
        }

        _verticalSpeed = 0f;

        ResolveMotorOverlapsIfNeeded();
    }

    bool IsGroundNormalTooSteep(in Vector3 normal)
    {
        if (motorSettings != null) return motorSettings.IsSlopeTooSteep(normal);
        return Vector3.Angle(Vector3.up, normal) > MaxSlopeDegreesLegacy;
    }

    /// <summary>
    /// 角色胶囊 / 子物体上的碰撞体（受击盒、武器等）。Ground Layers 设为 Everything 时，
    /// SphereCast 会先命中这些体 → 错误的 hit.point / Hard Snap → 肉眼陷地或抖动。
    /// </summary>
    bool IsOwnHierarchyCollider(Collider c)
    {
        if (c == null) return false;
        var t = c.transform;
        return t == transform || t.IsChildOf(transform);
    }

    bool TryPickNearestGroundSphereCast(Vector3 origin, float probeRadius, float maxDistance, out RaycastHit bestHit)
    {
        bestHit = default;
        var n = Physics.SphereCastNonAlloc(
            origin,
            probeRadius,
            Vector3.down,
            s_groundProbeHits,
            maxDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore);
        var best = float.MaxValue;
        var found = false;
        for (var i = 0; i < n; i++)
        {
            ref var h = ref s_groundProbeHits[i];
            if (IsOwnHierarchyCollider(h.collider)) continue;
            if (h.distance < best)
            {
                best = h.distance;
                bestHit = h;
                found = true;
            }
        }

        return found;
    }

    bool TryPickNearestGroundRaycast(Vector3 origin, float maxDistance, out RaycastHit bestHit)
    {
        bestHit = default;
        var n = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            s_groundProbeHits,
            maxDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore);
        var best = float.MaxValue;
        var found = false;
        for (var i = 0; i < n; i++)
        {
            ref var h = ref s_groundProbeHits[i];
            if (IsOwnHierarchyCollider(h.collider)) continue;
            if (h.distance < best)
            {
                best = h.distance;
                bestHit = h;
                found = true;
            }
        }

        return found;
    }
}

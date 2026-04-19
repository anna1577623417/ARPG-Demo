using UnityEngine;

/// <summary>
/// 玩家控制器：连续移动采样 + 离散意图入队。
/// 跑步（Run）：摇杆高幅度 **或** **同一 WASD 方向双击** 进入「粘性跑步」——有移动输入期间保持 Run，可任意变向，松手后退出。
/// </summary>
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Player))]
[AddComponentMenu("GameMain/Player/Player Controller")]
public class PlayerController : EntityController
{
    private enum MoveTapCardinal : byte
    {
        None = 0,
        Up = 1,
        Down = 2,
        Left = 3,
        Right = 4,
    }

    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private InputReader inputReader;

    [Header("Continuous locomotion — Run")]
    [Tooltip("仅手柄：左摇杆幅度超过该阈值视为 Run。键盘 WASD 模长常为 1，不会走此分支；键盘 Run 只靠双击粘性。")]
    [SerializeField, Range(0f, 1f)] private float runMagnitudeThreshold = 0.85f;

    [Header("Double-tap WASD → sticky run")]
    [Tooltip("同一方向（W/A/S/D 主导）两次「按下边沿」之间的最大间隔（秒），小于此值则进入粘性跑步。")]
    [SerializeField, Range(0.05f, 0.8f)] private float doubleTapCardinalWindow = 0.35f;

    [Tooltip("判定「主导方向」时，合成向量模长低于此值视为无输入（用于松手退出粘性跑步）。")]
    [SerializeField, Range(0.01f, 0.3f)] private float moveReleaseThreshold = 0.12f;

    [Tooltip("判定 W/A/S/D 主导轴时，主分量需比另一轴大多少（避免斜向误判）。")]
    [SerializeField, Range(0f, 0.3f)] private float tapAxisSeparation = 0.06f;

    [Header("Debug")]
    [Tooltip("勾选后输出粘性跑步（双击 WASD）相关调试日志；仅开发用。")]
    [SerializeField] private bool debugRunLogs = true;

    [Tooltip("勾选后在场景视图玩家脚底绘制相机参考水平前向与角色水平前向；颜色见下方两项。")]
    [SerializeField] private bool debugForwardDirectionArrows;

    [Tooltip("camera-relative DEBUG：相机/移动参考在水平面上的「正前」箭头颜色。")]
    [SerializeField] private Color debugCameraRelativeArrowColor = new Color(0.04f, 0.12f, 0.42f);

    [Tooltip("camera-relative DEBUG：角色自身水平朝前箭头颜色。")]
    [SerializeField] private Color debugPlayerForwardArrowColor = new Color(0.35f, 0.92f, 0.35f);

    [Tooltip("箭头轴线长度（世界空间）。")]
    [SerializeField, Min(0.05f)] private float debugArrowLength = 1.25f;

    [Tooltip("箭头起点相对 transform.position 的抬高（模拟贴在脚底上方）。")]
    [SerializeField] private float debugArrowHeightOffset = 0.08f;

    [Header("Primary attack — tap vs hold")]
    [Tooltip("左键（Attack）短按派发轻击，长按达到阈值派发蓄力意图；与 WeaponMoveset 中 LightAttacks / ChargedAttacks 对应。")]
    [SerializeField] private PrimaryAttackSplitPolicy primaryAttackSplit = new PrimaryAttackSplitPolicy
    {
        HoldSecondsBeforeChargedIntent = 0.18f,
    };

    private readonly PrimaryAttackPressTracker _primaryAttackPress = new PrimaryAttackPressTracker();

    private IGameModeMovementContext _movementContext;
    private bool _isInitialized;
    private bool _loggedMissingMovementContext;

    private Vector2 _prevMoveInput;

    /// <summary>索引 = MoveTapCardinal。记录该方向上一次「按下边沿」时间。</summary>
    private readonly float[] _lastCardinalPressTime = new float[5];

    private bool _stickyRunMode;
    private bool _prevWantsRun;

    // ─── 生命周期 ───

    private void Awake()
    {
        Init();
    }

    private void Init()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        if (player == null)
        {
            player = GetComponent<Player>();
        }

        if (inputReader == null && player != null)
        {
            inputReader = player.InputReader;
        }

        _primaryAttackPress.Configure(in primaryAttackSplit);
    }

    /// <summary>
    /// 构造期/生成点注入相机移动上下文：由 <see cref="SystemRoot"/>（场景登记）或 <see cref="PlayerFactory"/>（运行时生成）推送；
    /// 调用方持有接口引用而非查表解析；晚注入时移动采样会在下一帧起生效。
    /// </summary>
    public void InjectMovementContext(IGameModeMovementContext context)
    {
        _movementContext = context;
    }

    private void OnEnable()
    {
        Init();
        if (inputReader != null)
        {
            _primaryAttackPress.SyncInitialHeldState(inputReader.IsAttackHeld);
        }
    }

    private void OnDisable()
    {
        if (inputReader != null)
        {
            _primaryAttackPress.SyncInitialHeldState(inputReader.IsAttackHeld);
        }
    }

    private void Update()
    {
        if (player == null || inputReader == null)
        {
            return;
        }

        ConsumeDiscreteIntents();
        _primaryAttackPress.Tick(Time.time, inputReader.IsAttackHeld, player);

        var rawInput = inputReader.MoveInput;
        var releaseSq = moveReleaseThreshold * moveReleaseThreshold;

        if (rawInput.sqrMagnitude < releaseSq)
        {
            if (_stickyRunMode)
            {
                _stickyRunMode = false;
                LogRunSticky(false, "input released (below threshold)");
            }
        }

        DetectDoubleTapCardinalStickyRun(rawInput, releaseSq);

        var worldDirection = ResolveWorldDirection(rawInput);
        var wantsRun = ResolveRunIntent(rawInput, releaseSq);
        player.SetMovementIntent(worldDirection, wantsRun);

        //if (debugRunLogs && wantsRun != _prevWantsRun)
        //{
        //    Debug.Log(
        //        $"[PlayerController][Locomotion] WantsRun {(wantsRun ? "TRUE → Run" : "FALSE → Walk/Idle")} | " +
        //        $"sticky={_stickyRunMode} | moveMag={rawInput.magnitude:F3} | " +
        //        $"threshold={runMagnitudeThreshold:F2}",
        //        this);
        //}

        _prevWantsRun = wantsRun;
        _prevMoveInput = rawInput;
    }

    /// <summary>
    /// 核心控制流改为直接依赖调用：控制器直接消费 InputReader 的离散脉冲并入队意图。
    /// </summary>
    private void ConsumeDiscreteIntents()
    {
        if (inputReader.ConsumeJumpPressed())
        {
            player.EnqueueGameplayIntent(PlayerIntentCatalog.Jump(Time.time));
        }

        if (inputReader.ConsumeDodgePressed())
        {
            player.EnqueueGameplayIntent(PlayerIntentCatalog.Dodge(Time.time, null));
        }

        if (inputReader.ConsumeSwordDashPressed())
        {
            player.EnqueueGameplayIntent(PlayerIntentCatalog.SwordDash(Time.time, null));
        }
    }

    private static MoveTapCardinal GetDominantTapDir(Vector2 v, float separation)
    {
        var ax = Mathf.Abs(v.x);
        var ay = Mathf.Abs(v.y);
        if (ay > ax + separation)
        {
            return v.y > 0f ? MoveTapCardinal.Up : MoveTapCardinal.Down;
        }

        if (ax > ay + separation)
        {
            return v.x > 0f ? MoveTapCardinal.Right : MoveTapCardinal.Left;
        }

        return MoveTapCardinal.None;
    }

    private void DetectDoubleTapCardinalStickyRun(Vector2 rawInput, float releaseSq)
    {
        if (rawInput.sqrMagnitude < releaseSq)
        {
            return;
        }

        var curr = GetDominantTapDir(rawInput, tapAxisSeparation);
        var prev = GetDominantTapDir(_prevMoveInput, tapAxisSeparation);

        if (curr == MoveTapCardinal.None || curr == prev)
        {
            return;
        }

        // 主导方向从「非当前键」切到「当前键」：视为一次新的方向按下边沿。
        var idx = (int)curr;
        var lastT = _lastCardinalPressTime[idx];
        if (lastT > 0.001f && Time.time - lastT <= doubleTapCardinalWindow)
        {
            if (!_stickyRunMode)
            {
                LogRunSticky(true, $"double-tap {curr} within {doubleTapCardinalWindow:F2}s");
            }

            _stickyRunMode = true;
        }

        _lastCardinalPressTime[idx] = Time.time;
    }

    private void LogRunSticky(bool on, string reason)
    {
        if (!debugRunLogs)
        {
            return;
        }

        //Debug.Log(
        //    on
        //        ? $"[PlayerController][Run] Sticky RUN **ON** ({reason})"
        //        : $"[PlayerController][Run] Sticky RUN **OFF** — {reason}",
        //    this);
    }

    private bool ResolveRunIntent(Vector2 rawInput, float releaseSq)
    {
        if (rawInput.sqrMagnitude < releaseSq)
        {
            return false;
        }

        if (_stickyRunMode)
        {
            return true;
        }

        if (inputReader.MoveActuatedByGamepad && rawInput.magnitude >= runMagnitudeThreshold)
        {
            return true;
        }

        return false;
    }

    private Vector3 ResolveWorldDirection(Vector2 rawInput)
    {
        if (rawInput.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        var input = Vector2.ClampMagnitude(rawInput, 1f);

        if (_movementContext != null && !_movementContext.IsCameraRelativeMovement)
        {
            return new Vector3(input.x, 0f, input.y);
        }

        Quaternion refRotation;

        if (_movementContext != null)
        {
            refRotation = _movementContext.GetMovementReferenceRotation();
        }
        else
        {
#if UNITY_EDITOR
            if (!_loggedMissingMovementContext)
            {
                _loggedMissingMovementContext = true;
                Debug.LogWarning(
                    "[PlayerController] 缺少 IGameModeMovementContext：请在 SystemRoot → Scene Player Controllers 登记本角色，" +
                    "或对 Instantiate 结果使用 PlayerFactory 注入。当前使用 Camera.main Y 角作为移动参考。",
                    this);
            }
#endif
            var mainCam = Camera.main;
            if (mainCam == null)
            {
                return new Vector3(input.x, 0f, input.y);
            }

            refRotation = Quaternion.Euler(0f, mainCam.transform.eulerAngles.y, 0f);
        }

        var forward = refRotation * Vector3.forward;
        var right = refRotation * Vector3.right;
        return forward * input.y + right * input.x;
    }

    /// <summary>
    /// 场景视图调试：与 <see cref="ResolveWorldDirection"/> 相同的参考旋转，绘制水平面上「镜头参考前」与「角色前」。
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!debugForwardDirectionArrows)
        {
            return;
        }

        Init();

        var origin = transform.position + Vector3.up * debugArrowHeightOffset;

        if (!TryGetDebugFlatCameraReferenceForward(out var camFlatFwd))
        {
            return;
        }

        DrawHorizontalDirectionArrow(origin, camFlatFwd, debugArrowLength, debugCameraRelativeArrowColor);

        var playerFlatFwd = transform.forward;
        playerFlatFwd.y = 0f;
        if (playerFlatFwd.sqrMagnitude > 1e-8f)
        {
            DrawHorizontalDirectionArrow(
                origin,
                playerFlatFwd.normalized,
                debugArrowLength,
                debugPlayerForwardArrowColor);
        }
    }

    /// <summary>
    /// 与移动解析一致的相机参考：取 yaw 后在 XZ 平面上的单位前向；非相机相对模式时为世界 +Z。
    /// </summary>
    private bool TryGetDebugFlatCameraReferenceForward(out Vector3 flatForward)
    {
        flatForward = Vector3.forward;

        if (_movementContext != null && !_movementContext.IsCameraRelativeMovement)
        {
            flatForward = Vector3.forward;
            return true;
        }

        Quaternion refRotation;

        if (_movementContext != null)
        {
            refRotation = _movementContext.GetMovementReferenceRotation();
        }
        else
        {
            var mainCam = Camera.main;
            if (mainCam == null)
            {
                flatForward = Vector3.forward;
                return true;
            }

            refRotation = Quaternion.Euler(0f, mainCam.transform.eulerAngles.y, 0f);
        }

        var raw = refRotation * Vector3.forward;
        raw.y = 0f;
        if (raw.sqrMagnitude < 1e-8f)
        {
            flatForward = Vector3.forward;
            return true;
        }

        flatForward = raw.normalized;
        return true;
    }

    private static void DrawHorizontalDirectionArrow(Vector3 origin, Vector3 directionXZUnit, float length, Color color)
    {
        var dir = directionXZUnit;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-8f)
        {
            return;
        }

        dir.Normalize();
        var tip = origin + dir * length;

        Gizmos.color = color;
        Gizmos.DrawLine(origin, tip);

        var headLen = Mathf.Clamp(length * 0.18f, 0.05f, length * 0.45f);
        var wing = Vector3.Cross(Vector3.up, dir).normalized * (headLen * 0.42f);
        var back = -dir * headLen;
        Gizmos.DrawLine(tip, tip + back + wing);
        Gizmos.DrawLine(tip, tip + back - wing);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _primaryAttackPress.Configure(in primaryAttackSplit);
    }
#endif
}

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
    [SerializeField] private bool debugRunLogs = true;

    [Header("Primary attack — tap vs hold")]
    [Tooltip("左键（Attack）短按派发轻击，长按达到阈值派发蓄力意图；与 WeaponMoveset 中 LightAttacks / ChargedAttacks 对应。")]
    [SerializeField] private PrimaryAttackSplitPolicy primaryAttackSplit = new PrimaryAttackSplitPolicy
    {
        HoldSecondsBeforeChargedIntent = 0.18f,
    };

    private readonly PrimaryAttackPressTracker _primaryAttackPress = new PrimaryAttackPressTracker();

    private GameModeManager _gameModeManager;
    private bool _isInitialized;

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

        _gameModeManager = GameModeManager.Instance;
        if (_gameModeManager == null)
        {
            _gameModeManager = FindFirstObjectByType<GameModeManager>();
        }

        _primaryAttackPress.Configure(in primaryAttackSplit);
    }

    private void OnEnable()
    {
        Init();
        if (inputReader != null)
        {
            _primaryAttackPress.SyncInitialHeldState(inputReader.IsAttackHeld);
        }

        GlobalEventBus.Subscribe<JumpInputEvent>(OnJumpInput);
        GlobalEventBus.Subscribe<DodgeInputEvent>(OnDodgeInput);
        GlobalEventBus.Subscribe<SwordDashInputEvent>(OnSwordDashInput);
    }

    private void OnDisable()
    {
        GlobalEventBus.Unsubscribe<JumpInputEvent>(OnJumpInput);
        GlobalEventBus.Unsubscribe<DodgeInputEvent>(OnDodgeInput);
        GlobalEventBus.Unsubscribe<SwordDashInputEvent>(OnSwordDashInput);
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

        if (debugRunLogs && wantsRun != _prevWantsRun)
        {
            Debug.Log(
                $"[PlayerController][Locomotion] WantsRun {(wantsRun ? "TRUE → Run" : "FALSE → Walk/Idle")} | " +
                $"sticky={_stickyRunMode} | moveMag={rawInput.magnitude:F3} | " +
                $"threshold={runMagnitudeThreshold:F2}",
                this);
        }

        _prevWantsRun = wantsRun;
        _prevMoveInput = rawInput;
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

        Debug.Log(
            on
                ? $"[PlayerController][Run] Sticky RUN **ON** ({reason})"
                : $"[PlayerController][Run] Sticky RUN **OFF** — {reason}",
            this);
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

        if (_gameModeManager != null)
        {
            var activeCtrl = _gameModeManager.ActiveCameraController;
            if (activeCtrl != null && !activeCtrl.IsCameraRelativeMovement)
            {
                return new Vector3(input.x, 0f, input.y);
            }
        }

        Quaternion refRotation;

        if (_gameModeManager != null)
        {
            refRotation = _gameModeManager.GetMovementReferenceRotation();
        }
        else
        {
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

    private void OnJumpInput(JumpInputEvent evt)
    {
        if (player == null || !evt.IsPressed)
        {
            return;
        }

        player.EnqueueGameplayIntent(PlayerIntentCatalog.Jump(Time.time));
    }

    private void OnDodgeInput(DodgeInputEvent evt)
    {
        if (player == null)
        {
            return;
        }

        player.EnqueueGameplayIntent(PlayerIntentCatalog.Dodge(Time.time, null));
    }

    private void OnSwordDashInput(SwordDashInputEvent evt)
    {
        if (player == null)
        {
            return;
        }

        player.EnqueueGameplayIntent(PlayerIntentCatalog.SwordDash(Time.time, null));
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _primaryAttackPress.Configure(in primaryAttackSplit);
    }
#endif
}

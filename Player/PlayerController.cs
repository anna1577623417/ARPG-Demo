using UnityEngine;

/// <summary>
/// 玩家控制器：连续移动采样 + 离散意图入队。
/// 跑步（Run）：摇杆高幅度 **或** **同一 WASD 方向双击** 进入「粘性跑步」——有移动输入期间保持 Run，可任意变向，松手后退出。
/// 连续移动在 <see cref="LateUpdate"/> 应用，保证晚于各 <see cref="CameraController"/> 的 <c>LateUpdate</c>，
/// 与当帧已刷新的 <see cref="ICameraDirectionProvider"/> 对齐。
/// </summary>
[DefaultExecutionOrder(20)]
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

    [Tooltip("绘制 ICameraDirectionProvider 的平面 Forward（蓝）与合成移动方向（绿），用于验证帧序与多相机。")]
    [SerializeField] private bool debugDrawCameraRelativeAxes;

    [Tooltip("[MoveDiag:1] 每帧打印 Provider 平面 Forward/Right；勿动鼠标与移动，观察是否漂移。")]
    [SerializeField] private bool debugLogProviderPlanarAxes;

    [Tooltip("[MoveDiag:3] 有移动输入时打印角色根 transform.forward，按住 W 看朝向是否被移动/动画反向带动。")]
    [SerializeField] private bool debugLogPlayerForwardWhenMoving;

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

    /// <summary>Update 采样，LateUpdate 与相机方向对齐后写入移动意图。</summary>
    private Vector2 _pendingLocomotionMoveInput;

    /// <summary>
    /// Runtime party bootstrap may assign <see cref="Player.BindSharedInputReader"/> after Awake —
    /// player.asset wins over a serialized-but-stale controller reference.
    /// </summary>
    private InputReader EffectiveInputReader =>
        player != null && player.InputReader != null ? player.InputReader : inputReader;

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

        if (inputReader == null && player != null && player.InputReader != null)
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
        var reader = EffectiveInputReader;
        if (reader != null)
        {
            _primaryAttackPress.SyncInitialHeldState(reader.IsAttackHeld);
        }
    }

    private void OnDisable()
    {
        var reader = EffectiveInputReader;
        if (reader != null)
        {
            _primaryAttackPress.SyncInitialHeldState(reader.IsAttackHeld);
        }
    }

    private void Update()
    {
        var reader = EffectiveInputReader;
        if (player == null || reader == null)
        {
            return;
        }

        ConsumeDiscreteIntents(reader);
        _primaryAttackPress.Tick(Time.time, reader.IsAttackHeld, player);

        var rawInput = reader.MoveInput;
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

        _pendingLocomotionMoveInput = rawInput;
        _prevMoveInput = rawInput;
    }

    private void LateUpdate()
    {
        var reader = EffectiveInputReader;
        if (player == null || reader == null)
        {
            return;
        }

        var rawInput = _pendingLocomotionMoveInput;
        var releaseSq = moveReleaseThreshold * moveReleaseThreshold;
        var worldDirection = ResolveWorldDirection(rawInput);
        var wantsRun = ResolveRunIntent(rawInput, releaseSq);
        player.SetMovementIntent(worldDirection, wantsRun);

        if (debugLogProviderPlanarAxes)
        {
            if (_gameModeManager == null)
            {
                Debug.Log($"[MoveDiag:1 Provider] GameModeManager=null frame={Time.frameCount}");
            }
            else
            {
                var p = _gameModeManager.ActiveCameraDirectionProvider;
                if (p == null)
                {
                    Debug.Log($"[MoveDiag:1 Provider] ActiveCameraDirectionProvider=null frame={Time.frameCount}");
                }
                else
                {
                    Debug.Log(
                        $"[MoveDiag:1 Provider] Forward={p.Forward} Right={p.Right} | moveRaw={rawInput} worldDir={worldDirection} " +
                        $"LookInput={reader.LookInput} frame={Time.frameCount}");
                }
            }
        }

        if (debugLogPlayerForwardWhenMoving && rawInput.sqrMagnitude > releaseSq)
        {
            var f = player.transform.forward;
            Debug.Log(
                $"[MoveDiag:3 PlayerRoot] transform.forward={f} xz=({f.x:F5}, {f.z:F5}) moveRaw={rawInput} worldDir={worldDirection} frame={Time.frameCount}");
        }

        if (debugDrawCameraRelativeAxes && _gameModeManager != null)
        {
            var p = _gameModeManager.ActiveCameraDirectionProvider;
            var origin = player.transform.position + Vector3.up * 0.1f;
            if (p != null)
            {
                Debug.DrawRay(origin, p.Forward * 3f, Color.blue);
            }

            if (worldDirection.sqrMagnitude > 1e-6f)
            {
                Debug.DrawRay(origin, worldDirection.normalized * 2.5f, Color.green);
            }
        }

        _prevWantsRun = wantsRun;
    }

    /// <summary>
    /// 核心控制流改为直接依赖调用：控制器直接消费 InputReader 的离散脉冲并入队意图。
    /// </summary>
    private void ConsumeDiscreteIntents(InputReader reader)
    {
        if (reader.ConsumeJumpPressed())
        {
            player.EnqueueGameplayIntent(PlayerIntentCatalog.Jump(Time.time));
        }

        if (reader.ConsumeDodgePressed())
        {
            player.EnqueueGameplayIntent(PlayerIntentCatalog.Dodge(Time.time, null));
        }

        if (reader.ConsumeSwordDashPressed())
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

        var reader = EffectiveInputReader;
        if (reader != null && reader.MoveActuatedByGamepad && rawInput.magnitude >= runMagnitudeThreshold)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 仅使用 <see cref="GameModeManager.ActiveCameraDirectionProvider"/> 的平面 Forward/Right；<b>禁止</b>在此读取 <see cref="Camera.main"/>，
    /// 避免多相机 / Brain 与状态机耦合下的隐性方向源。
    /// </summary>
    private Vector3 ResolveWorldDirection(Vector2 rawInput)
    {
        if (rawInput.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        var input = Vector2.ClampMagnitude(rawInput, 1f);

        if (_gameModeManager == null)
        {
            return Vector3.zero;
        }

        var provider = _gameModeManager.ActiveCameraDirectionProvider;
        if (provider == null)
        {
            return Vector3.zero;
        }

        var forward = provider.Forward;
        var right = provider.Right;
        if (forward.sqrMagnitude < 1e-8f || right.sqrMagnitude < 1e-8f)
        {
            return Vector3.zero;
        }

        return forward * input.y + right * input.x;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _primaryAttackPress.Configure(in primaryAttackSplit);
    }
#endif
}

using UnityEngine;

/// <summary>
/// 玩家控制器（"大脑"层）。
///
/// ═══ 职责 ═══
///
/// InputReader = "硬件翻译官"（原始信号：W 键按下、摇杆推到 0.7）
/// PlayerController = "战场指挥官"（语义意图：向相机前方移动、跑步）
/// Player = "士兵肉体"（执行能力：加速、跳跃、攻击）
/// StateMachine = "决策中枢"（状态逻辑：何时用什么能力）
///
/// ═══ 数据流 ═══
///
/// InputReader.MoveInput (Vector2)
///   → PlayerController.ResolveWorldDirection() → 相机相对 or 世界坐标
///   → PlayerController.ResolveRunIntent() → 走/跑意图
///   → Player.SetMovementIntent(worldDir, wantsRun)
///   → StateMachine 消费意图
///
/// ═══ 多模式支持 ═══
///
/// 监听 GameModeChangedEvent，在不同模式下：
/// - Action: 相机相对移动 + WASD + Shift 跑步
/// - FPS: 相机相对移动 + WASD + 角色朝向跟随相机
/// - MOBA: 世界坐标移动 + 右键点击移动（预留）
/// </summary>
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Player))]
[AddComponentMenu("GameMain/Player/Player Controller")]
public class PlayerController : EntityController
{
    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private InputReader inputReader;

    [Header("Run Tuning")]
    [SerializeField] private bool holdShiftToRun = true;
    [SerializeField, Range(0f, 1f)] private float runMagnitudeThreshold = 0.85f;

    [Header("Input Filtering")]
    [Tooltip("移动启动阈值（秒）。\n玩家必须持续按住方向键超过此时间才确认移动意图。\n过滤误触和抖动式点按。\n0 = 无过滤（立即响应）。")]
    [SerializeField, Range(0f, 0.3f)] private float moveStartupThreshold = 0.08f;

    [Tooltip("移动松手宽容时间（秒）。\n松手后在此窗口内重新按下，不会中断移动。\n防止快速换方向时角色卡顿。")]
    [SerializeField, Range(0f, 0.2f)] private float moveReleaseTolerance = 0.06f;

    private GameModeManager _gameModeManager;
    private bool _isInitialized;
    private float _moveHoldTime;
    private float _moveReleaseTime;
    private bool _moveConfirmed;

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
    }

    private void OnEnable()
    {
        Init();
        GlobalEventBus.Subscribe<AttackInputEvent>(OnAttackInput);
    }

    private void OnDisable()
    {
        GlobalEventBus.Unsubscribe<AttackInputEvent>(OnAttackInput);
    }

    // ─── 每帧驱动 ───

    private void Update()
    {
        if (player == null || inputReader == null)
        {
            return;
        }

        var rawInput = inputReader.MoveInput;
        var hasRawInput = rawInput.sqrMagnitude > 0.0001f;

        // ─── 输入启动过滤（防抖动/误触）───
        // 原始输入必须持续 moveStartupThreshold 才确认移动意图。
        // 松手后有 moveReleaseTolerance 的宽容窗口，防止换方向时卡顿。
        if (hasRawInput)
        {
            _moveHoldTime += Time.deltaTime;
            _moveReleaseTime = 0f;

            if (_moveHoldTime >= moveStartupThreshold)
                _moveConfirmed = true;
        }
        else
        {
            _moveHoldTime = 0f;
            _moveReleaseTime += Time.deltaTime;

            if (_moveReleaseTime > moveReleaseTolerance)
                _moveConfirmed = false;
        }

        // 只有确认了移动意图才输出方向，否则输出零向量
        Vector3 worldDirection;
        bool wantsRun;

        if (_moveConfirmed && hasRawInput)
        {
            worldDirection = ResolveWorldDirection(rawInput);
            wantsRun = ResolveRunIntent(rawInput);
        }
        else
        {
            worldDirection = Vector3.zero;
            wantsRun = false;
        }

        player.SetMovementIntent(worldDirection, wantsRun);
    }

    // ─── 移动方向解算 ───

    /// <summary>
    /// 将原始 2D 输入转换为世界空间 3D 方向。
    /// Action/FPS 模式：相机相对（W = 相机前方）
    /// MOBA 模式：世界坐标（W = 世界北方）
    /// </summary>
    private Vector3 ResolveWorldDirection(Vector2 rawInput)
    {
        if (rawInput.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        var input = Vector2.ClampMagnitude(rawInput, 1f);

        // ─── 判断是否为世界坐标模式（MOBA）───
        if (_gameModeManager != null)
        {
            var activeCtrl = _gameModeManager.ActiveCameraController;
            if (activeCtrl != null && !activeCtrl.IsCameraRelativeMovement)
            {
                return new Vector3(input.x, 0f, input.y);
            }
        }

        // ─── 相机相对移动（默认路径）───
        //
        // 使用 MovementReferenceRotation（纯鼠标 _yaw 构造的水平旋转）
        // 而非 Camera.main.transform。
        //
        // 为什么不读 Camera.main？
        //   followTarget 是 Player 子物体。当 Player.LookAtDirection 转身时，
        //   followTarget 的世界旋转被拖动 → Cinemachine 写入 Camera.main 的朝向被污染
        //   → 下一帧 PlayerController 读到错误方向 → 角色继续转 → 反馈死循环 → 原地转圈。
        //
        //   MovementReferenceRotation 直接从 _yaw 构造（Quaternion.Euler(0, _yaw, 0)），
        //   _yaw 只受鼠标增量驱动，完全不受角色旋转或 Cinemachine 帧序影响。
        //
        Quaternion refRotation;

        if (_gameModeManager != null)
        {
            refRotation = _gameModeManager.GetMovementReferenceRotation();
        }
        else
        {
            // 无 GameModeManager 时回退到 Camera.main（兼容无完整系统的测试场景）
            var mainCam = Camera.main;
            if (mainCam == null) return new Vector3(input.x, 0f, input.y);
            refRotation = Quaternion.Euler(0f, mainCam.transform.eulerAngles.y, 0f);
        }

        // 从参考旋转提取水平 forward 和 right
        var forward = refRotation * Vector3.forward;
        var right = refRotation * Vector3.right;

        // W(+y) = 相机前方, S(-y) = 相机后方, A(-x) = 相机左方, D(+x) = 相机右方
        return forward * input.y + right * input.x;
    }

    // ─── 跑步意图解算 ───

    /// <summary>
    /// 判断是否为跑步意图。
    /// 两种触发方式（可配置）：
    /// 1. 摇杆幅度超过阈值（适合手柄）
    /// 2. Shift 按住（适合键盘）
    /// 所有硬件状态通过 InputReader 读取，不直接访问 Keyboard.current。
    /// </summary>
    private bool ResolveRunIntent(Vector2 rawInput)
    {
        if (rawInput.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        var byMagnitude = rawInput.magnitude >= runMagnitudeThreshold;
        if (!holdShiftToRun)
        {
            return byMagnitude;
        }

        return inputReader.IsSprintHeld || byMagnitude;
    }

    // ─── 攻击意图 ───

    private void OnAttackInput(AttackInputEvent evt)
    {
        if (player == null || !evt.IsPressed)
        {
            return;
        }

        player.SetAttackIntent(true);
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 游戏模式管理器（原 CameraManager）。
///
/// ═══ 核心职责 ═══
///
/// 统一调度游戏模式切换，切换时同步三件事：
///   1. 相机 — 激活对应的 CameraController（Priority 切换）
///   2. 控制器 — 由 PlayerController 监听 GameModeChangedEvent 自行切换行为
///   3. 输入 — 由各模块监听事件自行适配
///
/// ═══ 不做什么 ═══
///
/// - 不写任何相机行为逻辑（那是 CameraController 的职责）
/// - 不写角色移动逻辑（那是 PlayerController 的职责）
/// - 不写 if(mode == MOBA) 之类的模式分支
///
/// ═══ 搭建指南 ═══
///
/// 1. 场景中创建 GameModeManager 节点
/// 2. 为每种模式创建子节点，挂对应的 CameraController + VCam
///    ├── ActionCamera  (ActionCameraController + CinemachineFreeLook)
///    ├── FPSCamera     (FPSCameraController + CinemachineVirtualCamera)
///    └── MOBACamera    (MOBACameraController + CinemachineVirtualCamera)
/// 3. 将三个 CameraController 拖到 cameraControllers 列表
/// 4. 启动时自动激活 defaultMode 对应的控制器
/// </summary>
[AddComponentMenu("GameMain/Game/Game Mode Manager")]
public class GameModeManager : MonoSingleton<GameModeManager>, IGameModule, IGameModeMovementContext
{
    [Header("Camera Controllers")]
    [Tooltip("注册所有模式的相机控制器，顺序不限")]
    [SerializeField] private List<CameraController> cameraControllers = new List<CameraController>();

    [Header("Settings")]
    [SerializeField] private GameModeType defaultMode = GameModeType.Action;

    private GameModeType _currentMode;
    private readonly Dictionary<GameModeType, CameraController> _controllerMap
        = new Dictionary<GameModeType, CameraController>();
    private bool _isInitialized;

    // ─── 公开属性 ───

    /// <summary>当前游戏模式。</summary>
    public GameModeType CurrentMode => _currentMode;

    /// <summary>当前激活的相机控制器。</summary>
    public CameraController ActiveCameraController =>
        _controllerMap.TryGetValue(_currentMode, out var ctrl) ? ctrl : null;

    /// <summary>当前模式是否使用相机相对移动。</summary>
    public bool IsCameraRelativeMovement
    {
        get
        {
            var ctrl = ActiveCameraController;
            return ctrl != null && ctrl.IsCameraRelativeMovement;
        }
    }

    /// <summary>获取当前激活相机的 Transform。</summary>
    public Transform GetActiveCameraTransform()
    {
        var ctrl = ActiveCameraController;
        return ctrl != null ? ctrl.CameraTransform : null;
    }

    /// <summary>
    /// 获取移动参考旋转（仅水平偏航角）。
    /// 由 CameraController 子类从鼠标驱动的 _yaw 直接构造，
    /// 不经过 Cinemachine → Camera.main 链路，无帧序问题。
    /// </summary>
    public Quaternion GetMovementReferenceRotation()
    {
        var ctrl = ActiveCameraController;
        return ctrl != null ? ctrl.MovementReferenceRotation : Quaternion.identity;
    }

    public bool IsInitialized => _isInitialized;

    // ─── 生命周期 ───

    protected override void Awake()
    {
        base.Awake();
        if (!IsPrimaryInstance)
        {
            return;
        }
        Init();
    }

    private void OnEnable()
    {
        Init();
        GlobalEventBus.Subscribe<SwitchGameModeInputEvent>(OnSwitchGameModeInput);
    }

    private void OnDisable()
    {
        GlobalEventBus.Unsubscribe<SwitchGameModeInputEvent>(OnSwitchGameModeInput);
    }

    public void Init()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        // 构建 mode → controller 映射
        _controllerMap.Clear();
        for (int i = 0; i < cameraControllers.Count; i++)
        {
            var ctrl = cameraControllers[i];
            if (ctrl == null) continue;
            _controllerMap[ctrl.Mode] = ctrl;
        }

        // 先禁用所有，再激活默认模式
        DisableAllControllers();
        SwitchToMode(defaultMode);
    }

    // ─── 模式切换 ───

    /// <summary>切换到指定游戏模式。</summary>
    public void SwitchToMode(GameModeType mode)
    {
        var previousMode = _currentMode;

        // 禁用当前控制器
        if (_controllerMap.TryGetValue(_currentMode, out var currentCtrl))
        {
            currentCtrl.enabled = false;
        }

        _currentMode = mode;

        // 启用新控制器
        if (_controllerMap.TryGetValue(_currentMode, out var newCtrl))
        {
            newCtrl.enabled = true;
        }

        if (previousMode != _currentMode)
        {
            GlobalEventBus.Publish(new GameModeChangedEvent(previousMode, _currentMode));
        }
    }

    /// <summary>循环切换到下一个模式（Action → FPS → MOBA → Action）。</summary>
    public void SwitchToNextMode()
    {
        var next = (GameModeType)(((int)_currentMode + 1) % 3);
        SwitchToMode(next);
    }

    // ─── 内部 ───

    private void DisableAllControllers()
    {
        foreach (var kvp in _controllerMap)
        {
            if (kvp.Value != null)
            {
                kvp.Value.enabled = false;
            }
        }
    }

    private void OnSwitchGameModeInput(SwitchGameModeInputEvent evt)
    {
        SwitchToNextMode();
    }
}

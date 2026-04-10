using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 输入读取器（ScriptableObject 单例语义）。
///
/// ═══ 它是怎么工作的？ ═══
///
/// 完整调用链：
///   玩家按下物理按键（如键盘 Space）
///     → Unity Input System 匹配到 .inputactions 里定义的 "Jump" Action
///     → 自动生成的 PlayerInputSystem.cs 触发 IGamePlayActions.OnJump 回调
///     → InputReader 实现了 IGamePlayActions，所以 OnJump 被调用
///     → OnJump 内部做两件事：
///         ① 缓存数据到属性（供状态机轮询）
///         ② 发布事件到 GlobalEventBus（供状态机监听打断）
///     → 状态机从 EventBus 收到 JumpInputEvent，执行状态切换
///
/// 为什么要实现 IGamePlayActions 和 IUIActions 接口？
///   .inputactions 文件在 Unity 中会自动生成一个 C# 类 PlayerInputSystem，
///   里面为每个 ActionMap 定义了一个接口（GamePlay → IGamePlayActions，UI → IUIActions），
///   每个 Action 对应接口里的一个方法。
///   调用 _inputActions.GamePlay.SetCallbacks(this) 后，Input System 会把
///   所有 GamePlay ActionMap 下的回调绑定到 this（即 InputReader）。
///   所以 InputReader 必须实现接口里的每一个方法，否则编译报错。
///
/// 为什么用 ScriptableObject 而不是 MonoBehaviour？
///   - SO 是资产级对象，不依赖场景中的 GameObject 存活。
///   - 多个系统（Player、AI接管、回放）可引用同一个 InputReader 资产。
///   - 切换场景时 SO 不会被销毁，输入状态天然持久。
///
/// 换绑流程预留：
///   外部 RebindManager 通过 InputReader.ActionAsset 访问底层 InputActionAsset，
///   调用 PerformInteractiveRebinding 执行改键，
///   结果通过 SaveBindingOverridesAsJson / LoadBindingOverridesFromJson 持久化到 PlayerPrefs。
/// </summary>
[CreateAssetMenu(fileName = "InputReader", menuName = "GameMain/Input/Input Reader")]
public class InputReader : ScriptableObject, PlayerInputSystem.IGamePlayActions, PlayerInputSystem.IUIActions
{
    private PlayerInputSystem _inputActions;
    private InputFocusMode _currentFocus = InputFocusMode.Gameplay;
    [SerializeField, Range(0f, 1f)] private float moveDeadZone = 0.12f;

    // ═══ 缓存状态（供状态机 Update 轮询读取） ═══

    /// <summary>移动输入方向（归一化 Vector2，x=左右，y=前后）。</summary>
    public Vector2 MoveInput { get; private set; }

    /// <summary>视角/鼠标增量输入。</summary>
    public Vector2 LookInput { get; private set; }

    /// <summary>攻击键是否持续按下（用于蓄力判定等）。</summary>
    public bool IsAttackHeld { get; private set; }

    /// <summary>跳跃键是否持续按下。</summary>
    public bool IsJumpHeld { get; private set; }

    /// <summary>冲刺键是否持续按下（Shift），供 PlayerController 判断跑步意图。</summary>
    public bool IsSprintHeld { get; private set; }

    /// <summary>当前输入焦点模式。</summary>
    public InputFocusMode CurrentFocus => _currentFocus;

    /// <summary>暴露底层 InputActionAsset，供 RebindManager 执行换绑。</summary>
    public InputActionAsset ActionAsset => _inputActions?.asset;

    // ═══ 手动绑定的额外按键（不在 .inputactions 中定义，运行时创建） ═══

    private InputAction _switchCameraAction;
    private InputAction _sprintAction;

    // ═══ 生命周期 ═══

    private void OnEnable()
    {
        if (_inputActions == null)
        {
            _inputActions = new PlayerInputSystem();
            // 把 Player ActionMap 下所有 Action 的回调绑定到 this
            // 之后每次按键触发，Input System 就会调用下面对应的 On_ 方法
            _inputActions.GamePlay.SetCallbacks(this);
            _inputActions.UI.SetCallbacks(this);
        }

        // 手动创建切换相机按键（V 键），不依赖 .inputactions 定义
        if (_switchCameraAction == null)
        {
            _switchCameraAction = new InputAction("SwitchCamera", InputActionType.Button, "<Keyboard>/v");
            _switchCameraAction.performed += OnSwitchCamera;
        }

        // 手动创建冲刺按键（Left Shift），供 PlayerController 判断跑步意图
        if (_sprintAction == null)
        {
            _sprintAction = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            _sprintAction.performed += ctx => IsSprintHeld = true;
            _sprintAction.canceled += ctx => IsSprintHeld = false;
        }

        SetFocus(InputFocusMode.Gameplay);
    }

    private void OnDisable()
    {
        _inputActions?.GamePlay.Disable();
        _inputActions?.UI.Disable();
        _switchCameraAction?.Disable();
        _sprintAction?.Disable();
    }

    // ═══ 焦点切换（Gameplay / UI 互斥） ═══

    /// <summary>
    /// 切换输入焦点。同时只能有一个 ActionMap 激活。
    /// Gameplay 激活时 UI 禁用，反之亦然。
    /// </summary>
    public void SetFocus(InputFocusMode mode)
    {
        _currentFocus = mode;

        switch (mode)
        {
            case InputFocusMode.Gameplay:
                _inputActions.GamePlay.Enable();
                _inputActions.UI.Disable();
                _switchCameraAction?.Enable();
                _sprintAction?.Enable();
                break;
            case InputFocusMode.UI:
                _inputActions.GamePlay.Disable();
                _inputActions.UI.Enable();
                _switchCameraAction?.Disable();
                _sprintAction?.Disable();
                ClearGameplayCache(); // 切到 UI 时清空 Gameplay 缓存，防止残留输入
                break;
        }

        GlobalEventBus.Publish(new InputFocusChangedEvent(mode));
    }

    // ═══ 输入禁用（眩晕、过场动画等） ═══

    public void DisableAllInput()
    {
        _inputActions?.GamePlay.Disable();
        _inputActions?.UI.Disable();
        ClearGameplayCache();
    }

    public void EnableInput()
    {
        SetFocus(_currentFocus);
    }

    private void ClearGameplayCache()
    {
        MoveInput = Vector2.zero;
        LookInput = Vector2.zero;
        IsAttackHeld = false;
        IsJumpHeld = false;
        IsSprintHeld = false;
    }

    // ═══════════════════════════════════════════════════════════════
    //  IGamePlayActions 接口实现
    //  每个方法对应 .inputactions 中 Player ActionMap 下的一个 Action。
    //  Unity Input System 会在三个时机调用：
    //    context.started   → 按键刚按下的那一帧
    //    context.performed → 按键满足交互条件（默认等于 started）
    //    context.canceled  → 按键松开
    //  我们在 performed 时发布"按下"事件，canceled 时发布"松开"事件。
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 移动（连续型）。
    /// WASD / 左摇杆，每帧都可能变化。
    /// 状态机通过轮询 MoveInput 属性读取，同时广播事件供其他系统响应。
    /// </summary>
    public void OnMove(InputAction.CallbackContext context)
    {
        var rawInput = context.ReadValue<Vector2>();
        MoveInput = rawInput.sqrMagnitude < moveDeadZone * moveDeadZone ? Vector2.zero : rawInput;
        GlobalEventBus.Publish(new MoveInputEvent(MoveInput));
    }

    /// <summary>
    /// 视角（连续型）。
    /// 鼠标增量 / 右摇杆，用于相机控制。
    /// </summary>
    public void OnLook(InputAction.CallbackContext context)
    {
        LookInput = context.ReadValue<Vector2>();
        GlobalEventBus.Publish(new LookInputEvent(LookInput));
    }

    /// <summary>
    /// 跳跃（离散型）。
    /// performed = 按下 → 发布 IsPressed=true，状态机收到后立即切换到 JumpState。
    /// canceled  = 松开 → 发布 IsPressed=false，用于可变高度跳跃（松开时截断上升力）。
    /// </summary>
    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            IsJumpHeld = true;
            GlobalEventBus.Publish(new JumpInputEvent(true));
        }
        else if (context.canceled)
        {
            IsJumpHeld = false;
            GlobalEventBus.Publish(new JumpInputEvent(false));
        }
    }

    /// <summary>
    /// 攻击（离散型 + 持续型）。
    /// performed = 按下 → 广播事件，状态机切换到 AttackState。
    /// canceled  = 松开 → 广播松开事件，IsAttackHeld 用于蓄力攻击等持续判定。
    /// </summary>
    public void OnAttack(InputAction.CallbackContext context)
    {
        Debug.Log("按下了 Attack 键");
        if (context.performed)
        {
            IsAttackHeld = true;
            GlobalEventBus.Publish(new AttackInputEvent(true));
        }
        else if (context.canceled)
        {
            IsAttackHeld = false;
            GlobalEventBus.Publish(new AttackInputEvent(false));
        }
    }

    /// <summary>
    /// 闪避（离散型）。
    /// 只关心 performed（按下瞬间），不需要持续状态。
    /// </summary>
    public void OnDodge(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            GlobalEventBus.Publish(new DodgeInputEvent());
        }
    }

    /// <summary>
    /// 交互（离散型）。
    /// 拾取物品、对话、开门等。
    /// </summary>
    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            GlobalEventBus.Publish(new InteractInputEvent());
        }
    }

    /// <summary>
    /// 暂停/菜单（离散型）。
    /// 按下后切换焦点到 UI 模式，由外部暂停管理器监听处理。
    /// </summary>
    public void OnPause(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            GlobalEventBus.Publish(new PauseInputEvent());
        }
    }

    /// <summary>
    /// 切换相机视角（离散型）。
    /// 手动绑定 V 键，不走 .inputactions 文件。
    /// </summary>
    private void OnSwitchCamera(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            GlobalEventBus.Publish(new SwitchGameModeInputEvent());
        }
    }

    // UI ActionMap 回调（当前先保留最小处理，后续可接 UI 事件总线）
    public void OnCancel(InputAction.CallbackContext context)
    {
    }

    public void OnNavigate(InputAction.CallbackContext context)
    {
    }

    public void OnSubmit(InputAction.CallbackContext context)
    {
    }
}

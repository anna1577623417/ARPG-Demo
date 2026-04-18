using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 输入读取器 — 硬件信号 → 连续缓存 + 离散脉冲的翻译层（ScriptableObject 单例语义）。
///
/// ═══ 2.0 数据流 ═══
///
/// 物理按键
///   → Unity Input System (.inputactions 资产中定义的 Action)
///   → 自动生成 PlayerInputSystem.cs 触发 IGamePlayActions 回调
///   → InputReader（本类）将物理信号翻译为两种输出：
///       ① 连续量属性（MoveInput / LookInput 等）— 供 PlayerController / CameraController 每帧轮询
///       ② 离散脉冲（Jump/Dodge/SwordDash Pressed）— 由 PlayerController 直接消费
///   → PlayerController 轮询 MoveInput/IsAttackHeld + 消费离散脉冲 → 入队 IntentBuffer
///   → PlayerStateManager.OnPreLogicUpdate：TransitionResolver 标签仲裁 + 当前状态 TryConsumeGameplayIntent
///
/// ═══ 设计原则 ═══
///
/// 1. 数据来源唯一性：所有键位绑定只在 .inputactions 资产中定义，
///    禁止在代码中 new InputAction() 临时创建，否则 RebindManager 无法统一管理。
/// 2. ScriptableObject 不依赖场景 GameObject，多系统可共享同一资产实例。
/// 3. InputReader 只做"翻译"，不做"决策"。语义意图的合法性由 TransitionResolver + 标签判定。
/// 4. 核心输入到动作管线不经 EventBus：事件只用于 UI/模式切换等旁路广播。
///
/// ═══ 换绑 ═══
///
/// RebindManager 通过 InputReader.ActionAsset 访问底层 InputActionAsset，
/// 调用 PerformInteractiveRebinding 执行改键，结果持久化到 PlayerPrefs。
/// </summary>
[CreateAssetMenu(fileName = "InputReader", menuName = "GameMain/Input/Input Reader")]
public class InputReader : ScriptableObject, PlayerInputSystem.IGamePlayActions, PlayerInputSystem.IUIActions
{
    private PlayerInputSystem _inputActions;
    private InputFocusMode _currentFocus = InputFocusMode.Gameplay;
    [SerializeField, Range(0f, 1f)] private float moveDeadZone = 0.12f;

    [Tooltip("Look（鼠标 delta / 摇杆）死区：低于此模长则视为 0，避免无输入时轴缓慢漂移。")]
    [SerializeField, Range(0f, 0.05f)] private float lookInputDeadZone = 0.002f;

    // ═══ 缓存状态（供状态机 Update 轮询读取） ═══

    /// <summary>移动输入方向（归一化 Vector2，x=左右，y=前后）。</summary>
    public Vector2 MoveInput { get; private set; }

    /// <summary>上一帧驱动 Move 的设备是否为手柄；键盘 WASD 全为 1 模长，不能用手感阈值当 Run。</summary>
    public bool MoveActuatedByGamepad { get; private set; }

    /// <summary>上一帧驱动 Look 的设备是否为手柄（右摇杆为连续量，需乘 <c>Time.deltaTime</c>；鼠标 delta 则否）。</summary>
    public bool LookActuatedByGamepad { get; private set; }

    /// <summary>视角/鼠标增量输入。</summary>
    public Vector2 LookInput { get; private set; }

    /// <summary>攻击键是否持续按下（用于蓄力判定等）。</summary>
    public bool IsAttackHeld { get; private set; }

    /// <summary>跳跃键是否持续按下。</summary>
    public bool IsJumpHeld { get; private set; }

    /// <summary>离散脉冲：本帧是否收到 Jump 按下边沿（由控制器消费并清零）。</summary>
    public bool ConsumeJumpPressed()
    {
        if (!_jumpPressedPulse)
        {
            return false;
        }

        _jumpPressedPulse = false;
        return true;
    }

    /// <summary>离散脉冲：本帧是否收到 Dodge 按下边沿（由控制器消费并清零）。</summary>
    public bool ConsumeDodgePressed()
    {
        if (!_dodgePressedPulse)
        {
            return false;
        }

        _dodgePressedPulse = false;
        return true;
    }

    /// <summary>离散脉冲：本帧是否收到 SwordDash 按下边沿（由控制器消费并清零）。</summary>
    public bool ConsumeSwordDashPressed()
    {
        if (!_swordDashPressedPulse)
        {
            return false;
        }

        _swordDashPressedPulse = false;
        return true;
    }

    /// <summary>阵容：上一名（由 <see cref="GameplayInputRouter"/> 消费）。</summary>
    public bool ConsumePartyPreviousPressed()
    {
        if (!_partyPreviousPressedPulse)
        {
            return false;
        }

        _partyPreviousPressedPulse = false;
        return true;
    }

    /// <summary>阵容：下一名。</summary>
    public bool ConsumePartyNextPressed()
    {
        if (!_partyNextPressedPulse)
        {
            return false;
        }

        _partyNextPressedPulse = false;
        return true;
    }

    /// <summary>阵容：直接切到槽位（0…7）。同一帧多次按下取最后一次。</summary>
    public bool ConsumePartySlotPressed(out int slot0Based)
    {
        if (_partySlotPressedPulse < 0)
        {
            slot0Based = -1;
            return false;
        }

        slot0Based = _partySlotPressedPulse;
        _partySlotPressedPulse = -1;
        return true;
    }

    /// <summary>战斗锁定：按下边沿（由 <see cref="GameplayInputRouter"/> 消费并转发事件）。</summary>
    public bool ConsumeLockOnTogglePressed()
    {
        if (!_lockOnTogglePressedPulse)
        {
            return false;
        }

        _lockOnTogglePressedPulse = false;
        return true;
    }

    /// <summary>当前输入焦点模式。</summary>
    public InputFocusMode CurrentFocus => _currentFocus;

    /// <summary>暴露底层 InputActionAsset，供 RebindManager 执行换绑。</summary>
    public InputActionAsset ActionAsset => _inputActions?.asset;

    // ═══ 编辑器操作提示 ═══
    //
    // 以下功能需要在 Unity 编辑器中的 .inputactions 文件中手动添加：
    //
    // 【GamePlay ActionMap】
    //   1. "Sprint"  — 离散「剑冲」；键盘 Shift + 手柄 LB（见 .inputactions）
    //   2. "Jump"    — 键盘 F；手柄 South
    //   3. "Dodge"  — 键盘 Space；手柄 East
    //   4. "SwitchCamera" — <Keyboard>/v
    //   5. 阵容切换 — PartyPrevious / PartyNext / PartySlot1…8（默认 Q / R / 数字行+小键盘；手柄十字键 1–4）
    //   6. LockOnToggle — 锁定（默认 Tab / 手柄 R3）
    //
    // 若在 Input Asset 窗口点「Generate C# Class」，会重写 PlayerInputSystem.cs：
    //   将 IGamePlayActions 新增方法在本类（InputReader）中实现即可。
    //
    // ═══ 重要原则：数据来源唯一性 ═══
    // 所有键位绑定必须且只能在 .inputactions 资产中定义。
    // 禁止在代码中使用 new InputAction(...) 临时创建绑定，
    // 否则会导致键位数据来源分裂，RebindManager 无法统一管理。

    // 离散输入脉冲（核心管线消费用）：不经全局事件总线推进控制流。
    private bool _jumpPressedPulse;
    private bool _dodgePressedPulse;
    private bool _swordDashPressedPulse;
    private bool _partyPreviousPressedPulse;
    private bool _partyNextPressedPulse;
    private int _partySlotPressedPulse = -1;
    private bool _lockOnTogglePressedPulse;

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

        SetFocus(InputFocusMode.Gameplay);
    }

    private void OnDisable()
    {
        _inputActions?.GamePlay.Disable();
        _inputActions?.UI.Disable();
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
                break;
            case InputFocusMode.UI:
                _inputActions.GamePlay.Disable();
                _inputActions.UI.Enable();
                ClearGameplayCache();
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
        MoveActuatedByGamepad = false;
        LookActuatedByGamepad = false;
        LookInput = Vector2.zero;
        IsAttackHeld = false;
        IsJumpHeld = false;
        _jumpPressedPulse = false;
        _dodgePressedPulse = false;
        _swordDashPressedPulse = false;
        _partyPreviousPressedPulse = false;
        _partyNextPressedPulse = false;
        _partySlotPressedPulse = -1;
        _lockOnTogglePressedPulse = false;
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
    /// 状态机通过轮询 MoveInput 属性读取。
    /// </summary>
    public void OnMove(InputAction.CallbackContext context)
    {
        var rawInput = context.ReadValue<Vector2>();
        MoveInput = rawInput.sqrMagnitude < moveDeadZone * moveDeadZone ? Vector2.zero : rawInput;
        MoveActuatedByGamepad = context.control != null && context.control.device is Gamepad;
    }

    /// <summary>
    /// 视角（连续型）。
    /// 鼠标增量 / 右摇杆，用于相机控制。
    /// </summary>
    public void OnLook(InputAction.CallbackContext context)
    {
        var raw = context.ReadValue<Vector2>();
        LookActuatedByGamepad = context.control != null && context.control.device is Gamepad;
        LookInput = CameraLookInputAdapter.ApplyDeadZone(raw, lookInputDeadZone);
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
            _jumpPressedPulse = true;
        }
        else if (context.canceled)
        {
            IsJumpHeld = false;
        }
    }

    /// <summary>
    /// 攻击（离散）。使用 started 避免与「Hold」交互冲突导致鼠标左键无 performed。
    /// </summary>
    public void OnAttack(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            IsAttackHeld = true;
        }
        else if (context.canceled)
        {
            IsAttackHeld = false;
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
            _dodgePressedPulse = true;
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
    /// Sprint 在输入资产中保留名称，语义为离散「剑道冲刺」，不再驱动连续跑步。
    /// </summary>
    public void OnSprint(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _swordDashPressedPulse = true;
        }
    }
    public void OnSwitchCamera(InputAction.CallbackContext context) {
        if (context.performed) GlobalEventBus.Publish(new SwitchGameModeInputEvent());
    }

    public void OnPartyPrevious(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _partyPreviousPressedPulse = true;
        }
    }

    public void OnPartyNext(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _partyNextPressedPulse = true;
        }
    }

    public void OnPartySlot1(InputAction.CallbackContext context) => SetPartySlotPulse(context, 0);
    public void OnPartySlot2(InputAction.CallbackContext context) => SetPartySlotPulse(context, 1);
    public void OnPartySlot3(InputAction.CallbackContext context) => SetPartySlotPulse(context, 2);
    public void OnPartySlot4(InputAction.CallbackContext context) => SetPartySlotPulse(context, 3);
    public void OnPartySlot5(InputAction.CallbackContext context) => SetPartySlotPulse(context, 4);
    public void OnPartySlot6(InputAction.CallbackContext context) => SetPartySlotPulse(context, 5);
    public void OnPartySlot7(InputAction.CallbackContext context) => SetPartySlotPulse(context, 6);
    public void OnPartySlot8(InputAction.CallbackContext context) => SetPartySlotPulse(context, 7);

    public void OnLockOnToggle(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _lockOnTogglePressedPulse = true;
        }
    }

    private void SetPartySlotPulse(InputAction.CallbackContext context, int slot0Based)
    {
        if (context.performed)
        {
            _partySlotPressedPulse = slot0Based;
        }
    }

    //UI ActionMap 回调（当前先保留最小处理，后续可接 UI 事件总线）
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

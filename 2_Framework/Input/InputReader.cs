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

    // ═══ 缓存状态（供状态机 Update 轮询读取） ═══

    /// <summary>移动输入方向（归一化 Vector2，x=左右，y=前后）。</summary>
    public Vector2 MoveInput { get; private set; }

    /// <summary>上一帧驱动 Move 的设备是否为手柄；键盘 WASD 全为 1 模长，不能用手感阈值当 Run。</summary>
    public bool MoveActuatedByGamepad { get; private set; }

    /// <summary>视角/鼠标增量输入。</summary>
    public Vector2 LookInput { get; private set; }

    /// <summary>攻击键是否持续按下（用于蓄力判定等）。</summary>
    public bool IsAttackHeld { get; private set; }

    /// <summary>交互键是否持续按下（副攻 HoldRelease / 松手 Heavy 等）。</summary>
    public bool IsInteractHeld { get; private set; }

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

    public bool ConsumePartyNextPressed()
    {
        if (!_partyNextPulse)
        {
            return false;
        }

        _partyNextPulse = false;
        return true;
    }

    public bool ConsumePartyPrevPressed()
    {
        if (!_partyPrevPulse)
        {
            return false;
        }

        _partyPrevPulse = false;
        return true;
    }

    /// <summary>调试用：PartyNext 脉冲是否尚未被消费。</summary>
    public bool DebugPartyNextPulsePending => _partyNextPulse;

    /// <summary>调试用：PartyPrev 脉冲是否尚未被消费。</summary>
    public bool DebugPartyPrevPulsePending => _partyPrevPulse;

    /// <summary>调试用：待消费的槽位脉冲（0～7），-1 表示无。</summary>
    public int DebugPartySlotPulseIndex => _partySlotPulseIndex;

    /// <summary>
    /// 数字键 1～8 选择的槽位（0～7）；本帧若未触发则返回 false。
    /// </summary>
    public bool ConsumePartySlotSelectPressed(out int slotIndex0Based)
    {
        if (_partySlotPulseIndex < 0)
        {
            slotIndex0Based = -1;
            return false;
        }

        slotIndex0Based = _partySlotPulseIndex;
        _partySlotPulseIndex = -1;
        return true;
    }

    /// <summary>离散脉冲：<see cref="SkillSlotType.Ability1"/>（默认键盘 Q）。</summary>
    public bool ConsumeSlotAbility1Pressed()
    {
        if (!_slotAbility1PressedPulse)
        {
            return false;
        }

        _slotAbility1PressedPulse = false;
        return true;
    }

    /// <summary>离散脉冲：<see cref="SkillSlotType.Ultimate"/>（默认键盘 R）。</summary>
    public bool ConsumeSlotUltimatePressed()
    {
        if (!_slotUltimatePressedPulse)
        {
            return false;
        }

        _slotUltimatePressedPulse = false;
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
    // 【GamePlay ActionMap】（默认键盘 — 见 PlayerInputSystem.inputactions，可用 RebindManager 改绑）
    //   Attack → Primary（轻击）；Interact(+RMB) → Secondary（重击）
    //   SlotAbility1（Ability1 槽）、SlotUltimate（Ultimate 槽）
    //   Dodge → Shift；Sprint →「剑冲」Ability2 槽（键盘 X / 手柄 LB）；Jump → Space（兼 F）
    //   PartyNext / PartyPrev → Z / C；Interact 对话拾取仍可用 E
    //
    // 添加后 Unity 会自动重新生成 PlayerInputSystem.cs，届时：
    //   - IGamePlayActions 接口会新增 OnSprint / OnSwitchCamera 方法
    //   - 在本文件中实现对应回调即可
    //
    // ═══ 重要原则：数据来源唯一性 ═══
    // 所有键位绑定必须且只能在 .inputactions 资产中定义。
    // 禁止在代码中使用 new InputAction(...) 临时创建绑定，
    // 否则会导致键位数据来源分裂，RebindManager 无法统一管理。

    // 离散输入脉冲（核心管线消费用）：不经全局事件总线推进控制流。
    private bool _jumpPressedPulse;
    private bool _dodgePressedPulse;
    private bool _swordDashPressedPulse;

    private bool _partyNextPulse;
    private bool _partyPrevPulse;
    private bool _slotAbility1PressedPulse;
    private bool _slotUltimatePressedPulse;

    // ═══ v4.4：Slot-Based 统一脉冲 + 持续按住状态 ═══════════════════════════════
    // 设计：物理 InputAction 的回调"双写"——既保留旧 pulse（向后兼容），也写入按 SkillSlotType
    // 索引的统一表，使 PlayerController 可按 slot 循环派发，不再硬编码 SwordDash/Dodge。
    //
    // 槽位维度（与 SkillSlotType enum 严格对齐，0..6 共 7 槽）：
    //   [0] Primary    ← LM (Attack action)        蓄力可读 PrimaryHoldDuration
    //   [1] Secondary  ← RM (Interact-as-Secondary 或新增 InputAction)
    //   [2] Ability1   ← Q  (SlotAbility1 action)
    //   [3] Ability2   ← Shift (Sprint action)     ← 历史名"Sprint"，语义为 Ability2 槽
    //   [4] Dodge      ← Dodge action              （可在 .inputactions 中拆出独立键，或并入其它槽）
    //   [5] Ultimate   ← R  (SlotUltimate action)
    //   [6] Jump       ← Space (Jump action)
    private const int SkillSlotCount = 7;
    private readonly bool[] _slotPressedPulses = new bool[SkillSlotCount];
    private readonly bool[] _slotHeld = new bool[SkillSlotCount];
    private readonly float[] _slotHeldStartTime = new float[SkillSlotCount];

    /// <summary>消费指定槽位的"按下"脉冲（与既有 ConsumeXxxPressed 一致语义）。</summary>
    public bool ConsumeSkillSlotPressed(SkillSlotType slot)
    {
        var idx = (int)slot;
        if ((uint)idx >= SkillSlotCount) return false;
        if (!_slotPressedPulses[idx]) return false;
        _slotPressedPulses[idx] = false;
        return true;
    }

    /// <summary>消费"按下"脉冲并附带按住时长（用于蓄力/长按）。Primary 长按场景常用。</summary>
    public bool ConsumeSkillSlotPressed(SkillSlotType slot, out float holdDurationSeconds)
    {
        holdDurationSeconds = 0f;
        var idx = (int)slot;
        if ((uint)idx >= SkillSlotCount) return false;
        if (!_slotPressedPulses[idx]) return false;
        _slotPressedPulses[idx] = false;
        // 注：此处返回的是"截至本帧的累积按住时长"。若按下即松开（短点），返回 ~0。
        // 真正的"松手时拿到完整时长"用法见 PrimaryAttackPressTracker（基于 Held + 时间戳）。
        if (_slotHeldStartTime[idx] > 0f)
        {
            holdDurationSeconds = Mathf.Max(0f, Time.time - _slotHeldStartTime[idx]);
        }
        return true;
    }

    /// <summary>该槽位当前是否被按住（用于轮询）。</summary>
    public bool IsSkillSlotHeld(SkillSlotType slot)
    {
        var idx = (int)slot;
        if ((uint)idx >= SkillSlotCount) return false;
        return _slotHeld[idx];
    }

    /// <summary>该槽位已按住的时长（秒）。未按住返回 0。</summary>
    public float GetSkillSlotHoldDuration(SkillSlotType slot)
    {
        var idx = (int)slot;
        if ((uint)idx >= SkillSlotCount) return 0f;
        if (!_slotHeld[idx] || _slotHeldStartTime[idx] <= 0f) return 0f;
        return Mathf.Max(0f, Time.time - _slotHeldStartTime[idx]);
    }

    /// <summary>底层共用：物理回调收到 started/canceled 时双写到 slot 表。</summary>
    private void WriteSlotEdge(SkillSlotType slot, bool pressed)
    {
        var idx = (int)slot;
        if ((uint)idx >= SkillSlotCount) return;
        if (pressed)
        {
            _slotPressedPulses[idx] = true;
            _slotHeld[idx] = true;
            _slotHeldStartTime[idx] = Time.time;
        }
        else
        {
            _slotHeld[idx] = false;
            _slotHeldStartTime[idx] = 0f;
        }
    }

    /// <summary>-1 表示无；否则为 0～7 的队伍槽索引。</summary>
    private int _partySlotPulseIndex = -1;

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

    // ═══ 焦点切换（Gameplay / UI 互斥或 Mixed 双开） ═══

    /// <summary>
    /// 切换 ActionMap 激活策略。
    /// <see cref="InputFocusMode.Gameplay"/> 与 <see cref="InputFocusMode.UI"/> 互斥；
    /// <see cref="InputFocusMode.Mixed"/> 同时启用两图（见 <see cref="EnableGameplayAndUiMaps"/>）。
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
            case InputFocusMode.Mixed:
                _inputActions.GamePlay.Enable();
                _inputActions.UI.Enable();
                break;
        }

        GlobalEventBus.Publish(new InputFocusChangedEvent(mode));
    }

    /// <summary>
    /// 战斗 HUD 等场景：GamePlay 与 UI 两图同时 Enable。等同 <c>SetFocus(InputFocusMode.Mixed)</c>。
    /// </summary>
    public void EnableGameplayAndUiMaps()
    {
        SetFocus(InputFocusMode.Mixed);
    }

    // ═══ 输入禁用（眩晕、过场动画等） ═══

    public void DisableAllInput()
    {
        _inputActions?.GamePlay.Disable();
        _inputActions?.UI.Disable();
        ClearGameplayCache();
    }

    /// <summary>
    /// 关闭 Gameplay 中除队伍切换（PartyNext/PartyPrev/PartySlot*）以外的动作。
    /// 用于阵亡等需停操作但仍允许切人的场景；与 <see cref="DisableAllInput"/> 不同，不会关掉 Party*。
    /// </summary>
    public void DisableGameplayExceptPartySwitch()
    {
        if (_inputActions == null)
        {
            return;
        }

        InputActionMap map = _inputActions.GamePlay.Get();
        for (var i = 0; i < map.actions.Count; i++)
        {
            var action = map.actions[i];
            if (IsPartySwitchGameplayAction(action.name))
            {
                continue;
            }

            action.Disable();
        }

        ClearGameplayCache();
    }

    public void EnableInput()
    {
        SetFocus(_currentFocus);
        RestoreGameplayControlsWhileFocused();
    }

    /// <summary>
    /// 在 Gameplay 焦点下重新启用 Gameplay 图中全部动作。
    /// 阵亡时的 <see cref="DisableGameplayExceptPartySwitch"/> 会单独 Disable 非 Party 动作，换人成功后须调用本方法（或 <see cref="EnableInput"/>）恢复新上场角色的完整操作。
    /// </summary>
    public void RestoreGameplayControlsWhileFocused()
    {
        if (_inputActions == null)
        {
            return;
        }

        if (_currentFocus != InputFocusMode.Gameplay && _currentFocus != InputFocusMode.Mixed)
        {
            return;
        }

        InputActionMap map = _inputActions.GamePlay.Get();
        if (!map.enabled)
        {
            map.Enable();
        }

        for (var i = 0; i < map.actions.Count; i++)
        {
            map.actions[i].Enable();
        }
    }

    private static bool IsPartySwitchGameplayAction(string actionName)
    {
        if (actionName == "PartyNext" || actionName == "PartyPrev")
        {
            return true;
        }

        return actionName.StartsWith("PartySlot", System.StringComparison.Ordinal);
    }

    private void ClearGameplayCache()
    {
        MoveInput = Vector2.zero;
        MoveActuatedByGamepad = false;
        LookInput = Vector2.zero;
        IsAttackHeld = false;
        IsInteractHeld = false;
        IsJumpHeld = false;
        _jumpPressedPulse = false;
        _dodgePressedPulse = false;
        _swordDashPressedPulse = false;
        _partyNextPulse = false;
        _partyPrevPulse = false;
        _partySlotPulseIndex = -1;
        _slotAbility1PressedPulse = false;
        _slotUltimatePressedPulse = false;
        for (var i = 0; i < SkillSlotCount; i++)
        {
            _slotPressedPulses[i] = false;
            _slotHeld[i] = false;
            _slotHeldStartTime[i] = 0f;
        }
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
        LookInput = context.ReadValue<Vector2>();
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
            WriteSlotEdge(SkillSlotType.Jump, pressed: true);   // ★ v4.4 双写
        }
        else if (context.canceled)
        {
            IsJumpHeld = false;
            WriteSlotEdge(SkillSlotType.Jump, pressed: false);
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
            WriteSlotEdge(SkillSlotType.Primary, pressed: true);   // ★ v4.4 双写：LM = Primary 槽
        }
        else if (context.canceled)
        {
            IsAttackHeld = false;
            WriteSlotEdge(SkillSlotType.Primary, pressed: false);
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
            WriteSlotEdge(SkillSlotType.Dodge, pressed: true);   // ★ v4.4 双写：Dodge 槽（可由 Loadout 重定向）
        }
        else if (context.canceled)
        {
            WriteSlotEdge(SkillSlotType.Dodge, pressed: false);
        }
    }

    /// <summary>
    /// 交互：拾取/对话等脉冲 + 持续按住态（副攻技能管线）。
    /// </summary>
    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            IsInteractHeld = true;
        }
        else if (context.canceled)
        {
            IsInteractHeld = false;
        }

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
    /// Sprint InputAction（默认 Shift）映射到 <see cref="SkillSlotType.Ability2"/> 槽位。
    /// 语义由 Loadout 决定（默认绑剑冲，但可换成任意技能）；InputReader 不再写死"剑冲"。
    /// 旧 ConsumeSwordDashPressed 仍保留为兼容期 API。
    /// </summary>
    public void OnSprint(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _swordDashPressedPulse = true;
            WriteSlotEdge(SkillSlotType.Ability2, pressed: true);   // ★ v4.4 双写：Shift = Ability2 槽
        }
        else if (context.canceled)
        {
            WriteSlotEdge(SkillSlotType.Ability2, pressed: false);
        }
    }
    public void OnSwitchCamera(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            GlobalEventBus.Publish(new SwitchGameModeInputEvent());
        }
    }

    public void OnPartyNext(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _partyNextPulse = true;
        }
    }

    public void OnPartyPrev(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _partyPrevPulse = true;
        }
    }

    public void OnPartySlot1(InputAction.CallbackContext context)
    {
        if (context.performed) QueuePartySlot(0);
    }

    public void OnPartySlot2(InputAction.CallbackContext context)
    {
        if (context.performed) QueuePartySlot(1);
    }

    public void OnPartySlot3(InputAction.CallbackContext context)
    {
        if (context.performed) QueuePartySlot(2);
    }

    public void OnPartySlot4(InputAction.CallbackContext context)
    {
        if (context.performed) QueuePartySlot(3);
    }

    public void OnPartySlot5(InputAction.CallbackContext context)
    {
        if (context.performed) QueuePartySlot(4);
    }

    public void OnPartySlot6(InputAction.CallbackContext context)
    {
        if (context.performed) QueuePartySlot(5);
    }

    public void OnPartySlot7(InputAction.CallbackContext context)
    {
        if (context.performed) QueuePartySlot(6);
    }

    public void OnPartySlot8(InputAction.CallbackContext context)
    {
        if (context.performed) QueuePartySlot(7);
    }

    public void OnSlotAbility1(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _slotAbility1PressedPulse = true;
            WriteSlotEdge(SkillSlotType.Ability1, pressed: true);   // ★ v4.4 双写
        }
        else if (context.canceled)
        {
            WriteSlotEdge(SkillSlotType.Ability1, pressed: false);
        }
    }

    public void OnSlotUltimate(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _slotUltimatePressedPulse = true;
            WriteSlotEdge(SkillSlotType.Ultimate, pressed: true);   // ★ v4.4 双写
        }
        else if (context.canceled)
        {
            WriteSlotEdge(SkillSlotType.Ultimate, pressed: false);
        }
    }

    private void QueuePartySlot(int slotIndex0Based)
    {
        _partySlotPulseIndex = slotIndex0Based;
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

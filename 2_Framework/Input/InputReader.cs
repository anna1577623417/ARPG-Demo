using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 输入读取器（Ver4.3.6+） — 硬件信号 → 连续缓存 + 离散脉冲翻译层。
///
/// ═══ 数据流 ═══
///   物理键 (.inputactions)
///     → PlayerInputSystem.IGamePlayActions 回调
///     → InputReader 写入：① 连续量属性 (MoveInput 等)  ② 17 个 SkillEntrySlot 槽位 SoA 表
///     → PlayerController 轮询消费 → SkillEntryIntentFactory.ForEntry → GameplayIntentBuffer
///     → PlayerStateManager 仲裁 → SkillEntryService → RouteResolver → RouteRuntime
///
/// ═══ 设计原则 ═══
///   1. **零旧符号**：仅认识 SkillEntrySlot；不存在 SkillSlotType。
///   2. **数据来源唯一性**：键位在 .inputactions 资产中定义，禁止代码 new InputAction()。
///   3. **只翻译不决策**：Tap/Hold/Combo/Charge 分流在 PressTracker + RouteResolver 完成。
///   4. **核心管线不走 EventBus**：SkillEntryInputEdgeEvent 仅供旁路（调试/UI）订阅。
/// </summary>
[CreateAssetMenu(fileName = "InputReader", menuName = "GameMain/Input/Input Reader")]
public class InputReader : ScriptableObject, PlayerInputSystem.IGamePlayActions, PlayerInputSystem.IUIActions
{
    [SerializeField, Range(0f, 1f)] float moveDeadZone = 0.12f;

    PlayerInputSystem _inputActions;
    InputFocusMode _currentFocus = InputFocusMode.Gameplay;

    readonly InputModifierBuffer _moveBuffer = new InputModifierBuffer();
    public InputModifierBuffer MoveModifierBuffer => _moveBuffer;

    // ═══ 连续量（PlayerController / CameraController 每帧轮询） ═══
    public Vector2 MoveInput { get; private set; }
    public bool MoveActuatedByGamepad { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool IsAttackHeld { get; private set; }
    public bool IsInteractHeld { get; private set; }
    public bool IsJumpHeld { get; private set; }
    /// <summary>165.1 L7：LeftCtrl/RightCtrl 按住（Hold）或 Toggle 边沿；对应 .inputactions GamePlay/Sprint。</summary>
    public bool IsRunHeld { get; private set; }

    public InputFocusMode CurrentFocus => _currentFocus;
    public InputActionAsset ActionAsset => _inputActions?.asset;

    // ═══ 离散脉冲 ═══
    bool _jumpPressedPulse;
    bool _runToggleEdge;
    bool _partyNextPulse;
    bool _partyPrevPulse;
    int _partySlotPulseIndex = -1;

    // ═══ SkillEntry SoA 表（17 槽） ═══
    const int SlotCount = SkillEntrySlots.TableSize;
    readonly bool[] _slotPressedPulse = new bool[SlotCount];
    readonly bool[] _slotHeld = new bool[SlotCount];
    readonly float[] _slotHeldStartTime = new float[SlotCount];

    // ─── Public API ───

    public bool ConsumeJumpPressed()
    {
        if (!_jumpPressedPulse) return false;
        _jumpPressedPulse = false;
        return true;
    }

    /// <summary>165.1 L7：Toggle 模式下 Sprint 按下边沿。</summary>
    public bool ConsumeRunToggled()
    {
        if (!_runToggleEdge)
        {
            return false;
        }

        _runToggleEdge = false;
        return true;
    }

    public bool ConsumePartyNextPressed()
    {
        if (!_partyNextPulse) return false;
        _partyNextPulse = false;
        return true;
    }

    public bool ConsumePartyPrevPressed()
    {
        if (!_partyPrevPulse) return false;
        _partyPrevPulse = false;
        return true;
    }

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

    public bool DebugPartyNextPulsePending => _partyNextPulse;
    public bool DebugPartyPrevPulsePending => _partyPrevPulse;
    public int DebugPartySlotPulseIndex => _partySlotPulseIndex;

    /// <summary>消费 SkillEntrySlot 按下脉冲（一次性）。</summary>
    public bool ConsumeSkillEntryPressed(SkillEntrySlot slot)
    {
        var idx = (int)slot;
        if ((uint)idx >= SlotCount) return false;
        if (!_slotPressedPulse[idx]) return false;
        _slotPressedPulse[idx] = false;
        return true;
    }

    /// <summary>消费按下脉冲并附带按住时长（截至本帧的累积）。</summary>
    public bool ConsumeSkillEntryPressed(SkillEntrySlot slot, out float holdSeconds)
    {
        holdSeconds = 0f;
        var idx = (int)slot;
        if ((uint)idx >= SlotCount) return false;
        if (!_slotPressedPulse[idx]) return false;
        _slotPressedPulse[idx] = false;
        if (_slotHeldStartTime[idx] > 0f)
        {
            holdSeconds = Mathf.Max(0f, Time.time - _slotHeldStartTime[idx]);
        }
        return true;
    }

    public bool IsSkillEntryHeld(SkillEntrySlot slot)
    {
        var idx = (int)slot;
        if ((uint)idx >= SlotCount) return false;
        return _slotHeld[idx];
    }

    public float GetSkillEntryHoldDuration(SkillEntrySlot slot)
    {
        var idx = (int)slot;
        if ((uint)idx >= SlotCount) return 0f;
        if (!_slotHeld[idx] || _slotHeldStartTime[idx] <= 0f) return 0f;
        return Mathf.Max(0f, Time.time - _slotHeldStartTime[idx]);
    }

    /// <summary>HUD 用：取槽位绑定按键的物理显示字符串。</summary>
    public string GetSkillEntryBindingDisplayString(SkillEntrySlot slot, string fallback = "")
    {
        if (!TryGetSkillEntryAction(slot, out var action) || action == null)
        {
            return fallback ?? string.Empty;
        }

        var idx = FindPreferredBindingIndex(action);
        if (idx < 0) return fallback ?? string.Empty;
        var s = action.GetBindingDisplayString(idx);
        return string.IsNullOrEmpty(s) ? (fallback ?? string.Empty) : s;
    }

    public bool TryGetSkillEntryAction(SkillEntrySlot slot, out InputAction action)
    {
        action = null;
        var asset = ActionAsset;
        if (asset == null) return false;
        var map = asset.FindActionMap("GamePlay", throwIfNotFound: false);
        if (map == null) return false;
        var name = EntryToActionName(slot);
        if (string.IsNullOrEmpty(name)) return false;
        action = map.FindAction(name, throwIfNotFound: false);
        return action != null;
    }

    static string EntryToActionName(SkillEntrySlot slot)
    {
        switch (slot)
        {
            case SkillEntrySlot.LM:    return "Attack_01-03";
            case SkillEntrySlot.Key0:  return "SlotAbility_18";
            case SkillEntrySlot.RM:    return "SkillSlotSecondary_04";
            case SkillEntrySlot.R:     return "SlotUltimate_05";
            case SkillEntrySlot.Q:     return "SlotAbility_06";
            case SkillEntrySlot.Shift: return "SlotAbility_07";
            case SkillEntrySlot.Space: return "SlotAbility_08";
            case SkillEntrySlot.Key1:  return "SlotAbility_09";
            case SkillEntrySlot.Key2:  return "SlotAbility_10";
            case SkillEntrySlot.Key3:  return "SlotAbility_11";
            case SkillEntrySlot.Key4:  return "SlotAbility_12";
            case SkillEntrySlot.Key5:  return "SlotAbility_13";
            case SkillEntrySlot.Key6:  return "SlotAbility_14";
            case SkillEntrySlot.Key7:  return "SlotAbility_15";
            case SkillEntrySlot.Key8:  return "SlotAbility_16";
            case SkillEntrySlot.Key9:  return "SlotAbility_17";
            default:                                 return null;
        }
    }

    static int FindPreferredBindingIndex(InputAction action)
    {
        var fallback = -1;
        for (var i = 0; i < action.bindings.Count; i++)
        {
            var b = action.bindings[i];
            if (b.isComposite || b.isPartOfComposite) continue;
            if (fallback < 0) fallback = i;
            if (string.IsNullOrEmpty(b.groups)
                || b.groups.IndexOf("Keyboard", StringComparison.OrdinalIgnoreCase) >= 0
                || b.groups.IndexOf("Mouse", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return i;
            }
        }
        return fallback;
    }

    void WriteSlotEdge(SkillEntrySlot slot, bool pressed)
    {
        var idx = (int)slot;
        if ((uint)idx >= SlotCount) return;
        var now = Time.time;
        if (pressed)
        {
            _slotPressedPulse[idx] = true;
            _slotHeld[idx] = true;
            _slotHeldStartTime[idx] = now;
            GlobalEventBus.Publish(new SkillEntryInputEdgeEvent(slot, SkillEntryInputEdge.Pressed, now));
        }
        else
        {
            _slotHeld[idx] = false;
            _slotHeldStartTime[idx] = 0f;
            GlobalEventBus.Publish(new SkillEntryInputEdgeEvent(slot, SkillEntryInputEdge.Released, now));
        }
    }

    // ─── Lifecycle / Focus ───

    void OnEnable()
    {
        if (_inputActions == null)
        {
            _inputActions = new PlayerInputSystem();
            _inputActions.GamePlay.SetCallbacks(this);
            _inputActions.UI.SetCallbacks(this);
        }
        SetFocus(InputFocusMode.Gameplay);
    }

    void OnDisable()
    {
        _inputActions?.GamePlay.Disable();
        _inputActions?.UI.Disable();
    }

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

    public void EnableGameplayAndUiMaps() => SetFocus(InputFocusMode.Mixed);

    public void DisableAllInput()
    {
        _inputActions?.GamePlay.Disable();
        _inputActions?.UI.Disable();
        ClearGameplayCache();
    }

    public void DisableGameplayExceptPartySwitch()
    {
        if (_inputActions == null) return;
        var map = _inputActions.GamePlay.Get();
        for (var i = 0; i < map.actions.Count; i++)
        {
            var a = map.actions[i];
            if (IsPartySwitchAction(a.name)) continue;
            a.Disable();
        }
        ClearGameplayCache();
    }

    public void EnableInput()
    {
        SetFocus(_currentFocus);
        RestoreGameplayControlsWhileFocused();
    }

    public void RestoreGameplayControlsWhileFocused()
    {
        if (_inputActions == null) return;
        if (_currentFocus != InputFocusMode.Gameplay && _currentFocus != InputFocusMode.Mixed) return;
        var map = _inputActions.GamePlay.Get();
        if (!map.enabled) map.Enable();
        for (var i = 0; i < map.actions.Count; i++) map.actions[i].Enable();
    }

    static bool IsPartySwitchAction(string n)
    {
        if (n == "PartyNext" || n == "PartyPrev") return true;
        return n.StartsWith("PartySlot", StringComparison.Ordinal);
    }

    void ClearGameplayCache()
    {
        MoveInput = Vector2.zero;
        MoveActuatedByGamepad = false;
        LookInput = Vector2.zero;
        IsAttackHeld = false;
        IsInteractHeld = false;
        IsJumpHeld = false;
        _jumpPressedPulse = false;
        _partyNextPulse = false;
        _partyPrevPulse = false;
        _partySlotPulseIndex = -1;
        for (var i = 0; i < SlotCount; i++)
        {
            _slotPressedPulse[i] = false;
            _slotHeld[i] = false;
            _slotHeldStartTime[i] = 0f;
        }
    }

    // ═══ IGamePlayActions 实现 ═══

    public void OnMove(InputAction.CallbackContext c)
    {
        var raw = c.ReadValue<Vector2>();
        MoveInput = raw.sqrMagnitude < moveDeadZone * moveDeadZone ? Vector2.zero : raw;
        MoveActuatedByGamepad = c.control != null && c.control.device is Gamepad;
        if (MoveInput.sqrMagnitude > 0.0001f)
        {
            _moveBuffer.PushMove(MoveInput, Time.time);
        }
    }

    public void OnLook(InputAction.CallbackContext c) => LookInput = c.ReadValue<Vector2>();

    public void OnJump(InputAction.CallbackContext c)
    {
        if (c.performed) { IsJumpHeld = true; _jumpPressedPulse = true; }
        else if (c.canceled) IsJumpHeld = false;
    }

    public void OnAttack(InputAction.CallbackContext c) => ApplyAttack(c);
    public void OnAttack_0103(InputAction.CallbackContext c) => ApplyAttack(c);

    void ApplyAttack(InputAction.CallbackContext c)
    {
        if (c.started) { IsAttackHeld = true;  WriteSlotEdge(SkillEntrySlot.LM, true); }
        else if (c.canceled) { IsAttackHeld = false; WriteSlotEdge(SkillEntrySlot.LM, false); }
    }

    public void OnInteract(InputAction.CallbackContext c)
    {
        var isRmb = c.control != null && c.control.path == "<Mouse>/rightButton";
        if (c.started) IsInteractHeld = isRmb;
        else if (c.canceled && (isRmb || IsInteractHeld)) IsInteractHeld = false;
        if (c.performed) GlobalEventBus.Publish(new InteractInputEvent());
    }

    public void OnPause(InputAction.CallbackContext c)
    {
        if (c.performed) GlobalEventBus.Publish(new PauseInputEvent());
    }

    public void OnSwitchCamera(InputAction.CallbackContext c)
    {
        if (c.performed) GlobalEventBus.Publish(new SwitchGameModeInputEvent());
    }

    public void OnPartyNext(InputAction.CallbackContext c) { if (c.performed) _partyNextPulse = true; }
    public void OnPartyPrev(InputAction.CallbackContext c) { if (c.performed) _partyPrevPulse = true; }
    public void OnPartySlot1(InputAction.CallbackContext c) { if (c.performed) _partySlotPulseIndex = 0; }
    public void OnPartySlot2(InputAction.CallbackContext c) { if (c.performed) _partySlotPulseIndex = 1; }
    public void OnPartySlot3(InputAction.CallbackContext c) { if (c.performed) _partySlotPulseIndex = 2; }
    public void OnPartySlot4(InputAction.CallbackContext c) { if (c.performed) _partySlotPulseIndex = 3; }
    public void OnPartySlot5(InputAction.CallbackContext c) { if (c.performed) _partySlotPulseIndex = 4; }
    public void OnPartySlot6(InputAction.CallbackContext c) { if (c.performed) _partySlotPulseIndex = 5; }
    public void OnPartySlot7(InputAction.CallbackContext c) { if (c.performed) _partySlotPulseIndex = 6; }
    public void OnPartySlot8(InputAction.CallbackContext c) { if (c.performed) _partySlotPulseIndex = 7; }

    public void OnSkillSlotSecondary_04(InputAction.CallbackContext c)
    {
        if (c.started)  WriteSlotEdge(SkillEntrySlot.RM, true);
        else if (c.canceled) WriteSlotEdge(SkillEntrySlot.RM, false);
    }

    public void OnSlotUltimate_05(InputAction.CallbackContext c)  => SlotEdge(c, SkillEntrySlot.R);
    public void OnSlotAbility_06(InputAction.CallbackContext c)   => SlotEdge(c, SkillEntrySlot.Q);
    public void OnSlotAbility_07(InputAction.CallbackContext c)   => SlotEdge(c, SkillEntrySlot.Shift);
    public void OnSlotAbility_08(InputAction.CallbackContext c)   => SlotEdge(c, SkillEntrySlot.Space);
    public void OnSlotAbility_18(InputAction.CallbackContext c)   => SlotEdge(c, SkillEntrySlot.Key0);
    public void OnSlotAbility_09(InputAction.CallbackContext c)   => SlotEdge(c, SkillEntrySlot.Key1);
    public void OnSlotAbility_10(InputAction.CallbackContext c)   => SlotEdge(c, SkillEntrySlot.Key2);
    public void OnSlotAbility_11(InputAction.CallbackContext c)   => SlotEdge(c, SkillEntrySlot.Key3);
    public void OnSlotAbility_12(InputAction.CallbackContext c)   => SlotEdge(c, SkillEntrySlot.Key4);
    public void OnSlotAbility_13(InputAction.CallbackContext c)   => SlotEdge(c, SkillEntrySlot.Key5);
    public void OnSlotAbility_14(InputAction.CallbackContext c)   => SlotEdge(c, SkillEntrySlot.Key6);
    public void OnSlotAbility_15(InputAction.CallbackContext c)   => SlotEdge(c, SkillEntrySlot.Key7);
    public void OnSlotAbility_16(InputAction.CallbackContext c)   => SlotEdge(c, SkillEntrySlot.Key8);
    public void OnSlotAbility_17(InputAction.CallbackContext c)   => SlotEdge(c, SkillEntrySlot.Key9);

    void SlotEdge(InputAction.CallbackContext c, SkillEntrySlot slot)
    {
        if (c.started)  WriteSlotEdge(slot, true);
        else if (c.canceled) WriteSlotEdge(slot, false);
    }

    // ─── 跑步（165.1 L7：GamePlay/Sprint Action → LeftCtrl/RightCtrl；Shift 仅 SlotAbility_07 技能）───
    public void OnSprint(InputAction.CallbackContext c)
    {
        if (c.started)
        {
            IsRunHeld = true;
            _runToggleEdge = true;
        }
        else if (c.canceled)
        {
            IsRunHeld = false;
        }
    }

    public void OnDodge(InputAction.CallbackContext c)  => SlotEdge(c, SkillEntrySlot.Space);
    public void OnSlotAbility1(InputAction.CallbackContext c) => SlotEdge(c, SkillEntrySlot.Q);
    public void OnSlotUltimate(InputAction.CallbackContext c) => SlotEdge(c, SkillEntrySlot.R);

    // ─── IUIActions（占位） ───
    public void OnCancel(InputAction.CallbackContext context) { }
    public void OnNavigate(InputAction.CallbackContext context) { }
    public void OnSubmit(InputAction.CallbackContext context) { }
}

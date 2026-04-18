#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

using UnityEngine;

/// <summary>
/// Routes party-switch input to <see cref="IPlayerManager"/>.
/// Prefer <see cref="InputReader"/> + <c>PlayerInputSystem.inputactions</c>（可换绑、与 RebindManager 一致）；
/// 若未注入 <see cref="InputReader"/> 或关闭选项，则退回键盘直连（仅编辑器/应急）。
/// </summary>
[DefaultExecutionOrder(-60)]
[AddComponentMenu("GameMain/MultiCharacter/Gameplay Input Router")]
public class GameplayInputRouter : MonoBehaviour
{
    [Tooltip("为真时从共享 InputReader 消费 Party* 离散脉冲（推荐）。")]
    [SerializeField] private bool usePartyActionsFromInputReader = true;

    private IPlayerManager _manager;
    private InputReader _inputReader;
    private IGameplayEventBus _eventBus;

    public void Construct(IPlayerManager manager, InputReader inputReader, IGameplayEventBus eventBus = null)
    {
        _manager = manager;
        _inputReader = inputReader;
        _eventBus = eventBus;
    }

    private void Update()
    {
        if (_manager == null)
        {
            return;
        }

        if (usePartyActionsFromInputReader &&
            _inputReader != null &&
            _inputReader.CurrentFocus == InputFocusMode.Gameplay)
        {
            if (_inputReader.ConsumePartyPreviousPressed())
            {
                _manager.SwitchPreviousOccupiedSlot();
            }

            if (_inputReader.ConsumePartyNextPressed())
            {
                _manager.SwitchNextOccupiedSlot();
            }

            if (_inputReader.ConsumePartySlotPressed(out var slot))
            {
                _manager.TrySwitchToSlot(slot);
            }

            if (_inputReader.ConsumeLockOnTogglePressed())
            {
                _eventBus?.Publish(new LockOnToggleInputEvent());
            }

            return;
        }

        PollKeyboardFallback();
    }

#if ENABLE_INPUT_SYSTEM
    private void PollKeyboardFallback()
    {
        var kb = Keyboard.current;
        if (kb == null || _manager == null)
        {
            return;
        }

        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
        {
            _manager.TrySwitchToSlot(0);
        }
        else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
        {
            _manager.TrySwitchToSlot(1);
        }
        else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
        {
            _manager.TrySwitchToSlot(2);
        }
        else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame)
        {
            _manager.TrySwitchToSlot(3);
        }
        else if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame)
        {
            _manager.TrySwitchToSlot(4);
        }
        else if (kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame)
        {
            _manager.TrySwitchToSlot(5);
        }
        else if (kb.digit7Key.wasPressedThisFrame || kb.numpad7Key.wasPressedThisFrame)
        {
            _manager.TrySwitchToSlot(6);
        }
        else if (kb.digit8Key.wasPressedThisFrame || kb.numpad8Key.wasPressedThisFrame)
        {
            _manager.TrySwitchToSlot(7);
        }

        if (kb.qKey.wasPressedThisFrame)
        {
            _manager.SwitchPreviousOccupiedSlot();
        }

        // 与 InputActions 默认「PartyNext = R」一致；勿用 E（与 Interact 冲突）。
        if (kb.rKey.wasPressedThisFrame)
        {
            _manager.SwitchNextOccupiedSlot();
        }

        if (kb.tabKey.wasPressedThisFrame)
        {
            _eventBus?.Publish(new LockOnToggleInputEvent());
        }
    }
#else
    private void PollKeyboardFallback()
    {
    }
#endif
}

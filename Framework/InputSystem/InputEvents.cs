using UnityEngine;

/// <summary>
/// 输入事件定义。
/// 所有输入事件都是 struct + IGameEvent，由 InputReader 发布到 GlobalEventBus。
/// 状态机和角色逻辑只依赖这些事件结构体，完全不引用 UnityEngine.InputSystem 命名空间。
/// 这就是解耦的核心：逻辑层永远不知道玩家按的是哪个物理按键。
/// </summary>

// ─── 连续型输入（每帧都可能变化，状态机通过轮询 InputReader 缓存读取） ───

public readonly struct MoveInputEvent : IGameEvent
{
    public readonly Vector2 Direction;
    public MoveInputEvent(Vector2 direction) { Direction = direction; }
}

public readonly struct LookInputEvent : IGameEvent
{
    public readonly Vector2 Delta;
    public LookInputEvent(Vector2 delta) { Delta = delta; }
}

// ─── 离散型输入（瞬间触发，通过 EventBus 广播，状态机监听后可打断当前状态） ───

public readonly struct JumpInputEvent : IGameEvent
{
    public readonly bool IsPressed;
    public JumpInputEvent(bool isPressed) { IsPressed = isPressed; }
}

public readonly struct AttackInputEvent : IGameEvent
{
    public readonly bool IsPressed;
    public AttackInputEvent(bool isPressed) { IsPressed = isPressed; }
}

public readonly struct DodgeInputEvent : IGameEvent { }

public readonly struct InteractInputEvent : IGameEvent { }

/// <summary>暂停/菜单键。</summary>
public readonly struct PauseInputEvent : IGameEvent { }

/// <summary>切换相机视角键。</summary>
public readonly struct SwitchGameModeInputEvent : IGameEvent { }

// ─── 输入焦点切换事件（InputReader 内部发布，用于 Gameplay/UI 互斥） ───

public readonly struct InputFocusChangedEvent : IGameEvent
{
    public readonly InputFocusMode Mode;
    public InputFocusChangedEvent(InputFocusMode mode) { Mode = mode; }
}

/// <summary>
/// 输入焦点模式。
/// 同时只能有一个焦点处于激活状态：
/// - Gameplay：角色移动、攻击、跳跃等
/// - UI：菜单导航、背包操作等
/// </summary>
public enum InputFocusMode
{
    Gameplay,
    UI
}

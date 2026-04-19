using UnityEngine;

/// <summary>
/// 输入事件定义。
/// 原则：EventBus 仅用于旁路广播（UI / 相机 / 调试）。
/// 核心输入-动作管线不依赖这些事件推进流程，而是由 PlayerController 直接消费 InputReader 缓存与离散脉冲。
/// </summary>

// ─── 仍通过总线广播的低频/全局输入事件（非核心动作推进） ───

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

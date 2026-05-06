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

// ─── 输入焦点切换事件（InputReader.SetFocus 发布后旁路订阅；含 Mixed 双开） ───

public readonly struct InputFocusChangedEvent : IGameEvent
{
    public readonly InputFocusMode Mode;
    public InputFocusChangedEvent(InputFocusMode mode) { Mode = mode; }
}

/// <summary>
/// Gameplay / UI ActionMap 激活策略（由 <see cref="InputReader.SetFocus"/> 应用）。
/// - Gameplay：仅 GamePlay 图（UI 图关闭）
/// - UI：仅 UI 图（全屏菜单等，GamePlay 关闭并清缓存）
/// - Mixed：两图同时 Enable，战斗时点 HUD / InputSystemUI 模块等（勿与 Gameplay 绑定同一键位）
/// </summary>
public enum InputFocusMode
{
    Gameplay,
    UI,
    Mixed
}

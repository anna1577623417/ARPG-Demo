using System;
using UnityEngine;

/// <summary>
/// 输入事件定义。
/// 原则：EventBus 仅用于旁路广播（UI / 相机 / 调试）。
/// 核心输入-动作管线不依赖这些事件推进流程，而是由 PlayerController 直接消费 InputReader 缓存与离散脉冲。
/// </summary>

/// <summary><see cref="SkillEntryInputEdgeEvent"/> 的边沿类型（与 Input System started/canceled 对齐）。</summary>
public enum SkillEntryInputEdge : byte
{
    Pressed = 0,
    Released = 1,
}

/// <summary>
/// 任意 Skill 物理入口在 Gameplay 焦点下的按下/松开边沿（由 <see cref="InputReader"/> 在
/// <c>WriteSlotEdge</c> 时发布）。<br/>
/// 与 <see cref="InputReader.ConsumeSkillEntryPressed"/> 等轮询 API 同源；订阅方可实现
/// 「OnSkill_NN_Pressed/Released」式旁路逻辑，而核心管线仍建议以消费脉冲为准避免双派发。
/// </summary>
public readonly struct SkillEntryInputEdgeEvent : IGameEvent
{
    public readonly SkillEntrySlot Entry;
    public readonly SkillEntryInputEdge Edge;
    /// <summary><see cref="Time.time"/>（秒），与 InputReader 槽位表时间戳一致。</summary>
    public readonly float TimeSeconds;

    public SkillEntryInputEdgeEvent(SkillEntrySlot entry, SkillEntryInputEdge edge, float timeSeconds)
    {
        Entry = entry;
        Edge = edge;
        TimeSeconds = timeSeconds;
    }
}

/// <summary>
/// 「OnSkill_NN_Pressed / Released」事件式订阅门面（蓝图 Phase 7 / 命名规范 §五）。<br/>
/// 内部桥接 <see cref="SkillEntryInputEdgeEvent"/>，让消费方用强类型 C# event 写：
/// <code>SkillEntryInputBus.OnPressed += (entry, t) =&gt; { if (entry == SkillEntrySlot.Q) ... }</code><br/>
/// 与 <see cref="InputReader"/> 轮询 API 同源——同一帧两条路径都能拿到，禁止同帧双派发意图（核心管线建议只走脉冲）。
/// </summary>
public static class SkillEntryInputBus
{
    static bool s_subscribed;
    static Action<SkillEntrySlot, float> s_pressed;
    static Action<SkillEntrySlot, float> s_released;

    /// <summary>按下边沿（physical down）。回调参数：(entry, Time.time)。</summary>
    public static event Action<SkillEntrySlot, float> OnPressed
    {
        add { EnsureSubscribed(); s_pressed += value; }
        remove { s_pressed -= value; }
    }

    /// <summary>松开边沿（physical up）。回调参数：(entry, Time.time)。</summary>
    public static event Action<SkillEntrySlot, float> OnReleased
    {
        add { EnsureSubscribed(); s_released += value; }
        remove { s_released -= value; }
    }

    static void EnsureSubscribed()
    {
        if (s_subscribed)
        {
            return;
        }

        GlobalEventBus.Subscribe<SkillEntryInputEdgeEvent>(HandleEdge);
        s_subscribed = true;
    }

    static void HandleEdge(SkillEntryInputEdgeEvent ev)
    {
        if (ev.Edge == SkillEntryInputEdge.Pressed)
        {
            s_pressed?.Invoke(ev.Entry, ev.TimeSeconds);
        }
        else
        {
            s_released?.Invoke(ev.Entry, ev.TimeSeconds);
        }
    }
}

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

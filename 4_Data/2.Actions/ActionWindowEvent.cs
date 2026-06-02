using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 动作时间窗上的运行时事件（<b>不进</b> GameplayTag / State 位掩码）。
/// 用于 HitFrame、音效、特效等一次性或区间触发。
/// </summary>
[Serializable]
public struct ActionWindowEvent
{
    public ActionWindowRuntimeEventKind Kind;

    /// <summary>依 Kind：SFX id、VFX 路径、动画事件名等。</summary>
    public string Payload;

    /// <summary>窗内归一化偏移 0~1（0=窗起点）。</summary>
    [Range(0f, 1f)]
    public float NormalizedOffset;
}

/// <summary>窗口事件种类（可继续扩展，保持序列化兼容）。</summary>
public enum ActionWindowRuntimeEventKind : byte
{
    None = 0,

    /// <summary>攻击/受击结算帧（逻辑命中采样）。</summary>
    HitFrame = 1,

    PlaySfx = 2,
    SpawnVfx = 3,

    /// <summary>
    /// 蓄力挂起进入：Action 时间线告诉 Skill 层「现在到了蓄势姿势的关键帧」。
    /// 仅当 <see cref="ChargeConfig.holdAnchorMode"/> = ByActionWindow 时生效。
    /// </summary>
    ChargeHoldEnter = 4,

    /// <summary>蓄力挂起离开：Action 时间线告诉 Skill 层「蓄势姿势结束」。</summary>
    ChargeHoldExit = 5,
}

/// <summary>
/// 播放时间轴推进时，按归一化时间采集本帧应触发的窗口事件（非 Tag）。
/// </summary>
public static class ActionWindowTimelineEvents
{
    const float Epsilon = 1e-5f;

    /// <summary>穿越窗内 <see cref="ActionWindowEvent.NormalizedOffset"/> 时刻时触发（139.2）。</summary>
    public static void AppendEventsOnCrossing(
        IReadOnlyList<ActionWindow> windows,
        float prevNormalized,
        float nextNormalized,
        List<ActionWindowEvent> buffer)
    {
        if (windows == null || buffer == null)
        {
            return;
        }

        var a = Mathf.Clamp01(prevNormalized);
        var b = Mathf.Clamp01(nextNormalized);
        if (b < a)
        {
            (a, b) = (b, a);
        }

        for (var i = 0; i < windows.Count; i++)
        {
            var w = windows[i];
            if (w.RuntimeEvents == null || w.RuntimeEvents.Count == 0)
            {
                continue;
            }

            var s = Mathf.Min(w.NormalizedStart, w.NormalizedEnd);
            var e = Mathf.Max(w.NormalizedStart, w.NormalizedEnd);

            for (var k = 0; k < w.RuntimeEvents.Count; k++)
            {
                var ev = w.RuntimeEvents[k];
                if (ev.Kind == ActionWindowRuntimeEventKind.None)
                {
                    continue;
                }

                var t = Mathf.Lerp(s, e, Mathf.Clamp01(ev.NormalizedOffset));
                if (ActionTimelineSampler.Crossed(a, b, t))
                {
                    buffer.Add(ev);
                }
            }
        }
    }

    /// <summary>
    /// 当归一化时间从区间外进入 <c>[NormalizedStart, NormalizedEnd]</c> 时，将窗口上 offset≈0 且未配置偏移的事件追加到 buffer。
    /// </summary>
    [Obsolete("Use AppendEventsOnCrossing for per-offset timing.")]
    public static void AppendEventsOnWindowEnter(
        IReadOnlyList<ActionWindow> windows,
        float prevNormalized,
        float nextNormalized,
        List<ActionWindowEvent> buffer)
    {
        AppendEventsOnCrossing(windows, prevNormalized, nextNormalized, buffer);
    }
}

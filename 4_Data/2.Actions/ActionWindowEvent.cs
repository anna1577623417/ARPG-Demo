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
}

/// <summary>窗口事件种类（可继续扩展，保持序列化兼容）。</summary>
public enum ActionWindowRuntimeEventKind : byte
{
    None = 0,

    /// <summary>攻击/受击结算帧（逻辑命中采样）。</summary>
    HitFrame = 1,

    PlaySfx = 2,
    SpawnVfx = 3,
}

/// <summary>
/// 播放时间轴推进时，按归一化时间采集本帧应触发的窗口事件（非 Tag）。
/// </summary>
public static class ActionWindowTimelineEvents
{
    /// <summary>
    /// 当归一化时间从区间外进入 <c>[NormalizedStart, NormalizedEnd]</c> 时，将窗口上配置的事件追加到 buffer。
    /// </summary>
    public static void AppendEventsOnWindowEnter(
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

        for (var i = 0; i < windows.Count; i++)
        {
            var w = windows[i];
            if (w.RuntimeEvents == null || w.RuntimeEvents.Count == 0)
            {
                continue;
            }

            var s = w.NormalizedStart;
            var e = w.NormalizedEnd;
            if (e < s)
            {
                (s, e) = (e, s);
            }

            var wasInside = a >= s && a <= e + 1e-6f;
            var nowInside = b >= s && b <= e + 1e-6f;
            if (wasInside || !nowInside)
            {
                continue;
            }

            for (var k = 0; k < w.RuntimeEvents.Count; k++)
            {
                buffer.Add(w.RuntimeEvents[k]);
            }
        }
    }
}

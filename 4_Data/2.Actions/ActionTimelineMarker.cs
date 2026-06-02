using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 动作归一化时间轴上的离散/区间标记（139.2 P2/P3）：FX / Audio / Camera / TimeScale。
/// 与 <see cref="ActionWindow"/> 切片正交；不进 GameplayTag 轨。
/// </summary>
[Serializable]
public struct ActionTimelineMarker
{
    [Range(0f, 1f)]
    [Tooltip("触发时刻（动作归一化 0~1）。")]
    public float NormalizedTime;

    public ActionTimelineMarkerKind Kind;

    [Tooltip("依 Kind：音效 id、VFX 路径、相机配置 id 等。")]
    public string Payload;

    [Tooltip("瞬时：真实秒（HitStop/Shake）；区间：归一化长度（SlowMo/BulletTime/Lock）。")]
    public float Duration;

    [Tooltip("强度：震动幅度、时间缩放倍率、FOV 推进量等。")]
    public float Intensity;
}

/// <summary>表现层时间轴标记种类。</summary>
public enum ActionTimelineMarkerKind : byte
{
    None = 0,

    HitFrame = 1,
    PlaySfx = 2,
    SpawnVfx = 3,

    CameraShake = 16,
    CameraPush = 17,
    CameraLock = 18,

    TimeScaleHitStop = 32,
    TimeScaleSlowMo = 33,
    TimeScaleBulletTime = 34,
}

public static class ActionTimelineMarkerKinds
{
    public static bool IsZone(ActionTimelineMarkerKind kind) =>
        kind is ActionTimelineMarkerKind.CameraPush
            or ActionTimelineMarkerKind.CameraLock
            or ActionTimelineMarkerKind.TimeScaleSlowMo
            or ActionTimelineMarkerKind.TimeScaleBulletTime;

    public static bool IsInstant(ActionTimelineMarkerKind kind) =>
        kind is ActionTimelineMarkerKind.CameraShake
            or ActionTimelineMarkerKind.TimeScaleHitStop
            or ActionTimelineMarkerKind.PlaySfx
            or ActionTimelineMarkerKind.SpawnVfx
            or ActionTimelineMarkerKind.HitFrame;
}

/// <summary>按归一化时间推进采集标记与窗内事件。</summary>
public static class ActionTimelineSampler
{
    const float Epsilon = 1e-5f;

    public static void AppendMarkersCrossed(
        IReadOnlyList<ActionTimelineMarker> markers,
        float prevNormalized,
        float nextNormalized,
        List<ActionTimelineMarker> buffer)
    {
        if (markers == null || buffer == null)
        {
            return;
        }

        var a = Mathf.Clamp01(prevNormalized);
        var b = Mathf.Clamp01(nextNormalized);
        if (b < a)
        {
            (a, b) = (b, a);
        }

        for (var i = 0; i < markers.Count; i++)
        {
            var m = markers[i];
            if (m.Kind == ActionTimelineMarkerKind.None)
            {
                continue;
            }

            if (Crossed(a, b, m.NormalizedTime))
            {
                buffer.Add(m);
            }
        }
    }

    public static void AppendTeleportsCrossed(
        IReadOnlyList<TeleportTrigger> teleports,
        float prevNormalized,
        float nextNormalized,
        List<TeleportTrigger> buffer)
    {
        if (teleports == null || buffer == null)
        {
            return;
        }

        var a = Mathf.Clamp01(prevNormalized);
        var b = Mathf.Clamp01(nextNormalized);
        if (b < a)
        {
            (a, b) = (b, a);
        }

        for (var i = 0; i < teleports.Count; i++)
        {
            var t = teleports[i];
            if (Crossed(a, b, t.TriggerTime))
            {
                buffer.Add(t);
            }
        }
    }

    public static bool IsZoneActive(in ActionTimelineMarker zone, float normalizedTime)
    {
        if (!ActionTimelineMarkerKinds.IsZone(zone.Kind) || zone.Duration <= Epsilon)
        {
            return false;
        }

        var start = zone.NormalizedTime;
        var end = Mathf.Clamp01(start + zone.Duration);
        return normalizedTime >= start - Epsilon && normalizedTime <= end + Epsilon;
    }

    public static bool Crossed(float prev, float next, float threshold)
    {
        return prev + Epsilon < threshold && next + Epsilon >= threshold;
    }
}

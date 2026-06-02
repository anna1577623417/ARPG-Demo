#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Action 时间轴 Scene 绘制（141.1）：统一 Handles，禁止 Gizmos。
/// </summary>
internal static class ActionDataTimelineSceneBridge
{
    // 兼容旧 SyncSceneBridge 字段（由编辑器窗口写入）
    public static ActionDataSO Action;
    public static float PreviewTime;
    public static Transform Anchor;

    public static void Clear()
    {
        Action = null;
        PreviewTime = 0f;
        Anchor = null;
    }

    public static ActionTimelinePreviewContext BuildFromLegacyState() =>
        ActionTimelinePreviewContext.Build(Action, PreviewTime, Anchor);

    /// <summary>OnSceneGUI 唯一入口。</summary>
    public static void DrawSceneGUI(in ActionTimelinePreviewContext ctx)
    {
        if (ctx.Action == null)
        {
            return;
        }

        if (!ctx.HasAnchor)
        {
            DrawNoAnchorHint();
            return;
        }

        DrawPreviewPlayhead(ctx);
        DrawCombatVolumes(ctx);
        DrawActiveWindowBands(ctx);
        DrawTeleportTrack(ctx);
        DrawMarkerTrack(ctx);
        DrawSceneHud(ctx);
    }

    static void DrawNoAnchorHint()
    {
        Handles.BeginGUI();
        var rect = new Rect(12f, 12f, 320f, 40f);
        GUI.Box(rect, "Action Timeline：请指定 Gizmo Anchor 或选中场景中的 Player。");
        Handles.EndGUI();
    }

    static void DrawSceneHud(in ActionTimelinePreviewContext ctx)
    {
        var labelPos = ctx.Position + Vector3.up * 2.2f;
        Handles.color = Color.white;
        Handles.Label(labelPos, $"{ctx.Action.name}  t={ctx.NormalizedTime:0.00}");
    }

    static void DrawPreviewPlayhead(in ActionTimelinePreviewContext ctx)
    {
        var t = ctx.NormalizedTime;
        var end = ctx.Position + ctx.PlanarForward * (t * 2.5f);
        Handles.color = new Color(1f, 0.92f, 0.2f, 0.9f);
        Handles.DrawDottedLine(ctx.Position, end, 4f);
        Handles.SphereHandleCap(0, end, Quaternion.identity, 0.08f, EventType.Repaint);
        Handles.Label(end + Vector3.up * 0.15f, $"t={t:0.00}");
    }

    static void DrawCombatVolumes(in ActionTimelinePreviewContext ctx)
    {
        var bits = ctx.ActiveStateBits;
        var pos = ctx.Position;
        var fwd = ctx.PlanarForward;

        if ((bits & (ulong)StateTag.HitboxActive_Window) != 0)
        {
            var center = pos + fwd * 1.1f + Vector3.up * 1f;
            Handles.color = new Color(1f, 0.22f, 0.22f, 0.85f);
            Handles.DrawWireCube(center, new Vector3(1.2f, 1.8f, 1.4f));
            Handles.Label(center + Vector3.up * 1.1f, "Hitbox");
        }

        if ((bits & (ulong)StateTag.HurtboxActive_Window) != 0)
        {
            var center = pos + Vector3.up * 1f;
            Handles.color = new Color(0.85f, 0.35f, 1f, 0.85f);
            DrawWireSphere(center, 0.55f);
            Handles.Label(center + Vector3.up * 0.7f, "Hurtbox");
        }

        if ((bits & (ulong)StateTag.Invulnerable) != 0)
        {
            var center = pos + Vector3.up * 1.6f;
            Handles.color = new Color(1f, 0.92f, 0.2f, 0.9f);
            DrawWireSphere(center, 0.35f);
            Handles.Label(center + Vector3.up * 0.45f, "Invuln");
        }
    }

    static void DrawActiveWindowBands(in ActionTimelinePreviewContext ctx)
    {
        var windows = ctx.Action.Windows;
        if (windows == null)
        {
            return;
        }

        var t = ctx.NormalizedTime;
        for (var i = 0; i < windows.Count; i++)
        {
            var w = windows[i];
            if (t < w.NormalizedStart || t > w.NormalizedEnd)
            {
                continue;
            }

            var ringCenter = ctx.Position + Vector3.up * 0.05f;
            Handles.color = new Color(0.3f, 0.85f, 1f, 0.35f);
            Handles.DrawWireDisc(ringCenter, Vector3.up, 0.65f + i * 0.04f);
        }
    }

    static void DrawTeleportTrack(in ActionTimelinePreviewContext ctx)
    {
        var teleports = ctx.Action.TeleportTriggers;
        if (teleports == null)
        {
            return;
        }

        var pos = ctx.Position;
        var fwd = ctx.PlanarForward;
        for (var i = 0; i < teleports.Count; i++)
        {
            var tp = teleports[i];
            var active = Mathf.Abs(tp.TriggerTime - ctx.NormalizedTime) < 0.03f;
            var start = pos + Vector3.up * 0.15f;
            var end = start + fwd * tp.Distance;

            Handles.color = active ? Color.white : new Color(0.2f, 0.85f, 1f, 0.75f);
            Handles.DrawLine(start, end);
            Handles.ArrowHandleCap(0, end, Quaternion.LookRotation(fwd), active ? 0.4f : 0.28f, EventType.Repaint);
            if (active)
            {
                Handles.Label(end + Vector3.up * 0.2f, $"Teleport {tp.Distance:0.0}m");
            }
        }
    }

    static void DrawMarkerTrack(in ActionTimelinePreviewContext ctx)
    {
        var markers = ctx.Action.TimelineMarkers;
        if (markers == null)
        {
            return;
        }

        var pos = ctx.Position;
        var fwd = ctx.PlanarForward;
        for (var i = 0; i < markers.Count; i++)
        {
            var m = markers[i];
            var p = pos + fwd * (m.NormalizedTime * 2f) + Vector3.up * (0.25f + i * 0.05f);
            var col = MarkerColor(m.Kind);
            var instantActive = Mathf.Abs(m.NormalizedTime - ctx.NormalizedTime) < 0.025f;
            var zoneActive = ActionTimelineSampler.IsZoneActive(in m, ctx.NormalizedTime);
            var active = instantActive || zoneActive;

            Handles.color = active ? Color.white : col;
            Handles.SphereHandleCap(0, p, Quaternion.identity, active ? 0.14f : 0.1f, EventType.Repaint);

            if (ActionTimelineMarkerKinds.IsZone(m.Kind) && m.Duration > 0.001f)
            {
                var zoneEnd = Mathf.Clamp01(m.NormalizedTime + m.Duration);
                var p1 = pos + fwd * (m.NormalizedTime * 2f);
                var p2 = pos + fwd * (zoneEnd * 2f);
                Handles.color = new Color(col.r, col.g, col.b, 0.35f);
                Handles.DrawDottedLine(p1, p2, 3f);
            }

            Handles.color = col;
            Handles.Label(p + Vector3.up * 0.12f, m.Kind.ToString());
        }
    }

    static void DrawWireSphere(Vector3 center, float radius)
    {
        Handles.DrawWireDisc(center, Vector3.up, radius);
        Handles.DrawWireDisc(center, Vector3.right, radius);
        Handles.DrawWireDisc(center, Vector3.forward, radius);
    }

    static Color MarkerColor(ActionTimelineMarkerKind kind) => kind switch
    {
        ActionTimelineMarkerKind.SpawnVfx => new Color(0.95f, 0.4f, 0.95f),
        ActionTimelineMarkerKind.PlaySfx => new Color(0.45f, 0.85f, 1f),
        ActionTimelineMarkerKind.CameraShake => new Color(0.7f, 0.7f, 1f),
        ActionTimelineMarkerKind.CameraPush => new Color(0.55f, 0.65f, 1f),
        ActionTimelineMarkerKind.CameraLock => new Color(0.35f, 0.45f, 0.95f),
        ActionTimelineMarkerKind.TimeScaleHitStop => new Color(1f, 0.55f, 0.2f),
        ActionTimelineMarkerKind.TimeScaleSlowMo => new Color(1f, 0.75f, 0.35f),
        ActionTimelineMarkerKind.TimeScaleBulletTime => new Color(1f, 0.35f, 0.35f),
        _ => Color.white,
    };
}
#endif

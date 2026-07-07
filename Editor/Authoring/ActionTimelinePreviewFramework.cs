#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 171.1 Phase4 — 统一 Action Preview Framework：各 Track 由 PreviewContext 单点驱动。
/// </summary>
internal static class ActionTimelinePreviewFramework
{
    static Vector3[] s_motionPathBuffer;
    static Vector3[] s_ghostPathBuffer;

    public static void DrawScene(
        in ActionTimelinePreviewContext ctx,
        in ActionTimelinePreviewTrackVisibility visibility)
        => DrawScene(in ctx, in visibility, ActionTimelinePreviewVisibility.Load());

    public static void DrawScene(
        in ActionTimelinePreviewContext ctx,
        in ActionTimelinePreviewTrackVisibility visibility,
        PreviewVisibilityMask previewMask)
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

        if (visibility.Motion)
        {
            DrawMotionTrack(ctx, previewMask);
        }

        if (visibility.GhostTrail && ActionTimelinePreviewVisibility.Has(previewMask, PreviewVisibilityMask.GhostTrail))
        {
            DrawGhostTrack(ctx);
        }

        if (ActionTimelinePreviewVisibility.Has(previewMask, PreviewVisibilityMask.Pose)
            || ActionTimelinePreviewVisibility.Has(previewMask, PreviewVisibilityMask.MotionDriven))
        {
            DrawPlayhead(ctx);
        }

        if (visibility.Combat)
        {
            DrawCombatTrack(ctx, previewMask);
        }

        if (visibility.Teleport && ActionTimelinePreviewVisibility.Has(previewMask, PreviewVisibilityMask.Teleport))
        {
            DrawTeleportTrack(ctx);
        }

        if ((visibility.Fx || visibility.Audio || visibility.Camera || visibility.TimeScale)
            && ActionTimelinePreviewVisibility.Has(previewMask, PreviewVisibilityMask.Presentation))
        {
            DrawPresentationTracks(ctx, visibility);
        }

        if (ActionTimelinePreviewVisibility.Has(previewMask, PreviewVisibilityMask.FutureMarkers))
        {
            DrawFutureMarkers(ctx);
        }

        if (ActionTimelinePreviewVisibility.Has(previewMask, PreviewVisibilityMask.SceneAnchor))
        {
            DrawSceneAnchor(ctx);
        }

        if (ActionTimelinePreviewVisibility.Has(previewMask, PreviewVisibilityMask.SceneInfo))
        {
            DrawSceneSpaceInfo(ctx);
        }
    }

    static void DrawNoAnchorHint()
    {
        Handles.BeginGUI();
        GUI.Box(new Rect(12f, 12f, 360f, 40f),
            "Action Timeline：请指定 Gizmo Anchor 或选中场景中的 Player。");
        Handles.EndGUI();
    }

    static Vector3[] s_xAxisBuffer;
    static Vector3[] s_yAxisBuffer;
    static Vector3[] s_zAxisBuffer;

    static void DrawMotionTrack(in ActionTimelinePreviewContext ctx, PreviewVisibilityMask previewMask)
    {
        var sampleCount = PreviewMotionDriver.DefaultPathSampleCount;
        var showCompositeOrAxis = ctx.MotionMode is MotionPreviewMode.Overlay
                                                 or MotionPreviewMode.MotionDriven;

        if (showCompositeOrAxis && ctx.HasMotionProfile)
        {
            if (ActionTimelinePreviewVisibility.Has(previewMask, PreviewVisibilityMask.Composite))
            {
                EnsureBuffer(ref s_motionPathBuffer, sampleCount);
                var pathForward = ResolveMotionPathForward(ctx);
                PreviewMotionDriver.BuildMotionProfilePath(
                    ctx.Action.MotionProfile,
                    ctx.AnchorOrigin,
                    pathForward,
                    sampleCount,
                    s_motionPathBuffer);

                Handles.color = ActionTimelineGizmoColors.Composite;
                Handles.DrawAAPolyLine(4f, s_motionPathBuffer);
            }

            DrawAxisDecomposition(ctx, sampleCount, previewMask);
        }

        if (ctx.MotionMode is MotionPreviewMode.ClipRootMotion or MotionPreviewMode.Overlay)
        {
            if (ActionTimelineRootMotionSampler.TryBuildPath(
                    ctx.Action, ctx.Anchor, sampleCount, out var rootPath, out _))
            {
                Handles.color = ActionTimelineGizmoColors.RootMotion;
                Handles.DrawAAPolyLine(3f, rootPath);
            }
        }

        if (ctx.MotionMode == MotionPreviewMode.Overlay && ctx.HasMotionProfile)
        {
            var compMid = Vector3.Lerp(ctx.RootMotionWorldPosition, ctx.MotionWorldPosition, 0.5f);
            Handles.color = ActionTimelineGizmoColors.Composite;
            Handles.DrawDottedLine(ctx.RootMotionWorldPosition, ctx.MotionWorldPosition, 4f);
            Handles.Label(compMid + Vector3.up * 0.25f, $"Δ={ctx.RootMotionOverlayDeltaMeters:F2}m");
            Handles.Label(ctx.MotionWorldPosition + Vector3.up * 0.35f, "MotionProfile");
            Handles.color = ActionTimelineGizmoColors.RootMotion;
            Handles.Label(ctx.RootMotionWorldPosition + Vector3.up * 0.5f, "RootMotion");
        }
    }

    /// <summary>171.5 W1：三色分轴。每轴从 CaptureOrigin 出发，仅在对应轴方向有 offset。</summary>
    static void DrawAxisDecomposition(
        in ActionTimelinePreviewContext ctx,
        int sampleCount,
        PreviewVisibilityMask previewMask)
    {
        var profile = ctx.Action.MotionProfile;
        if (profile == null) return;
        var curves = profile.AxisCurves;
        if (!curves.HasAnyCurve) return;

        var hasX = HasNonZeroCurve(curves.XCurve);
        var hasY = HasNonZeroCurve(curves.YCurve);
        var hasZ = HasNonZeroCurve(curves.ZCurve);
        if (!hasX && !hasY && !hasZ) return;

        var origin = ctx.AnchorOrigin;
        var heading = Quaternion.LookRotation(ResolveMotionPathForward(ctx), Vector3.up);

        if (hasX && ActionTimelinePreviewVisibility.Has(previewMask, PreviewVisibilityMask.XAxis))
        {
            EnsureBuffer(ref s_xAxisBuffer, sampleCount);
            BuildAxisPath(curves, origin, heading, sampleCount, AxisProj.X, s_xAxisBuffer);
            Handles.color = ActionTimelineGizmoColors.XAxis;
            Handles.DrawAAPolyLine(2f, s_xAxisBuffer);
        }
        if (hasY && ActionTimelinePreviewVisibility.Has(previewMask, PreviewVisibilityMask.YAxis))
        {
            EnsureBuffer(ref s_yAxisBuffer, sampleCount);
            BuildAxisPath(curves, origin, heading, sampleCount, AxisProj.Y, s_yAxisBuffer);
            Handles.color = ActionTimelineGizmoColors.YAxis;
            Handles.DrawAAPolyLine(2f, s_yAxisBuffer);
        }
        if (hasZ && ActionTimelinePreviewVisibility.Has(previewMask, PreviewVisibilityMask.ZAxis))
        {
            EnsureBuffer(ref s_zAxisBuffer, sampleCount);
            BuildAxisPath(curves, origin, heading, sampleCount, AxisProj.Z, s_zAxisBuffer);
            Handles.color = ActionTimelineGizmoColors.ZAxis;
            Handles.DrawAAPolyLine(2f, s_zAxisBuffer);
        }
    }

    enum AxisProj : byte { X, Y, Z }

    static void BuildAxisPath(
        in MotionAxisCurves curves,
        Vector3 origin, Quaternion heading,
        int sampleCount, AxisProj axis, Vector3[] outBuf)
    {
        for (var i = 0; i < sampleCount; i++)
        {
            var t = sampleCount > 1 ? i / (float)(sampleCount - 1) : 0f;
            var local = curves.SampleLocalPosition(t);
            // 仅保留对应轴的分量
            var projected = axis switch
            {
                AxisProj.X => new Vector3(local.x, 0f, 0f),
                AxisProj.Y => new Vector3(0f, local.y, 0f),
                AxisProj.Z => new Vector3(0f, 0f, local.z),
                _ => Vector3.zero,
            };
            outBuf[i] = origin + heading * projected;
        }
    }

    static bool HasNonZeroCurve(AnimationCurve c) =>
        c != null && c.length > 0;

    static void DrawGhostTrack(in ActionTimelinePreviewContext ctx)
    {
        if (!ctx.HasMotionProfile || ctx.MotionMode == MotionPreviewMode.ClipRootMotion)
        {
            return;
        }

        var duration = ctx.LogicDurationSeconds;
        if (duration <= 0.001f)
        {
            return;
        }

        const int ghostSamples = 6;
        EnsureBuffer(ref s_ghostPathBuffer, ghostSamples);
        s_ghostPathBuffer[0] = ctx.MotionWorldPosition;

        var written = 1;
        for (var i = 1; i < ghostSamples; i++)
        {
            var sec = i * 0.12f;
            var futureT = ctx.NormalizedTime + sec / duration;
            if (futureT > 1f)
            {
                break;
            }

            s_ghostPathBuffer[i] = EvaluateGhostWorldPosition(ctx, futureT);
            written++;
        }

        if (written > 1)
        {
            Handles.color = new Color(1f, 1f, 1f, 0.35f);
            Handles.DrawDottedLine(s_ghostPathBuffer[0], s_ghostPathBuffer[written - 1], 3f);
        }

        for (var i = 1; i < written; i++)
        {
            var alpha = 0.35f - i * 0.04f;
            Handles.color = new Color(1f, 1f, 1f, Mathf.Max(0.08f, alpha));
            Handles.DrawWireDisc(s_ghostPathBuffer[i], Vector3.up, 0.28f + i * 0.02f);
        }
    }

    static void DrawPlayhead(in ActionTimelinePreviewContext ctx)
    {
        Handles.color = new Color(1f, 0.15f, 0.15f, 0.95f);
        Handles.SphereHandleCap(0, ctx.Position, Quaternion.identity, 0.14f, EventType.Repaint);

        if (ctx.UseMotionProfileForVolumes
            && (ctx.MotionWorldPosition - ctx.AnchorOrigin).sqrMagnitude > 0.0001f)
        {
            Handles.color = new Color(1f, 0.15f, 0.15f, 0.45f);
            Handles.DrawDottedLine(ctx.AnchorOrigin + Vector3.up * 0.05f, ctx.Position, 4f);
        }

        if (ctx.UsesActionYawPreview)
        {
            DrawActionYawArrow(ctx);
        }
    }

    static void DrawActionYawArrow(in ActionTimelinePreviewContext ctx)
    {
        var origin = ctx.Position + Vector3.up * 0.08f;
        var yawFwd = ctx.ActionYawForward;
        yawFwd.y = 0f;
        if (yawFwd.sqrMagnitude < 0.0001f)
        {
            return;
        }

        yawFwd.Normalize();
        Handles.color = new Color(0.55f, 0.35f, 1f, 0.95f);
        var tip = origin + yawFwd * 0.75f;
        Handles.DrawLine(origin, tip);
        Handles.ConeHandleCap(0, tip, Quaternion.LookRotation(yawFwd, Vector3.up), 0.12f, EventType.Repaint);
        Handles.Label(tip + Vector3.up * 0.05f, $"Yaw {ctx.ActionYawDegrees:F0}°");
    }

    static Vector3 ResolveMotionPathForward(in ActionTimelinePreviewContext ctx)
        => ctx.PlanarForward.sqrMagnitude > 0.0001f ? ctx.PlanarForward : Vector3.forward;

    static void DrawCombatTrack(in ActionTimelinePreviewContext ctx, PreviewVisibilityMask previewMask)
    {
        var bits = ctx.ActiveStateBits;
        var pos = ctx.TrackAnchor;
        var fwd = ctx.PlanarForward;

        if ((bits & (ulong)StateTag.HitboxActive_Window) != 0
            && ActionTimelinePreviewVisibility.Has(previewMask, PreviewVisibilityMask.Hitbox))
        {
            var center = pos + fwd * 1.1f + Vector3.up * 1f;
            Handles.color = new Color(1f, 0.22f, 0.22f, 0.85f);
            Handles.DrawWireCube(center, new Vector3(1.2f, 1.8f, 1.4f));
            Handles.Label(center + Vector3.up * 1.1f, "Hitbox");
        }

        if ((bits & (ulong)StateTag.HurtboxActive_Window) != 0
            && ActionTimelinePreviewVisibility.Has(previewMask, PreviewVisibilityMask.Hurtbox))
        {
            var center = pos + Vector3.up * 1f;
            Handles.color = new Color(0.85f, 0.35f, 1f, 0.85f);
            DrawWireSphere(center, 0.55f);
            Handles.Label(center + Vector3.up * 0.7f, "Hurtbox");
        }

        if ((bits & (ulong)StateTag.Invulnerable) != 0
            && ActionTimelinePreviewVisibility.Has(previewMask, PreviewVisibilityMask.InvuInter))
        {
            var center = pos + Vector3.up * 1.6f;
            Handles.color = new Color(1f, 0.92f, 0.2f, 0.9f);
            DrawWireSphere(center, 0.35f);
            Handles.Label(center + Vector3.up * 0.45f, "Invuln");
        }

        if (!ActionTimelinePreviewVisibility.Has(previewMask, PreviewVisibilityMask.Windows))
        {
            return;
        }

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

            var ringCenter = pos + Vector3.up * 0.05f;
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

        var pos = ctx.TrackAnchor;
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

    static void DrawPresentationTracks(
        in ActionTimelinePreviewContext ctx,
        in ActionTimelinePreviewTrackVisibility visibility)
    {
        var markers = ctx.Action.TimelineMarkers;
        if (markers == null)
        {
            return;
        }

        for (var i = 0; i < markers.Count; i++)
        {
            var m = markers[i];
            if (!ShouldDrawMarker(m.Kind, visibility))
            {
                continue;
            }

            DrawPresentationMarker(ctx, in m, i);
        }
    }

    static bool ShouldDrawMarker(ActionTimelineMarkerKind kind, in ActionTimelinePreviewTrackVisibility vis)
    {
        return kind switch
        {
            ActionTimelineMarkerKind.SpawnVfx => vis.Fx,
            ActionTimelineMarkerKind.PlaySfx => vis.Audio,
            ActionTimelineMarkerKind.CameraShake
                or ActionTimelineMarkerKind.CameraPush
                or ActionTimelineMarkerKind.CameraLock => vis.Camera,
            ActionTimelineMarkerKind.TimeScaleHitStop
                or ActionTimelineMarkerKind.TimeScaleSlowMo
                or ActionTimelineMarkerKind.TimeScaleBulletTime => vis.TimeScale,
            _ => vis.Fx,
        };
    }

    static void DrawPresentationMarker(
        in ActionTimelinePreviewContext ctx,
        in ActionTimelineMarker m,
        int index)
    {
        var worldAtMarker = ctx.ResolveWorldAtNormalizedTime(m.NormalizedTime);
        var p = worldAtMarker + Vector3.up * (0.35f + index * 0.04f);
        var col = MarkerColor(m.Kind);
        var instantActive = Mathf.Abs(m.NormalizedTime - ctx.NormalizedTime) < 0.025f;
        var zoneActive = ActionTimelineSampler.IsZoneActive(in m, ctx.NormalizedTime);
        var active = instantActive || zoneActive;

        Handles.color = active ? Color.white : col;
        DrawMarkerIcon(m.Kind, p, active ? 0.16f : 0.11f);

        if (ActionTimelineMarkerKinds.IsZone(m.Kind) && m.Duration > 0.001f)
        {
            var zoneEnd = Mathf.Clamp01(m.NormalizedTime + m.Duration);
            var p1 = ctx.ResolveWorldAtNormalizedTime(m.NormalizedTime);
            var p2 = ctx.ResolveWorldAtNormalizedTime(zoneEnd);
            Handles.color = new Color(col.r, col.g, col.b, 0.35f);
            Handles.DrawDottedLine(p1, p2, 3f);
        }

        if (ActionTimelineMarkerKinds.IsInstant(m.Kind) && !active)
        {
            Handles.color = new Color(col.r, col.g, col.b, 0.25f);
            Handles.DrawWireDisc(worldAtMarker, Vector3.up, 0.08f);
        }

        Handles.color = col;
        Handles.Label(p + Vector3.up * 0.12f, m.Kind.ToString());
    }

    static void DrawMarkerIcon(ActionTimelineMarkerKind kind, Vector3 pos, float size)
    {
        switch (kind)
        {
            case ActionTimelineMarkerKind.CameraShake:
            case ActionTimelineMarkerKind.CameraPush:
            case ActionTimelineMarkerKind.CameraLock:
                var fwd = Vector3.forward;
                Handles.DrawWireCube(pos + Vector3.up * 0.05f, new Vector3(size, size * 0.6f, size * 1.4f));
                Handles.DrawLine(pos, pos + fwd * size * 2f);
                break;
            case ActionTimelineMarkerKind.TimeScaleHitStop:
            case ActionTimelineMarkerKind.TimeScaleSlowMo:
            case ActionTimelineMarkerKind.TimeScaleBulletTime:
                Handles.DrawWireDisc(pos, Vector3.up, size * 0.9f);
                break;
            default:
                Handles.SphereHandleCap(0, pos, Quaternion.identity, size, EventType.Repaint);
                break;
        }
    }

    static void DrawFutureMarkers(in ActionTimelinePreviewContext ctx)
    {
        var markers = ctx.Action.PreviewTimeMarkers;
        if (markers == null || markers.Count == 0 || !ctx.HasMotionProfile)
        {
            return;
        }

        var profile = ctx.Action.MotionProfile;
        var origin = ctx.AnchorOrigin;
        var fwd = ctx.PlanarForward.sqrMagnitude > 0.0001f ? ctx.PlanarForward : Vector3.forward;
        Handles.color = ActionTimelineGizmoColors.FutureMark;

        for (var i = 0; i < markers.Count; i++)
        {
            var t = Mathf.Clamp01(markers[i]);
            var world = PreviewMotionDriver.EvaluateWorldPosition(profile, t, origin, fwd);
            Handles.DrawWireDisc(world, Vector3.up, 0.1f);
            Handles.Label(world + Vector3.up * 0.14f, $"t={t:F3}");
        }
    }

    static void DrawSceneAnchor(in ActionTimelinePreviewContext ctx)
    {
        var origin = ctx.HasCaptureOrigin ? ctx.CaptureOriginPos : ctx.AnchorOrigin;
        Handles.color = new Color(1f, 1f, 1f, 0.85f);
        Handles.DrawLine(origin + Vector3.left * 0.25f, origin + Vector3.right * 0.25f);
        Handles.DrawLine(origin + Vector3.forward * 0.25f, origin + Vector3.back * 0.25f);
        Handles.DrawLine(origin, origin + Vector3.up * 0.35f);
    }

    static void DrawSceneSpaceInfo(in ActionTimelinePreviewContext ctx)
    {
        var model = ActionTimelineSpaceInfoModel.Build(ctx);
        var origin = ctx.HasCaptureOrigin ? ctx.CaptureOriginPos : ctx.AnchorOrigin;
        var labelPos = origin + Vector3.up * 1.8f;
        var lineHeight = 0.16f;
        var y = 0f;

        DrawStyledSceneLine(labelPos, ref y, lineHeight, ActionTimelineSpaceInfoField.Action, model);
        DrawStyledSceneLine(labelPos, ref y, lineHeight, ActionTimelineSpaceInfoField.Clip, model);
        DrawStyledSceneLine(labelPos, ref y, lineHeight, ActionTimelineSpaceInfoField.Seg, model);
        DrawStyledSceneLine(labelPos, ref y, lineHeight, ActionTimelineSpaceInfoField.AnimSpeed, model);
        DrawStyledSceneLine(labelPos, ref y, lineHeight, ActionTimelineSpaceInfoField.XyzLocal, model);
        DrawStyledSceneLine(labelPos, ref y, lineHeight, ActionTimelineSpaceInfoField.XyzWorld, model);
        DrawStyledSceneLine(labelPos, ref y, lineHeight, ActionTimelineSpaceInfoField.Origin, model);
        DrawStyledSceneLine(labelPos, ref y, lineHeight, ActionTimelineSpaceInfoField.Heading, model);
    }

    static void DrawStyledSceneLine(
        Vector3 anchor,
        ref float yOffset,
        float lineHeight,
        ActionTimelineSpaceInfoField field,
        in ActionTimelineSpaceInfoModel model)
    {
        if (!ActionTimelineSpaceInfoStyle.IsVisible(field))
        {
            return;
        }

        var text = ActionTimelineSpaceInfoStyle.GetLine(in model, field);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Handles.Label(anchor + Vector3.up * yOffset, text, ActionTimelineSpaceInfoStyle.GetSceneLabelStyle(field));
        yOffset += lineHeight;
    }

    public static void DrawTimelineSpaceInfo(
        in ActionTimelinePreviewContext ctx,
        Rect area)
    {
        var model = ActionTimelineSpaceInfoModel.Build(ctx);
        var y = area.y + 4f;
        var lineHeight = 16f;

        void DrawField(ActionTimelineSpaceInfoField field)
        {
            if (!ActionTimelineSpaceInfoStyle.IsVisible(field))
            {
                return;
            }

            var text = ActionTimelineSpaceInfoStyle.GetLine(in model, field);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var rect = new Rect(area.x + 6f, y, area.width - 12f, lineHeight);
            GUI.Label(rect, text, ActionTimelineSpaceInfoStyle.GetGuiStyle(field));
            y += lineHeight;
        }

        DrawField(ActionTimelineSpaceInfoField.Action);
        DrawField(ActionTimelineSpaceInfoField.Clip);
        DrawField(ActionTimelineSpaceInfoField.Seg);
        DrawField(ActionTimelineSpaceInfoField.AnimSpeed);
        DrawField(ActionTimelineSpaceInfoField.XyzLocal);
        DrawField(ActionTimelineSpaceInfoField.XyzWorld);
        DrawField(ActionTimelineSpaceInfoField.Origin);
        DrawField(ActionTimelineSpaceInfoField.Heading);
    }

    static void DrawWireSphere(Vector3 center, float radius)
    {
        Handles.DrawWireDisc(center, Vector3.up, radius);
        Handles.DrawWireDisc(center, Vector3.right, radius);
        Handles.DrawWireDisc(center, Vector3.forward, radius);
    }

    static Vector3 EvaluateGhostWorldPosition(in ActionTimelinePreviewContext ctx, float normalizedTime)
    {
        var profile = ctx.Action.MotionProfile;
        if (profile == null)
        {
            return ctx.AnchorOrigin;
        }

        var pathForward = ResolveMotionPathForward(ctx);
        return PreviewMotionDriver.EvaluateWorldPosition(
            profile, normalizedTime, ctx.AnchorOrigin, pathForward);
    }

    static void EnsureBuffer(ref Vector3[] buffer, int count)
    {
        if (buffer == null || buffer.Length != count)
        {
            buffer = new Vector3[count];
        }
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

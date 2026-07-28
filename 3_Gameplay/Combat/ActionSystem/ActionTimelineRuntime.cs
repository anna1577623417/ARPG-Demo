using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ActionData 时间轴运行时（139.2）：标记 / 窗内事件 / Teleport / 区间 TimeScale & Camera。
/// </summary>
public static class ActionTimelineRuntime
{
    static readonly List<ActionTimelineMarker> s_markerScratch = new List<ActionTimelineMarker>(16);
    static readonly List<TeleportTrigger> s_teleportScratch = new List<TeleportTrigger>(4);
    static readonly List<ActionWindowEvent> s_eventScratch = new List<ActionWindowEvent>(16);

    // 216.3 M1 L2 — 攻击判定物理查询复用缓冲（无堆分配）。
    static readonly Collider[] s_attackOverlap = new Collider[32];
    static readonly RaycastHit[] s_attackSweep = new RaycastHit[32];

    public static void Tick(
        IActionContext context,
        ActionDataSO action,
        float prevNormalized,
        float nextNormalized,
        Vector3 planarForward,
        ActionTimelinePlaybackState state)
    {
        if (context == null || context.Entity == null || action == null || state == null)
        {
            return;
        }

        FireCrossingMarkers(context, action, prevNormalized, nextNormalized, state);
        FireCrossingWindowEvents(context, action, prevNormalized, nextNormalized, state);
        FireCrossingCombatEvents(context, action, prevNormalized, nextNormalized, state); // 188.3 W9
        FireAttackClips(context, action, nextNormalized, state);                          // 216.3 M1 L2
        FireDefenseClips(context, action, nextNormalized, state);                         // 216.3 M5 L1
        FireCrossingTeleports(context, action, prevNormalized, nextNormalized, planarForward, state);
        UpdateZones(action, nextNormalized, state);
    }

    /// <summary>
    /// 216.3 M1 L2 — HitClip Active 区间驱动 AttackInstance：进入区间 Begin、区间内每帧 Sweep、离开区间 End。
    /// <para>单一真相：Active 区间即判定窗口；不读旧 CombatTrack、不做 <c>if (legacy)</c> 兜底。</para>
    /// </summary>
    static void FireAttackClips(
        IActionContext context,
        ActionDataSO action,
        float nextNt,
        ActionTimelinePlaybackState state)
    {
        var clips = action.AttackClips;
        if (clips == null || clips.Count == 0)
        {
            return;
        }

        for (var i = 0; i < clips.Count; i++)
        {
            var clip = clips[i];
            var hasProvider = clip.ShapeMode == HitShapeMode.WeaponTrace
                ? clip.WeaponSockets != null && clip.WeaponSockets.Count > 0
                : clip.Shape != null;
            if (!hasProvider)
            {
                continue;
            }

            var s = Mathf.Min(clip.ActiveStart, clip.ActiveEnd);
            var e = Mathf.Max(clip.ActiveStart, clip.ActiveEnd);
            var inside = nextNt >= s && nextNt <= e;
            var inst = state.GetOrCreateAttack(i);

            if (inside)
            {
                HitClipOriginResolver.Resolve(context.Entity, in clip, out var pos, out var rot);
                if (!inst.Active)
                {
                    inst.Begin(in clip, context.Entity, pos, rot);
                }

                inst.TickSweep(pos, rot, s_attackSweep, s_attackOverlap);
            }
            else if (inst.Active)
            {
                inst.End();
            }
        }
    }

    /// <summary>
    /// 216.3 M5 — DefenseClip Active 驱动：
    /// Guard → GuardVolumeProvider；Parry/Invincible → Registry 窗标志（供 Resolver）。
    /// </summary>
    static void FireDefenseClips(
        IActionContext context,
        ActionDataSO action,
        float nextNt,
        ActionTimelinePlaybackState state)
    {
        var clips = action.DefenseClips;
        if (clips == null || clips.Count == 0)
        {
            DefenseRuntimeRegistry.SetWindowFlags(context.Entity, false, false);
            return;
        }

        var anyParry = false;
        var anyInvincible = false;

        for (var i = 0; i < clips.Count; i++)
        {
            var clip = clips[i];
            var s = Mathf.Min(clip.ActiveStart, clip.ActiveEnd);
            var e = Mathf.Max(clip.ActiveStart, clip.ActiveEnd);
            var inside = nextNt >= s && nextNt <= e;

            if (clip.Kind == DefenseKind.Guard)
            {
                var guard = state.GetOrCreateGuard(i);
                if (inside)
                {
                    if (!guard.Active)
                    {
                        guard.Begin(in clip, context.Entity);
                    }

                    guard.Tick();
                }
                else if (guard.Active)
                {
                    guard.End();
                }

                continue;
            }

            if (!inside)
            {
                continue;
            }

            if (clip.Kind == DefenseKind.Parry)
            {
                anyParry = true;
                if (GameMainDebugSettings.CombatHit && state.TryFireDefenseWindowOnce(i))
                {
                    Debug.Log(
                        $"[Resolve] PARRY window on clip={clip.ResolvedName} active={s:F2}~{e:F2}");
                }
            }
            else if (clip.Kind == DefenseKind.Invincible)
            {
                anyInvincible = true;
                if (GameMainDebugSettings.CombatHit && state.TryFireDefenseWindowOnce(i))
                {
                    Debug.Log(
                        $"[Resolve] INVINCIBLE window on clip={clip.ResolvedName} active={s:F2}~{e:F2}");
                }
            }
        }

        DefenseRuntimeRegistry.SetWindowFlags(context.Entity, anyParry, anyInvincible);
    }

    /// <summary>188.3 W9 — Combat Track 时间轴穿越触发 CombatObjectSpawner.Spawn。</summary>
    static void FireCrossingCombatEvents(
        IActionContext context,
        ActionDataSO action,
        float prevNt,
        float nextNt,
        ActionTimelinePlaybackState state)
    {
        if (action.CombatTrack == null || action.CombatTrack.Length == 0) return;
        var spawner = context.CombatObjectSpawner;
        if (spawner == null) return; // Player 还未注入 Spawner（W9 接入前）

        for (var i = 0; i < action.CombatTrack.Length; i++)
        {
            ref var ev = ref action.CombatTrack[i];
            if (ev.Definition == null) continue;
            if (!ActionTimelineSampler.Crossed(prevNt, nextNt, ev.NormalizedTime)) continue;
            if (!state.TryFireCombatEventOnce(i)) continue;

            CombatSpawnResolver.Resolve(context.Entity, ev.Definition, in ev, out var pos, out var rot);
            spawner.Spawn(ev.Definition, context.Entity, pos, rot);
        }
    }

    static void FireCrossingMarkers(
        IActionContext context,
        ActionDataSO action,
        float prevNt,
        float nextNt,
        ActionTimelinePlaybackState state)
    {
        if (action.TimelineMarkers == null || action.TimelineMarkers.Count == 0)
        {
            return;
        }

        s_markerScratch.Clear();
        ActionTimelineSampler.AppendMarkersCrossed(action.TimelineMarkers, prevNt, nextNt, s_markerScratch);

        for (var i = 0; i < action.TimelineMarkers.Count; i++)
        {
            var m = action.TimelineMarkers[i];
            var crossed = false;
            for (var k = 0; k < s_markerScratch.Count; k++)
            {
                if (s_markerScratch[k].Kind == m.Kind
                    && Mathf.Abs(s_markerScratch[k].NormalizedTime - m.NormalizedTime) < 1e-4f)
                {
                    crossed = true;
                    break;
                }
            }

            if (!crossed || !state.TryFireMarkerOnce(i))
            {
                continue;
            }

            DispatchMarker(context, in m);
        }
    }

    static void FireCrossingWindowEvents(
        IActionContext context,
        ActionDataSO action,
        float prevNt,
        float nextNt,
        ActionTimelinePlaybackState state)
    {
        if (action.Windows == null)
        {
            return;
        }

        for (var wi = 0; wi < action.Windows.Count; wi++)
        {
            var w = action.Windows[wi];
            if (w.RuntimeEvents == null)
            {
                continue;
            }

            var s = Mathf.Min(w.NormalizedStart, w.NormalizedEnd);
            var e = Mathf.Max(w.NormalizedStart, w.NormalizedEnd);
            for (var ei = 0; ei < w.RuntimeEvents.Count; ei++)
            {
                var ev = w.RuntimeEvents[ei];
                if (ev.Kind == ActionWindowRuntimeEventKind.None)
                {
                    continue;
                }

                var key = wi * 1000 + ei;
                var t = Mathf.Lerp(s, e, Mathf.Clamp01(ev.NormalizedOffset));
                if (!ActionTimelineSampler.Crossed(prevNt, nextNt, t) || !state.TryFireWindowEventOnce(key))
                {
                    continue;
                }

                DispatchWindowEvent(context, in ev);
            }
        }
    }

    static void FireCrossingTeleports(
        IActionContext context,
        ActionDataSO action,
        float prevNt,
        float nextNt,
        Vector3 planarForward,
        ActionTimelinePlaybackState state)
    {
        if (action.TeleportTriggers == null)
        {
            return;
        }

        s_teleportScratch.Clear();
        ActionTimelineSampler.AppendTeleportsCrossed(action.TeleportTriggers, prevNt, nextNt, s_teleportScratch);

        for (var i = 0; i < action.TeleportTriggers.Count; i++)
        {
            var crossed = false;
            for (var k = 0; k < s_teleportScratch.Count; k++)
            {
                if (Mathf.Abs(s_teleportScratch[k].TriggerTime - action.TeleportTriggers[i].TriggerTime) < 1e-4f)
                {
                    crossed = true;
                    break;
                }
            }

            if (!crossed || !state.TryFireTeleportOnce(i))
            {
                continue;
            }

            var dist = action.TeleportTriggers[i].Distance;
            var fwd = planarForward.sqrMagnitude > 0.0001f ? planarForward.normalized : context.Transform.forward;
            context.Transform.position += fwd * dist;
            context.PublishTeleported(context.Transform.position);
        }
    }

    static void UpdateZones(ActionDataSO action, float nt, ActionTimelinePlaybackState state)
    {
        var camera = ResolveCamera();
        var timeScale = ActionTimeScaleDriver.Instance;
        var lockActive = false;
        var zoneScale = 1f;
        var zoneFound = false;

        if (action.TimelineMarkers != null)
        {
            for (var i = 0; i < action.TimelineMarkers.Count; i++)
            {
                var m = action.TimelineMarkers[i];
                if (!ActionTimelineMarkerKinds.IsZone(m.Kind))
                {
                    continue;
                }

                if (!ActionTimelineSampler.IsZoneActive(in m, nt))
                {
                    continue;
                }

                zoneFound = true;
                switch (m.Kind)
                {
                    case ActionTimelineMarkerKind.CameraLock:
                        lockActive = true;
                        break;
                    case ActionTimelineMarkerKind.TimeScaleSlowMo:
                    case ActionTimelineMarkerKind.TimeScaleBulletTime:
                        zoneScale = m.Intensity > 0.01f ? m.Intensity : 0.35f;
                        break;
                }
            }
        }

        if (lockActive != state.CameraLockActive)
        {
            camera?.SetLookInputLocked(lockActive);
            state.CameraLockActive = lockActive;
        }

        if (zoneFound)
        {
            if (!state.TimeScaleZoneActive)
            {
                timeScale?.PushZoneScale(zoneScale);
                state.TimeScaleZoneActive = true;
            }
        }
        else if (state.TimeScaleZoneActive)
        {
            timeScale?.PopZoneScale();
            state.TimeScaleZoneActive = false;
        }
    }

    static void DispatchMarker(IActionContext context, in ActionTimelineMarker marker)
    {
        switch (marker.Kind)
        {
            case ActionTimelineMarkerKind.PlaySfx:
            case ActionTimelineMarkerKind.SpawnVfx:
            case ActionTimelineMarkerKind.HitFrame:
                context.PublishActionPresentation(marker.Kind, marker.Payload);
                break;
            case ActionTimelineMarkerKind.CameraShake:
                ResolveCamera()?.AddImpulseShake(
                    marker.Intensity > 0.01f ? marker.Intensity : 0.6f,
                    marker.Duration > 0.01f ? marker.Duration : 0.12f);
                break;
            case ActionTimelineMarkerKind.CameraPush:
                ResolveCamera()?.AddImpulsePush(
                    marker.Intensity != 0f ? marker.Intensity : 4f,
                    marker.Duration > 0.01f ? marker.Duration : 0.2f);
                break;
            case ActionTimelineMarkerKind.TimeScaleHitStop:
                ActionTimeScaleDriver.Instance?.RequestHitStop(
                    marker.Duration > 0.01f ? marker.Duration : 0.06f);
                break;
        }
    }

    static void DispatchWindowEvent(IActionContext context, in ActionWindowEvent ev)
    {
        switch (ev.Kind)
        {
            case ActionWindowRuntimeEventKind.PlaySfx:
            case ActionWindowRuntimeEventKind.SpawnVfx:
            case ActionWindowRuntimeEventKind.HitFrame:
                context.PublishActionPresentation((ActionTimelineMarkerKind)ev.Kind, ev.Payload);
                break;
        }
    }

    static ActionCameraController ResolveCamera()
    {
        if (GameModeManager.Instance == null)
        {
            return null;
        }

        return GameModeManager.Instance.ActiveCameraController as ActionCameraController;
    }
}

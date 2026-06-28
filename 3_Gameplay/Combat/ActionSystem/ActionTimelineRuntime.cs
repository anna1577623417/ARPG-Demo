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

    public static void Tick(
        Player player,
        ActionDataSO action,
        float prevNormalized,
        float nextNormalized,
        Vector3 planarForward,
        ActionTimelinePlaybackState state)
    {
        if (player == null || action == null || state == null)
        {
            return;
        }

        FireCrossingMarkers(player, action, prevNormalized, nextNormalized, state);
        FireCrossingWindowEvents(player, action, prevNormalized, nextNormalized, state);
        FireCrossingCombatEvents(player, action, prevNormalized, nextNormalized, state); // 188.3 W9
        FireCrossingTeleports(player, action, prevNormalized, nextNormalized, planarForward, state);
        UpdateZones(action, nextNormalized, state);
    }

    /// <summary>188.3 W9 — Combat Track 时间轴穿越触发 CombatObjectSpawner.Spawn。</summary>
    static void FireCrossingCombatEvents(
        Player player,
        ActionDataSO action,
        float prevNt,
        float nextNt,
        ActionTimelinePlaybackState state)
    {
        if (action.CombatTrack == null || action.CombatTrack.Length == 0) return;
        var spawner = player != null ? player.CombatObjectSpawner : null;
        if (spawner == null) return; // Player 还未注入 Spawner（W9 接入前）

        for (var i = 0; i < action.CombatTrack.Length; i++)
        {
            ref var ev = ref action.CombatTrack[i];
            if (ev.Definition == null) continue;
            if (!ActionTimelineSampler.Crossed(prevNt, nextNt, ev.NormalizedTime)) continue;
            if (!state.TryFireCombatEventOnce(i)) continue;

            CombatSpawnResolver.Resolve(player, ev.Definition, in ev, out var pos, out var rot);
            spawner.Spawn(ev.Definition, player, pos, rot);
        }
    }

    static void FireCrossingMarkers(
        Player player,
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

            DispatchMarker(player, in m);
        }
    }

    static void FireCrossingWindowEvents(
        Player player,
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

                DispatchWindowEvent(player, in ev);
            }
        }
    }

    static void FireCrossingTeleports(
        Player player,
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
            var fwd = planarForward.sqrMagnitude > 0.0001f ? planarForward.normalized : player.transform.forward;
            player.transform.position += fwd * dist;
            player.PublishEvent(new PlayerTeleportedEvent(
                player.GetInstanceID(),
                player.name,
                player.transform.position));
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

    static void DispatchMarker(Player player, in ActionTimelineMarker marker)
    {
        switch (marker.Kind)
        {
            case ActionTimelineMarkerKind.PlaySfx:
            case ActionTimelineMarkerKind.SpawnVfx:
            case ActionTimelineMarkerKind.HitFrame:
                player.PublishEvent(new ActionTimelinePresentationEvent(
                    player.GetInstanceID(),
                    marker.Kind,
                    marker.Payload));
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

    static void DispatchWindowEvent(Player player, in ActionWindowEvent ev)
    {
        switch (ev.Kind)
        {
            case ActionWindowRuntimeEventKind.PlaySfx:
            case ActionWindowRuntimeEventKind.SpawnVfx:
            case ActionWindowRuntimeEventKind.HitFrame:
                player.PublishEvent(new ActionTimelinePresentationEvent(
                    player.GetInstanceID(),
                    (ActionTimelineMarkerKind)ev.Kind,
                    ev.Payload));
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

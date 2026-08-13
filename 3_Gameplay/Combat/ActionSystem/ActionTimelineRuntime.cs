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
        IActionContext context,
        ActionDataSO action,
        float prevNormalized,
        float nextNormalized,
        Vector3 planarForward,
        ActionTimelinePlaybackState state,
        uint actionLeaseVersion = 0u)
    {
        if (context == null || context.Entity == null || action == null || state == null)
        {
            return;
        }

        FireCrossingMarkers(context, action, prevNormalized, nextNormalized, state);
        FireCrossingWindowEvents(context, action, prevNormalized, nextNormalized, state);
        FireCrossingCombatEvents(
            context,
            action,
            prevNormalized,
            nextNormalized,
            state,
            actionLeaseVersion);
        FirePrimaryAttackTrack(context, action, nextNormalized, state, actionLeaseVersion);
        FireDefenseClips(context, action, nextNormalized, state);                         // 216.3 M5 L1
        FireCrossingTeleports(context, action, prevNormalized, nextNormalized, planarForward, state);
        UpdateZones(action, nextNormalized, state);
    }

    static void FirePrimaryAttackTrack(
        IActionContext context,
        ActionDataSO action,
        float nextNormalized,
        ActionTimelinePlaybackState state,
        uint actionLeaseVersion)
    {
        switch (ActionAttackTrackRuntimePolicy.Select(action))
        {
            case ActionAttackTrackKind.Contact:
                FireContactEvents(
                    context,
                    action,
                    nextNormalized,
                    state,
                    actionLeaseVersion);
                break;

        }
    }

    /// <summary>ContactEvents 是 Action 攻击单轨；Active 区间驱动 ContactRuntime。</summary>
    static void FireContactEvents(
        IActionContext context,
        ActionDataSO action,
        float nextNormalized,
        ActionTimelinePlaybackState state,
        uint actionLeaseVersion)
    {
        var events = action.ContactEvents;
        if (events == null || events.Count == 0)
        {
            return;
        }

        for (var i = 0; i < events.Count; i++)
        {
            var contactEvent = events[i];
            var runtimeId = ContactEventId.IsValid(contactEvent.EventId)
                ? contactEvent.EventId
                : $"invalid:{i}";

            if (!ActionWindowResolver.TryResolveContactWindow(
                    action,
                    in contactEvent,
                    out var resolvedWindow,
                    out var windowInfo))
            {
                if (ContactEventId.IsValid(contactEvent.EventId) && state.RejectContactOnce(runtimeId))
                {
                    Debug.LogError(
                        $"[Contact] REJECT action={action.name} eventId={runtimeId} " +
                        $"reason=WindowResolve:{windowInfo.Message}",
                        action);
                }

                continue;
            }

            var start = resolvedWindow.NormalizedStart;
            var end = resolvedWindow.NormalizedEnd;
            var inside = nextNormalized >= start && nextNormalized <= end;

            if (!inside)
            {
                if (ContactEventId.IsValid(contactEvent.EventId))
                {
                    if (state.TryGetContact(runtimeId, out var inactive) && inactive.Active)
                    {
                        inactive.End();
                    }
                }

                continue;
            }

            if (!ContactEventId.IsValid(contactEvent.EventId))
            {
                if (state.RejectContactOnce(runtimeId))
                {
                    Debug.LogError(
                        $"[Contact] REJECT action={action.name} index={i} reason=InvalidEventId",
                        action);
                }

                continue;
            }

            if (state.IsContactRejected(runtimeId))
            {
                continue;
            }

            var instance = state.GetOrCreateContact(runtimeId);
            if (!instance.Active)
            {
                if (!CombatObjectSpecResolver.TryResolveContact(
                        contactEvent.Definition,
                        in contactEvent.Override,
                        out var spec,
                        out var validation))
                {
                    state.RejectContactOnce(runtimeId);
                    Debug.LogError(
                        $"[Contact] REJECT action={action.name} eventId={runtimeId} " +
                        $"reason={validation.FirstErrorOrNull()}",
                        action);
                    continue;
                }

                var windowStartAnchor = ContactOriginResolver.ResolveAnchor(context.Entity, in spec);
                var beginPose = ContactPoseResolver.ResolveForBegin(
                    in spec,
                    in windowStartAnchor,
                    start);
                instance.BeginContact(
                    in spec,
                    context.Entity,
                    action,
                    runtimeId,
                    contactEvent.DebugName,
                    actionLeaseVersion,
                    in beginPose);
            }

            var activeSpec = instance.ContactRuntime.Spec;
            ResolvedContactPose tickPose;
            if (instance.ContactRuntime.HasFrozenPose)
            {
                tickPose = instance.ContactRuntime.FrozenPose;
            }
            else
            {
                var currentAnchor = ContactOriginResolver.ResolveAnchor(context.Entity, in activeSpec);
                tickPose = ContactPoseResolver.ResolveForTick(
                    in activeSpec,
                    null,
                    in currentAnchor,
                    nextNormalized);
            }

            instance.TickContact(in tickPose);
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

    /// <summary>223.4-5 — Combat Track 只提交值类型请求，不持有或 Tick Spawned Runtime。</summary>
    static void FireCrossingCombatEvents(
        IActionContext context,
        ActionDataSO action,
        float prevNt,
        float nextNt,
        ActionTimelinePlaybackState state,
        uint actionLeaseVersion)
    {
        if (action.CombatTrack == null || action.CombatTrack.Length == 0) return;
        var spawnPort = context.CombatSpawnPort;
        if (spawnPort == null) return;

        for (var i = 0; i < action.CombatTrack.Length; i++)
        {
            ref var ev = ref action.CombatTrack[i];
            if (ev.Definition == null) continue;
            if (!ActionTimelineSampler.Crossed(prevNt, nextNt, ev.NormalizedTime)) continue;
            if (!state.TryFireCombatEventOnce(i)) continue;

            CombatSpawnResolver.Resolve(context.Entity, ev.Definition, in ev, out var pos, out var rot);
            var lineage = default(SpawnLineageContext);
            var eventId = $"combat:{i}:{ev.DebugLabel}";
            var useV2Spawned = ev.Definition.SchemaVersion >= CombatObjectSchemaVersion.ArchetypeV2
                && ev.Definition.Archetype != CombatObjectArchetype.ActionContact
                && ev.Definition.SpawnedData.UseExplicitData;
            var placementSource = ev.OverrideSpawn
                ? ev.SpawnSourceOverride
                : useV2Spawned
                    ? ev.Definition.SpawnedData.Origin
                    : ev.Definition.SpawnSource;
            var request = new CombatSpawnRequest(
                ev.Definition,
                context.Entity,
                null,
                placementSource,
                pos,
                rot,
                context.Transform.forward,
                action,
                eventId,
                actionLeaseVersion,
                in lineage,
                CombatSpawnCause.ActionTimeline,
                ev.DebugLabel);
            var result = spawnPort.Submit(in request);
            if (!result.Accepted)
            {
                Debug.LogWarning(
                    $"[SpawnedCombat] REJECT action={action.name} event={eventId} " +
                    $"code={result.RejectCode} reason={result.Message}",
                    action);
            }
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

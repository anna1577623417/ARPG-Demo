#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 214.3 — Editor 侧 CombatObject 预览演算（与 Runtime Movement 公式对齐）。
/// </summary>
public static class CombatHitPreviewResolver
{
    const int TrajectorySamples = 24;

    public readonly struct PreviewPose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly float ExpandedRadius;
        public readonly bool UseExpandedSphere;

        public PreviewPose(Vector3 position, Quaternion rotation, float expandedRadius, bool useExpandedSphere)
        {
            Position = position;
            Rotation = rotation;
            ExpandedRadius = expandedRadius;
            UseExpandedSphere = useExpandedSphere;
        }
    }

    public static float ResolveActionDurationSeconds(ActionDataSO action)
    {
        if (action == null)
        {
            return 1f;
        }

        return Mathf.Max(0.01f, action.Duration);
    }

    public static float NormalizedOffsetToElapsedSeconds(ActionDataSO action, float spawnNt, float currentNt)
    {
        var duration = ResolveActionDurationSeconds(action);
        return Mathf.Max(0f, currentNt - spawnNt) * duration;
    }

    public static (Vector3 pos, Quaternion rot) ResolveSpawn(
        Transform anchor,
        in CombatEvent ev,
        CombatObjectDefinitionSO def)
    {
        if (def == null)
        {
            return (Vector3.zero, Quaternion.identity);
        }

        var src = ev.OverrideSpawn ? ev.SpawnSourceOverride : def.SpawnSource;
        var localOffset = ev.OverrideSpawn ? ev.LocalOffsetOverride : def.LocalOffset;
        var localEuler = ev.OverrideSpawn ? ev.LocalEulerOffsetOverride : def.LocalEulerOffset;

        if (anchor != null && CombatHitPreviewRig.TryResolveSpawn(anchor, src, localOffset, localEuler, out var pos, out var rot))
        {
            return (pos, rot);
        }

        var anchorPos = anchor != null ? anchor.position : Vector3.zero;
        var anchorRot = anchor != null ? anchor.rotation : Quaternion.identity;
        return (anchorPos + anchorRot * localOffset, anchorRot * Quaternion.Euler(localEuler));
    }

    public static PreviewPose Sample(
        ActionDataSO action,
        in CombatEvent ev,
        float currentNt,
        Transform anchor)
    {
        if (ev.Definition == null)
        {
            return default;
        }

        var def = ev.Definition;
        var elapsed = NormalizedOffsetToElapsedSeconds(action, ev.NormalizedTime, currentNt);
        var (spawnPos, spawnRot) = ResolveSpawn(anchor, in ev, def);
        var pos = SimulatePosition(def, spawnPos, spawnRot, elapsed);
        var expanded = def.Movement.Kind == MovementKind.Expand
            ? ResolveExpandedRadius(def, elapsed)
            : 0f;
        return new PreviewPose(pos, spawnRot, expanded, def.Movement.Kind == MovementKind.Expand);
    }

    public static Vector3 SimulatePosition(
        CombatObjectDefinitionSO def,
        Vector3 spawnPos,
        Quaternion spawnRot,
        float elapsedSec)
    {
        if (def == null)
        {
            return spawnPos;
        }

        var mov = def.Movement;
        switch (mov.Kind)
        {
            case MovementKind.Static:
            case MovementKind.Expand:
                return spawnPos;

            case MovementKind.Linear:
            {
                var dir = spawnRot * Vector3.forward;
                var dist = mov.Speed * elapsedSec;
                if (mov.MaxDistance > 0f)
                {
                    dist = Mathf.Min(dist, mov.MaxDistance);
                }

                return spawnPos + dir * dist;
            }

            case MovementKind.Curve:
            {
                var x = mov.LocalOffsetXOverTime != null ? mov.LocalOffsetXOverTime.Evaluate(elapsedSec) : 0f;
                var y = mov.LocalOffsetYOverTime != null ? mov.LocalOffsetYOverTime.Evaluate(elapsedSec) : 0f;
                var z = mov.LocalOffsetZOverTime != null ? mov.LocalOffsetZOverTime.Evaluate(elapsedSec) : 0f;
                return spawnPos + spawnRot * new Vector3(x, y, z);
            }

            case MovementKind.Homing:
            {
                var dir = spawnRot * Vector3.forward;
                var dist = mov.Speed * elapsedSec;
                if (mov.MaxDistance > 0f)
                {
                    dist = Mathf.Min(dist, mov.MaxDistance);
                }

                return spawnPos + dir * dist;
            }

            default:
                return spawnPos;
        }
    }

    public static float ResolveExpandedRadius(CombatObjectDefinitionSO def, float elapsedSec)
    {
        if (def == null)
        {
            return 0f;
        }

        var mov = def.Movement;
        var duration = Mathf.Max(0.0001f, def.Lifecycle.Duration);
        var t = Mathf.Clamp01(elapsedSec / duration);
        if (mov.ExpandCurve != null && mov.ExpandCurve.length > 0)
        {
            t = Mathf.Clamp01(mov.ExpandCurve.Evaluate(t));
        }

        return Mathf.Lerp(mov.StartRadius, mov.EndRadius, t);
    }

    public static void DrawShapeGizmo(
        HitShapeSO shape,
        in PreviewPose pose,
        Color color)
    {
        if (shape == null)
        {
            return;
        }

        if (pose.UseExpandedSphere && pose.ExpandedRadius > 0f)
        {
            var prev = Handles.color;
            Handles.color = color;
            DrawWireSphere(pose.Position, pose.ExpandedRadius);
            Handles.color = prev;
            return;
        }

        HitShapeGizmoPreview.DrawShapeHandles(shape, pose.Position, pose.Rotation, color);
    }

    public static void DrawTrajectory(
        ActionDataSO action,
        in CombatEvent ev,
        Transform anchor,
        Color color)
    {
        if (ev.Definition == null || anchor == null)
        {
            return;
        }

        var def = ev.Definition;
        var mov = def.Movement;
        if (mov.Kind != MovementKind.Curve
            && mov.Kind != MovementKind.Linear
            && mov.Kind != MovementKind.Homing)
        {
            return;
        }

        var duration = Mathf.Max(0.0001f, def.Lifecycle.Duration > 0f ? def.Lifecycle.Duration : 0.12f);
        var (spawnPos, spawnRot) = ResolveSpawn(anchor, in ev, def);
        var points = new Vector3[TrajectorySamples + 1];
        for (var i = 0; i <= TrajectorySamples; i++)
        {
            var t = duration * i / TrajectorySamples;
            points[i] = SimulatePosition(def, spawnPos, spawnRot, t);
        }

        Handles.color = color;
        Handles.DrawAAPolyLine(3f, points);
    }

    public static void DrawExpandRings(
        ActionDataSO action,
        in CombatEvent ev,
        Transform anchor)
    {
        if (ev.Definition == null || ev.Definition.Movement.Kind != MovementKind.Expand)
        {
            return;
        }

        var def = ev.Definition;
        var (spawnPos, _) = ResolveSpawn(anchor, in ev, def);
        var startR = def.Movement.StartRadius;
        var endR = def.Movement.EndRadius;
        if (startR > 0f)
        {
            Handles.color = new Color(0.2f, 0.95f, 0.35f, 0.9f);
            DrawWireSphere(spawnPos, startR);
        }

        if (endR > 0f)
        {
            Handles.color = new Color(0.95f, 0.25f, 0.2f, 0.9f);
            DrawWireSphere(spawnPos, endR);
        }
    }

    public static void DrawAllForAction(
        ActionDataSO action,
        float normalizedTime,
        Transform anchor,
        bool drawTrajectory,
        bool drawExpandRings)
    {
        if (action?.CombatTrack == null || anchor == null)
        {
            return;
        }

        for (var i = 0; i < action.CombatTrack.Length; i++)
        {
            ref var ev = ref action.CombatTrack[i];
            if (ev.Definition == null || ev.Definition.Shape == null)
            {
                continue;
            }

            if (drawTrajectory)
            {
                DrawTrajectory(action, in ev, anchor, new Color(1f, 0.85f, 0.15f, 0.95f));
            }

            if (drawExpandRings)
            {
                DrawExpandRings(action, in ev, anchor);
            }

            if (normalizedTime < ev.NormalizedTime)
            {
                continue;
            }

            var pose = Sample(action, in ev, normalizedTime, anchor);
            DrawShapeGizmo(ev.Definition.Shape, in pose, new Color(1f, 0.3f, 0.3f, 1f));
        }
    }

    /// <summary>
    /// 216.3 M6 L1 — 解析 HitClip 在归一化时间 nt 的世界位姿（Origin + MotionProfile 位移增量）。
    /// </summary>
    public static bool TryResolveHitClipWorldPose(
        ActionDataSO action,
        in HitClip clip,
        Transform anchor,
        Vector3 planarForward,
        Vector3 anchorOrigin,
        float normalizedTime,
        out Vector3 worldPos,
        out Quaternion worldRot)
    {
        worldPos = default;
        worldRot = Quaternion.identity;
        if (anchor == null)
        {
            return false;
        }

        if (!CombatHitPreviewRig.TryResolveSpawn(
                anchor,
                clip.Origin,
                clip.OriginOffset,
                clip.OriginEuler,
                out worldPos,
                out worldRot))
        {
            worldPos = anchor.position + anchor.rotation * clip.OriginOffset;
            worldRot = anchor.rotation * Quaternion.Euler(clip.OriginEuler);
        }

        // 用 MotionProfile 在 nt 相对 t=0 的位移，把静态骨骼预览拖成覆盖轨迹。
        if (action != null && action.MotionProfile != null && action.MotionProfile.UsesAxisCurves)
        {
            var root0 = PreviewMotionDriver.EvaluateWorldPosition(
                action.MotionProfile, 0f, anchorOrigin, planarForward);
            var rootT = PreviewMotionDriver.EvaluateWorldPosition(
                action.MotionProfile, Mathf.Clamp01(normalizedTime), anchorOrigin, planarForward);
            worldPos += rootT - root0;
        }

        return true;
    }

    /// <summary>
    /// 216.3 M1 L3 — 当 playhead 落入 HitClip.Active 区间时，按 Origin 解析世界位姿并画 Shape。
    /// </summary>
    public static void DrawAttackClipsForAction(
        ActionDataSO action,
        float normalizedTime,
        Transform anchor)
    {
        DrawAttackClipsForAction(action, normalizedTime, anchor, anchor != null ? anchor.position : Vector3.zero, Vector3.forward);
    }

    /// <summary>216.3 M6 — 带 Motion 前向的 Active Shape 预览。</summary>
    public static void DrawAttackClipsForAction(
        ActionDataSO action,
        float normalizedTime,
        Transform anchor,
        Vector3 anchorOrigin,
        Vector3 planarForward)
    {
        if (action?.AttackClips == null || anchor == null)
        {
            return;
        }

        var clips = action.AttackClips;
        for (var i = 0; i < clips.Count; i++)
        {
            var clip = clips[i];
            var start = Mathf.Min(clip.ActiveStart, clip.ActiveEnd);
            var end = Mathf.Max(clip.ActiveStart, clip.ActiveEnd);
            if (normalizedTime < start || normalizedTime > end)
            {
                continue;
            }

            if (!TryResolveHitClipWorldPose(
                    action, in clip, anchor, planarForward, anchorOrigin, normalizedTime,
                    out var pos, out var rot))
            {
                continue;
            }

            var color = new Color(0.98f, 0.35f, 0.28f, 1f);
            if (clip.ShapeMode == HitShapeMode.WeaponTrace && clip.WeaponSockets != null && clip.WeaponSockets.Count > 0)
            {
                if (CoverageDrawer.TrySampleSockets(
                        action, in clip, anchor, planarForward, anchorOrigin, normalizedTime,
                        out var tip, out var radius))
                {
                    Handles.color = color;
                    DrawWireSphere(tip, radius);
                    Handles.DrawDottedLine(pos, tip, 3f);
                }
            }
            else if (clip.Shape != null)
            {
                HitShapeGizmoPreview.DrawShapeHandles(clip.Shape, pos, rot, color);
            }
            else
            {
                continue;
            }

            var label = string.IsNullOrEmpty(clip.DebugName) ? $"HitClip#{i}" : clip.DebugName;
            Handles.color = color;
            Handles.Label(pos + Vector3.up * 0.15f, label);

            if (clip.Reach > 0.01f)
            {
                var tip = pos + rot * Vector3.forward * clip.Reach;
                Handles.DrawDottedLine(pos, tip, 3f);
                Handles.ArrowHandleCap(0, tip, rot, 0.18f, EventType.Repaint);
            }
        }
    }

    static void DrawWireSphere(Vector3 center, float radius)
    {
        Handles.DrawWireDisc(center, Vector3.up, radius);
        Handles.DrawWireDisc(center, Vector3.right, radius);
        Handles.DrawWireDisc(center, Vector3.forward, radius);
    }
}
#endif

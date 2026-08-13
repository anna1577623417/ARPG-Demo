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

    static void DrawWireSphere(Vector3 center, float radius)
    {
        Handles.DrawWireDisc(center, Vector3.up, radius);
        Handles.DrawWireDisc(center, Vector3.right, radius);
        Handles.DrawWireDisc(center, Vector3.forward, radius);
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 188.3 W2 — Scene 预览 HitShape Gizmo 的静态桥接器。
/// <para>由 ActionDataSO Inspector / HitShapeSO Inspector 设置当前预览目标，
/// SceneView 重绘时统一调 HitShapeSO.DrawGizmo。</para>
/// </summary>
[InitializeOnLoad]
public static class HitShapeGizmoPreview
{
    public static HitShapeSO Shape;
    public static Vector3 OriginWorld;
    public static Quaternion RotationWorld = Quaternion.identity;
    public static Color Color = new Color(1f, 0.25f, 0.25f, 1f);
    public static bool FollowSelection;

    static HitShapeGizmoPreview()
    {
        SceneView.duringSceneGui += OnScene;
    }

    public static void Clear()
    {
        Shape = null;
        SceneView.RepaintAll();
    }

    public static void SetTarget(HitShapeSO shape, Transform follow = null)
    {
        Shape = shape;
        if (follow != null)
        {
            OriginWorld = follow.position;
            RotationWorld = follow.rotation;
            FollowSelection = true;
        }
        SceneView.RepaintAll();
    }

    static void OnScene(SceneView view)
    {
        if (Shape == null) return;
        if (!CombatSceneEditCoordinator.AllowsHitShapeGizmoOwner()) return;
        if (FollowSelection && Selection.activeTransform != null)
        {
            OriginWorld = Selection.activeTransform.position;
            RotationWorld = Selection.activeTransform.rotation;
        }
        DrawShapeHandles(Shape, OriginWorld, RotationWorld, Color);
        CombatSceneDrawSourceProbe.RegisterPrimaryDraw(
            CombatSceneDrawSourceProbe.SourceHitShapeGizmo,
            OriginWorld,
            $"shape={Shape.name} follow={FollowSelection}");
    }

    public static void DrawShapeHandles(HitShapeSO shape, Vector3 origin, Quaternion rotation, Color color)
    {
        if (shape == null)
        {
            return;
        }

        var prevColor = Handles.color;
        Handles.color = color;
        DrawWithHandles(shape, origin, rotation);
        Handles.color = prevColor;
    }

    static void DrawWithHandles(HitShapeSO shape, Vector3 origin, Quaternion rot)
    {
        switch (shape)
        {
            case SphereShapeSO sph:
                if (sph.radius > 0f)
                {
                    Handles.DrawWireDisc(origin, Vector3.up, sph.radius);
                    Handles.DrawWireDisc(origin, Vector3.right, sph.radius);
                    Handles.DrawWireDisc(origin, Vector3.forward, sph.radius);
                }
                break;
            case BoxShapeSO box:
                {
                    var center = origin + rot * box.offset;
                    var prev = Handles.matrix;
                    Handles.matrix = Matrix4x4.TRS(center, rot, Vector3.one);
                    Handles.DrawWireCube(Vector3.zero, box.halfExtents * 2f);
                    Handles.matrix = prev;
                }
                break;
            case CapsuleShapeSO cap:
                if (cap.radius > 0f && cap.height > 0f)
                {
                    var axis = rot * Vector3.up;
                    var half = Mathf.Max(cap.height * 0.5f - cap.radius, 0f);
                    var center = origin + rot * cap.offset;
                    var p0 = center + axis * half;
                    var p1 = center - axis * half;
                    Handles.DrawWireDisc(p0, axis, cap.radius);
                    Handles.DrawWireDisc(p1, axis, cap.radius);
                    var right = rot * Vector3.right * cap.radius;
                    var fwd = rot * Vector3.forward * cap.radius;
                    Handles.DrawLine(p0 + right, p1 + right);
                    Handles.DrawLine(p0 - right, p1 - right);
                    Handles.DrawLine(p0 + fwd, p1 + fwd);
                    Handles.DrawLine(p0 - fwd, p1 - fwd);
                }
                break;
            case ConeShapeSO cone:
                if (cone.length > 0f && cone.angleDegrees > 0f)
                {
                    var basePos = origin + rot * cone.offset;
                    var forward = rot * Vector3.forward;
                    var fromDir = Quaternion.AngleAxis(-cone.angleDegrees, Vector3.up) * forward;
                    Handles.DrawWireArc(basePos, Vector3.up, fromDir, cone.angleDegrees * 2f, cone.length);
                    var leftEdge = Quaternion.AngleAxis(-cone.angleDegrees, Vector3.up) * forward;
                    var rightEdge = Quaternion.AngleAxis(cone.angleDegrees, Vector3.up) * forward;
                    Handles.DrawLine(basePos, basePos + leftEdge * cone.length);
                    Handles.DrawLine(basePos, basePos + rightEdge * cone.length);
                }
                break;
            case RingShapeSO ring:
                if (ring.outerRadius > 0f)
                {
                    var center = origin + rot * ring.offset;
                    Handles.DrawWireDisc(center, Vector3.up, ring.outerRadius);
                    if (ring.innerRadius > 0f && ring.innerRadius < ring.outerRadius)
                    {
                        Handles.DrawWireDisc(center, Vector3.up, ring.innerRadius);
                    }
                }
                break;
            case BeamShapeSO beam:
                if (beam.length > 0f && beam.width > 0f)
                {
                    var fwd = rot * Vector3.forward;
                    var center = origin + rot * beam.offset + fwd * (beam.length * 0.5f);
                    var prev = Handles.matrix;
                    Handles.matrix = Matrix4x4.TRS(center, rot, Vector3.one);
                    Handles.DrawWireCube(Vector3.zero, new Vector3(beam.width, beam.height, beam.length));
                    Handles.matrix = prev;
                }
                break;
            default:
                Handles.DrawWireDisc(origin, Vector3.up, 0.5f);
                break;
        }
    }
}
#endif

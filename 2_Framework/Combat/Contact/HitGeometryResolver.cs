using UnityEngine;

/// <summary>
/// 224.1 L6 — Shape + Contact Pose → ResolvedHitGeometry。
/// L6 首切片：识别 Kind/Dimensions/LegacyOffset；Pivot 默认 Center，专用 Pivot 合同后续补齐。
/// </summary>
public static class HitGeometryResolver
{
    public static bool TryResolve(
        HitShapeSO shape,
        in ResolvedContactPose pose,
        out ResolvedHitGeometry geometry)
    {
        geometry = default;
        if (shape == null) return false;

        switch (shape)
        {
            case SphereShapeSO sphere:
                geometry = new ResolvedHitGeometry(
                    HitShapeKind.Sphere,
                    pose.Position,
                    pose.Rotation,
                    HitShapePivotFeature.Center,
                    HitShapeAxis.LocalY,
                    new HitShapeDimensions(radius: sphere.radius),
                    0,
                    Vector3.zero);
                return true;
            case BoxShapeSO box:
            {
                var center = pose.Position + pose.Rotation * box.offset;
                geometry = new ResolvedHitGeometry(
                    HitShapeKind.Box,
                    center,
                    pose.Rotation,
                    HitShapePivotFeature.Center,
                    HitShapeAxis.LocalY,
                    new HitShapeDimensions(halfExtents: box.halfExtents),
                    0,
                    box.offset);
                return true;
            }
            case CapsuleShapeSO capsule:
            {
                var center = pose.Position + pose.Rotation * capsule.offset;
                geometry = new ResolvedHitGeometry(
                    HitShapeKind.Capsule,
                    center,
                    pose.Rotation,
                    HitShapePivotFeature.Center,
                    HitShapeAxis.LocalY,
                    new HitShapeDimensions(radius: capsule.radius, height: capsule.height),
                    0,
                    capsule.offset);
                return true;
            }
            case ConeShapeSO cone:
            {
                var center = pose.Position + pose.Rotation * cone.offset;
                geometry = new ResolvedHitGeometry(
                    HitShapeKind.Cone,
                    center,
                    pose.Rotation,
                    HitShapePivotFeature.Back,
                    HitShapeAxis.LocalZ,
                    new HitShapeDimensions(length: cone.length, angleDegrees: cone.angleDegrees),
                    0,
                    cone.offset);
                return true;
            }
            case RingShapeSO ring:
            {
                var center = pose.Position + pose.Rotation * ring.offset;
                geometry = new ResolvedHitGeometry(
                    HitShapeKind.Ring,
                    center,
                    pose.Rotation,
                    HitShapePivotFeature.Center,
                    HitShapeAxis.LocalY,
                    new HitShapeDimensions(innerRadius: ring.innerRadius, outerRadius: ring.outerRadius),
                    0,
                    ring.offset);
                return true;
            }
            case BeamShapeSO beam:
            {
                var center = pose.Position + pose.Rotation * beam.offset;
                geometry = new ResolvedHitGeometry(
                    HitShapeKind.Beam,
                    center,
                    pose.Rotation,
                    HitShapePivotFeature.Back,
                    HitShapeAxis.LocalZ,
                    new HitShapeDimensions(
                        width: beam.width,
                        height: beam.height,
                        length: beam.length),
                    0,
                    beam.offset);
                return true;
            }
            default:
                geometry = new ResolvedHitGeometry(
                    HitShapeKind.Unknown,
                    pose.Position,
                    pose.Rotation,
                    HitShapePivotFeature.Center,
                    HitShapeAxis.LocalY,
                    default,
                    0,
                    Vector3.zero);
                return false;
        }
    }
}

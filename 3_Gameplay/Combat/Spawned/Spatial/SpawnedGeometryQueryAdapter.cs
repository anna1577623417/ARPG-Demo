using UnityEngine;

/// <summary>在不修改基础 Shape SO 的前提下应用几何演化；不会把非球形偷换为 Sphere。</summary>
public static class SpawnedGeometryQueryAdapter
{
    public static int Overlap(
        HitShapeSO shape,
        SpawnedGeometryEvolutionKind evolution,
        float scale,
        Vector3 position,
        Quaternion rotation,
        Collider[] results,
        int layerMask)
    {
        if (shape == null || results == null || results.Length == 0)
        {
            return 0;
        }

        var uniform = Mathf.Max(0f, scale);
        if (shape is SphereShapeSO sphere)
        {
            var radius = evolution == SpawnedGeometryEvolutionKind.LegacyExpand
                ? uniform
                : sphere.radius * uniform;
            return radius > 0f
                ? Physics.OverlapSphereNonAlloc(position, radius, results, layerMask)
                : 0;
        }

        if (shape is BoxShapeSO box)
        {
            var center = position + rotation * (box.offset * uniform);
            return Physics.OverlapBoxNonAlloc(
                center,
                box.halfExtents * uniform,
                results,
                rotation,
                layerMask);
        }

        if (shape is CapsuleShapeSO capsule)
        {
            ResolveCapsule(capsule, uniform, position, rotation, out var p0, out var p1, out var radius);
            return radius > 0f
                ? Physics.OverlapCapsuleNonAlloc(p0, p1, radius, results, layerMask)
                : 0;
        }

        // 尚未声明演化能力的复杂形状只能使用基础尺寸；Validator 会阻止其配置 Evolution。
        return shape.Overlap(position, rotation, results, layerMask);
    }

    public static int Sweep(
        HitShapeSO shape,
        SpawnedGeometryEvolutionKind evolution,
        float scale,
        Vector3 from,
        Vector3 to,
        Quaternion rotation,
        RaycastHit[] results,
        int layerMask)
    {
        var delta = to - from;
        var distance = delta.magnitude;
        if (shape == null || results == null || results.Length == 0 || distance < 1e-5f)
        {
            return 0;
        }

        var direction = delta / distance;
        var uniform = Mathf.Max(0f, scale);
        if (shape is SphereShapeSO sphere)
        {
            var radius = evolution == SpawnedGeometryEvolutionKind.LegacyExpand
                ? uniform
                : sphere.radius * uniform;
            return radius > 0f
                ? Physics.SphereCastNonAlloc(
                    from,
                    radius,
                    direction,
                    results,
                    distance,
                    layerMask)
                : 0;
        }

        if (shape is BoxShapeSO box)
        {
            var center = from + rotation * (box.offset * uniform);
            return Physics.BoxCastNonAlloc(
                center,
                box.halfExtents * uniform,
                direction,
                results,
                rotation,
                distance,
                layerMask);
        }

        if (shape is CapsuleShapeSO capsule)
        {
            ResolveCapsule(capsule, uniform, from, rotation, out var p0, out var p1, out var radius);
            return radius > 0f
                ? Physics.CapsuleCastNonAlloc(
                    p0,
                    p1,
                    radius,
                    direction,
                    results,
                    distance,
                    layerMask)
                : 0;
        }

        return 0;
    }

    public static bool SupportsEvolution(HitShapeSO shape) =>
        SpawnedGeometryCapability.SupportsEvolution(shape);

    public static float CharacteristicRadius(HitShapeSO shape, float scale)
    {
        var uniform = Mathf.Max(0.001f, scale);
        if (shape is SphereShapeSO sphere) return Mathf.Max(0.01f, sphere.radius * uniform);
        if (shape is BoxShapeSO box) return Mathf.Max(0.01f, box.halfExtents.magnitude * uniform);
        if (shape is CapsuleShapeSO capsule) return Mathf.Max(0.01f, capsule.radius * uniform);
        return 0.25f;
    }

    static void ResolveCapsule(
        CapsuleShapeSO capsule,
        float scale,
        Vector3 position,
        Quaternion rotation,
        out Vector3 p0,
        out Vector3 p1,
        out float radius)
    {
        radius = capsule.radius * scale;
        var height = capsule.height * scale;
        var axis = rotation * Vector3.up;
        var half = Mathf.Max(height * 0.5f - radius, 0f);
        var center = position + rotation * (capsule.offset * scale);
        p0 = center + axis * half;
        p1 = center - axis * half;
    }
}

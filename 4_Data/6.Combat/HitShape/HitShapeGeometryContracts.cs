using UnityEngine;

public enum HitShapeKind : byte
{
    Unknown = 0,
    Sphere = 1,
    Box = 2,
    Capsule = 3,
    Cone = 4,
    Ring = 5,
    Beam = 6,
    WeaponTrace = 7,
}

public enum HitShapePivotFeature : byte
{
    Center = 0,
    Back = 1,
    Front = 2,
    Bottom = 3,
    Top = 4,
    CustomNormalized = 5,
}

public enum HitShapeAxis : byte
{
    LocalX = 0,
    LocalY = 1,
    LocalZ = 2,
}

public readonly struct HitShapeDimensions
{
    public readonly float Radius;
    public readonly float Height;
    public readonly Vector3 HalfExtents;
    public readonly float Length;
    public readonly float AngleDegrees;
    public readonly float InnerRadius;
    public readonly float OuterRadius;
    public readonly float Width;

    public HitShapeDimensions(
        float radius = 0f,
        float height = 0f,
        Vector3 halfExtents = default,
        float length = 0f,
        float angleDegrees = 0f,
        float innerRadius = 0f,
        float outerRadius = 0f,
        float width = 0f)
    {
        Radius = radius;
        Height = height;
        HalfExtents = halfExtents;
        Length = length;
        AngleDegrees = angleDegrees;
        InnerRadius = innerRadius;
        OuterRadius = outerRadius;
        Width = width;
    }
}

/// <summary>224.1 L6 — Preview/Physics 共用的几何解析结果。</summary>
public readonly struct ResolvedHitGeometry
{
    public readonly HitShapeKind Kind;
    public readonly Vector3 Center;
    public readonly Quaternion Rotation;
    public readonly HitShapePivotFeature Pivot;
    public readonly HitShapeAxis Axis;
    public readonly HitShapeDimensions Dimensions;
    public readonly int ShapeRevision;
    public readonly Vector3 LegacyOffset;

    public ResolvedHitGeometry(
        HitShapeKind kind,
        Vector3 center,
        Quaternion rotation,
        HitShapePivotFeature pivot,
        HitShapeAxis axis,
        in HitShapeDimensions dimensions,
        int shapeRevision,
        Vector3 legacyOffset)
    {
        Kind = kind;
        Center = center;
        Rotation = rotation;
        Pivot = pivot;
        Axis = axis;
        Dimensions = dimensions;
        ShapeRevision = shapeRevision;
        LegacyOffset = legacyOffset;
    }
}

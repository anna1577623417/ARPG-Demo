using System;
using UnityEngine;

public enum SpawnedGuidanceKind : byte
{
    None = 0,
    Target = 1,
}

public enum SpawnedTargetLossPolicy : byte
{
    ContinueHeading = 0,
    Terminate = 1,
}

public enum SpawnedRotationPolicy : byte
{
    SpawnRotation = 0,
    FaceVelocity = 1,
    FaceTarget = 2,
    FollowSource = 3,
}

public enum SpawnedCurveTimeDomain : byte
{
    SecondsSinceSpawn = 0,
    NormalizedLifetime = 1,
    NormalizedTravel = 2,
}

public enum SpawnedTravelMetric : byte
{
    PathLength = 0,
    Displacement = 1,
}

public enum SpawnedTravelLimitResponse : byte
{
    Terminate = 0,
    Stop = 1,
    Clamp = 2,
}

public enum SpawnedGeometryEvolutionKind : byte
{
    None = 0,
    UniformScale = 1,
    LegacyExpand = 2,
}

[Serializable]
public struct SpawnedSpatialPolicyAuthoring
{
    public bool UseExplicitPolicy;
    public SpawnedGuidanceKind Guidance;
    public SpawnedTargetLossPolicy TargetLoss;
    public SpawnedRotationPolicy Rotation;
    public SpawnedCurveTimeDomain CurveTimeDomain;
    public SpawnedTravelMetric TravelMetric;
    public SpawnedTravelLimitResponse TravelLimitResponse;
    public SpawnedGeometryEvolutionKind GeometryEvolution;
    public bool FollowSourcePosition;
    [Min(0f)] public float GeometryStartScale;
    [Min(0f)] public float GeometryEndScale;
    public AnimationCurve GeometryScaleCurve;
}

public readonly struct ResolvedSpawnedSpatialSpec
{
    public readonly MovementKind MotionKind;
    public readonly float Speed;
    public readonly float TravelLimit;
    public readonly float TurnRateDegPerSecond;
    public readonly AnimationCurve CurveX;
    public readonly AnimationCurve CurveY;
    public readonly AnimationCurve CurveZ;
    public readonly SpawnedGuidanceKind Guidance;
    public readonly SpawnedTargetLossPolicy TargetLoss;
    public readonly SpawnedRotationPolicy Rotation;
    public readonly SpawnedCurveTimeDomain CurveTimeDomain;
    public readonly SpawnedTravelMetric TravelMetric;
    public readonly SpawnedTravelLimitResponse TravelLimitResponse;
    public readonly SpawnedGeometryEvolutionKind GeometryEvolution;
    public readonly bool FollowSourcePosition;
    public readonly float GeometryStartScale;
    public readonly float GeometryEndScale;
    public readonly AnimationCurve GeometryScaleCurve;

    public ResolvedSpawnedSpatialSpec(
        MovementKind motionKind,
        float speed,
        float travelLimit,
        float turnRateDegPerSecond,
        AnimationCurve curveX,
        AnimationCurve curveY,
        AnimationCurve curveZ,
        SpawnedGuidanceKind guidance,
        SpawnedTargetLossPolicy targetLoss,
        SpawnedRotationPolicy rotation,
        SpawnedCurveTimeDomain curveTimeDomain,
        SpawnedTravelMetric travelMetric,
        SpawnedTravelLimitResponse travelLimitResponse,
        SpawnedGeometryEvolutionKind geometryEvolution,
        bool followSourcePosition,
        float geometryStartScale,
        float geometryEndScale,
        AnimationCurve geometryScaleCurve)
    {
        MotionKind = motionKind;
        Speed = Mathf.Max(0f, speed);
        TravelLimit = Mathf.Max(0f, travelLimit);
        TurnRateDegPerSecond = Mathf.Max(0f, turnRateDegPerSecond);
        CurveX = CloneCurve(curveX);
        CurveY = CloneCurve(curveY);
        CurveZ = CloneCurve(curveZ);
        Guidance = guidance;
        TargetLoss = targetLoss;
        Rotation = rotation;
        CurveTimeDomain = curveTimeDomain;
        TravelMetric = travelMetric;
        TravelLimitResponse = travelLimitResponse;
        GeometryEvolution = geometryEvolution;
        FollowSourcePosition = followSourcePosition;
        GeometryStartScale = Mathf.Max(0f, geometryStartScale);
        GeometryEndScale = Mathf.Max(0f, geometryEndScale);
        GeometryScaleCurve = CloneCurve(geometryScaleCurve);
    }

    static AnimationCurve CloneCurve(AnimationCurve source)
    {
        if (source == null)
        {
            return null;
        }

        var clone = new AnimationCurve(source.keys)
        {
            preWrapMode = source.preWrapMode,
            postWrapMode = source.postWrapMode,
        };
        return clone;
    }
}

public static class SpawnedSpatialSpecResolver
{
    public static ResolvedSpawnedSpatialSpec Resolve(
        in SpawnedSpatialPolicyAuthoring authoring,
        in MovementParams legacyMovement,
        in ResolvedSpawnedRuntimePolicy lifetime)
    {
        var motion = legacyMovement.Kind == MovementKind.Expand
            ? MovementKind.Static
            : legacyMovement.Kind;
        var guidance = authoring.UseExplicitPolicy
            ? authoring.Guidance
            : legacyMovement.Kind == MovementKind.Homing
                ? SpawnedGuidanceKind.Target
                : SpawnedGuidanceKind.None;
        var targetLoss = authoring.UseExplicitPolicy
            ? authoring.TargetLoss
            : SpawnedTargetLossPolicy.Terminate;
        var rotation = authoring.UseExplicitPolicy
            ? authoring.Rotation
            : motion == MovementKind.Linear || motion == MovementKind.Homing
                ? SpawnedRotationPolicy.FaceVelocity
                : SpawnedRotationPolicy.SpawnRotation;
        var curveDomain = authoring.UseExplicitPolicy
            ? authoring.CurveTimeDomain
            : lifetime.LifetimeKind == SpawnedLifetimeKind.Timed
                ? SpawnedCurveTimeDomain.NormalizedLifetime
                : SpawnedCurveTimeDomain.SecondsSinceSpawn;
        var geometryEvolution = authoring.UseExplicitPolicy
            ? authoring.GeometryEvolution
            : legacyMovement.Kind == MovementKind.Expand
                ? SpawnedGeometryEvolutionKind.LegacyExpand
                : SpawnedGeometryEvolutionKind.None;

        return new ResolvedSpawnedSpatialSpec(
            motion,
            legacyMovement.Speed,
            legacyMovement.MaxDistance,
            legacyMovement.TurnRateDegPerSec,
            legacyMovement.LocalOffsetXOverTime,
            legacyMovement.LocalOffsetYOverTime,
            legacyMovement.LocalOffsetZOverTime,
            guidance,
            targetLoss,
            rotation,
            curveDomain,
            authoring.UseExplicitPolicy
                ? authoring.TravelMetric
                : SpawnedTravelMetric.PathLength,
            authoring.UseExplicitPolicy
                ? authoring.TravelLimitResponse
                : SpawnedTravelLimitResponse.Terminate,
            geometryEvolution,
            authoring.UseExplicitPolicy && authoring.FollowSourcePosition,
            authoring.UseExplicitPolicy
                ? authoring.GeometryStartScale
                : legacyMovement.StartRadius,
            authoring.UseExplicitPolicy
                ? authoring.GeometryEndScale
                : legacyMovement.EndRadius,
            authoring.UseExplicitPolicy
                ? authoring.GeometryScaleCurve
                : legacyMovement.ExpandCurve);
    }
}

public static class SpawnedGeometryCapability
{
    public static bool SupportsEvolution(HitShapeSO shape) =>
        shape is SphereShapeSO || shape is BoxShapeSO || shape is CapsuleShapeSO;
}

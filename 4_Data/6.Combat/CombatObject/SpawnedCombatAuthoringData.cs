using System;
using UnityEngine;

/// <summary>
/// 223.6 M1：Spawned V2 的唯一作者输入集合。
/// SchemaV2 Definition 必须启用它；旧 Shape/Movement/Lifecycle/Damage/TargetFilter
/// 仅可由迁移 Adapter 读取，不能在 V2 Resolver 中作为 fallback。
/// </summary>
[Serializable]
public struct SpawnedCombatAuthoringData
{
    [Tooltip("V2 Definition 必须开启；关闭表示旧资产，不能进入无 fallback 的新入口。")]
    public bool UseExplicitData;

    [Header("Geometry / Outcome")]
    public HitShapeSO Geometry;
    public CombatOutcomeProfile Outcome;

    [Header("Query / Hit")]
    public ContactQueryPolicy QueryPolicy;
    public HitPolicyParams HitPolicy;
    [Min(1)] public int MaxApplicationsTotal;

    [Header("Placement")]
    public SpawnSource Origin;
    public Vector3 LocalOffset;
    public Vector3 LocalEuler;

    [Header("Motion / Lifetime")]
    public MovementParams Motion;
    public SpawnedRuntimePolicyAuthoring RuntimePolicy;
    public SpawnedSpatialPolicyAuthoring SpatialPolicy;
    public CombatObjectDefinitionSO OnExpireSpawn;

    public static SpawnedCombatAuthoringData Default =>
        new SpawnedCombatAuthoringData
        {
            UseExplicitData = true,
            QueryPolicy = ContactQueryPolicy.Default,
            HitPolicy = HitPolicyParams.Default,
            MaxApplicationsTotal = 999,
            Origin = SpawnSource.SelfRootBone,
            Motion = MovementParams.DefaultStatic,
            RuntimePolicy = SpawnedRuntimePolicyAuthoring.OneSample,
            SpatialPolicy = new SpawnedSpatialPolicyAuthoring
            {
                UseExplicitPolicy = true,
                Guidance = SpawnedGuidanceKind.None,
                TargetLoss = SpawnedTargetLossPolicy.Terminate,
                Rotation = SpawnedRotationPolicy.SpawnRotation,
                CurveTimeDomain = SpawnedCurveTimeDomain.SecondsSinceSpawn,
                TravelMetric = SpawnedTravelMetric.PathLength,
                TravelLimitResponse = SpawnedTravelLimitResponse.Terminate,
                GeometryEvolution = SpawnedGeometryEvolutionKind.None,
                GeometryStartScale = 1f,
                GeometryEndScale = 1f,
            },
        };
}

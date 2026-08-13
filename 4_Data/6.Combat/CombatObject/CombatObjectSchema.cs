using System;

/// <summary>
/// 223.4 L1 — 作者看到的 Combat Definition 类型。
/// 零值刻意保留给历史资产，避免新增字段后把旧资产静默解释成 ActionContact。
/// </summary>
public enum CombatObjectArchetype : byte
{
    UnclassifiedLegacy = 0,
    ActionContact = 1,
    Projectile = 2,
    Area = 3,
    Aura = 4,
    Hazard = 5,
    SpawnedBurst = 6,
    CustomAdvanced = 7,
}

/// <summary>运行时真正使用的生命周期所有权模型。</summary>
public enum CombatExecutionModel : byte
{
    Unclassified = 0,
    ActionWindowBound = 1,
    SpawnedFinite = 2,
    OwnerBoundPersistent = 3,
    WorldBoundPersistent = 4,
    CustomExplicit = 5,
}

/// <summary>Definition 可启用的正交能力块；Archetype Schema 是唯一解释入口。</summary>
[Flags]
public enum CombatFeatureBlock : ulong
{
    None = 0UL,
    Geometry = 1UL << 0,
    AttackProfile = 1UL << 1,
    QueryPolicy = 1UL << 2,
    HitPolicy = 1UL << 3,
    ActionWindow = 1UL << 4,
    SpawnPlacement = 1UL << 5,
    Motion = 1UL << 6,
    Lifetime = 1UL << 7,
    Sampling = 1UL << 8,
    Attachment = 1UL << 9,
    Guidance = 1UL << 10,
    GeometryEvolution = 1UL << 11,
    TerminationSpawn = 1UL << 12,
}

/// <summary>同一 Definition 被消费的位置；验证不能只看资产自身字段。</summary>
public enum CombatDefinitionUseSite : byte
{
    Intrinsic = 0,
    ContactEvent = 1,
    SpawnRequest = 2,
    TerminationChild = 3,
}

/// <summary>显式序列化版本。Legacy 不会因为枚举零值获得新语义。</summary>
public enum CombatObjectSchemaVersion : byte
{
    Legacy = 0,
    ArchetypeV1 = 1,
    ArchetypeV2 = 2,
}

public enum CombatObjectMigrationState : byte
{
    RequiresReview = 0,
    Classified = 1,
    Migrated = 2,
}

/// <summary>中央 Schema 的不可变查询结果。</summary>
public readonly struct CombatArchetypeSchema
{
    public readonly CombatObjectArchetype Archetype;
    public readonly CombatExecutionModel ExecutionModel;
    public readonly CombatFeatureBlock RequiredFeatures;
    public readonly CombatFeatureBlock AllowedFeatures;
    public readonly bool AllowsContactEvent;
    public readonly bool AllowsSpawnRequest;
    public readonly bool AllowsTerminationChild;

    public CombatArchetypeSchema(
        CombatObjectArchetype archetype,
        CombatExecutionModel executionModel,
        CombatFeatureBlock requiredFeatures,
        CombatFeatureBlock allowedFeatures,
        bool allowsContactEvent,
        bool allowsSpawnRequest,
        bool allowsTerminationChild)
    {
        Archetype = archetype;
        ExecutionModel = executionModel;
        RequiredFeatures = requiredFeatures;
        AllowedFeatures = allowedFeatures;
        AllowsContactEvent = allowsContactEvent;
        AllowsSpawnRequest = allowsSpawnRequest;
        AllowsTerminationChild = allowsTerminationChild;
    }

    public bool Allows(CombatDefinitionUseSite useSite)
    {
        return useSite switch
        {
            CombatDefinitionUseSite.Intrinsic => true,
            CombatDefinitionUseSite.ContactEvent => AllowsContactEvent,
            CombatDefinitionUseSite.SpawnRequest => AllowsSpawnRequest,
            CombatDefinitionUseSite.TerminationChild => AllowsTerminationChild,
            _ => false,
        };
    }
}

/// <summary>
/// Archetype 的唯一权威映射。Inspector、Timeline、Spawner 与迁移器必须查询本表，
/// 不得各自复制一份 switch。
/// </summary>
public static class CombatObjectArchetypeSchemaRegistry
{
    const CombatFeatureBlock Common =
        CombatFeatureBlock.Geometry
        | CombatFeatureBlock.AttackProfile
        | CombatFeatureBlock.QueryPolicy
        | CombatFeatureBlock.HitPolicy;

    const CombatFeatureBlock SpawnedCommon =
        Common
        | CombatFeatureBlock.SpawnPlacement
        | CombatFeatureBlock.Lifetime
        | CombatFeatureBlock.Sampling;

    public static CombatArchetypeSchema Get(CombatObjectArchetype archetype)
    {
        switch (archetype)
        {
            case CombatObjectArchetype.ActionContact:
                // ActionContact 空间真相在 ActionContactAuthoring（224.1）；
                // Motion feature 门控 Binding/Sweep Inspector（L2），Geometry 仍来自 ShapePreset。
                return new CombatArchetypeSchema(
                    archetype,
                    CombatExecutionModel.ActionWindowBound,
                    Common | CombatFeatureBlock.ActionWindow,
                    Common | CombatFeatureBlock.ActionWindow | CombatFeatureBlock.Motion,
                    allowsContactEvent: true,
                    allowsSpawnRequest: false,
                    allowsTerminationChild: false);

            case CombatObjectArchetype.Projectile:
                return Spawned(
                    archetype,
                    SpawnedCommon | CombatFeatureBlock.Motion,
                    SpawnedCommon
                    | CombatFeatureBlock.Motion
                    | CombatFeatureBlock.Guidance
                    | CombatFeatureBlock.GeometryEvolution
                    | CombatFeatureBlock.TerminationSpawn);

            case CombatObjectArchetype.Area:
                return Spawned(
                    archetype,
                    SpawnedCommon,
                    SpawnedCommon
                    | CombatFeatureBlock.Motion
                    | CombatFeatureBlock.Attachment
                    | CombatFeatureBlock.GeometryEvolution
                    | CombatFeatureBlock.TerminationSpawn);

            case CombatObjectArchetype.Aura:
                return new CombatArchetypeSchema(
                    archetype,
                    CombatExecutionModel.OwnerBoundPersistent,
                    SpawnedCommon | CombatFeatureBlock.Attachment,
                    SpawnedCommon
                    | CombatFeatureBlock.Attachment
                    | CombatFeatureBlock.GeometryEvolution,
                    false,
                    true,
                    false);

            case CombatObjectArchetype.Hazard:
                return new CombatArchetypeSchema(
                    archetype,
                    CombatExecutionModel.WorldBoundPersistent,
                    SpawnedCommon,
                    SpawnedCommon
                    | CombatFeatureBlock.GeometryEvolution
                    | CombatFeatureBlock.TerminationSpawn,
                    false,
                    true,
                    true);

            case CombatObjectArchetype.SpawnedBurst:
                return Spawned(
                    archetype,
                    SpawnedCommon,
                    SpawnedCommon | CombatFeatureBlock.GeometryEvolution | CombatFeatureBlock.TerminationSpawn);

            case CombatObjectArchetype.CustomAdvanced:
                return new CombatArchetypeSchema(
                    archetype,
                    CombatExecutionModel.CustomExplicit,
                    Common,
                    (CombatFeatureBlock)ulong.MaxValue,
                    true,
                    true,
                    true);

            default:
                return new CombatArchetypeSchema(
                    CombatObjectArchetype.UnclassifiedLegacy,
                    CombatExecutionModel.Unclassified,
                    CombatFeatureBlock.None,
                    CombatFeatureBlock.None,
                    false,
                    false,
                    false);
        }
    }

    static CombatArchetypeSchema Spawned(
        CombatObjectArchetype archetype,
        CombatFeatureBlock required,
        CombatFeatureBlock allowed)
    {
        return new CombatArchetypeSchema(
            archetype,
            CombatExecutionModel.SpawnedFinite,
            required,
            allowed,
            false,
            true,
            true);
    }
}

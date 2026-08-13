using UnityEngine;

/// <summary>一次 Action Contact 解析出的只读运行规格。</summary>
public readonly struct ResolvedContactSpec
{
    public readonly CombatObjectDefinitionSO Definition;
    public readonly AttackShapePresetSO ShapePreset;
    public readonly HitShapeMode ShapeMode;
    public readonly HitShapeSO Geometry;
    public readonly WeaponSocketSetSO WeaponSockets;
    public readonly WeaponSocketLayoutSO WeaponSocketLayout;
    public readonly SpawnSource Origin;
    public readonly Vector3 LocalOffset;
    public readonly Quaternion LocalRotation;
    public readonly ContactMotionKind Motion;
    public readonly ContactAnchorBindingMode BindingMode;
    public readonly ContactSweepPolicy SweepPolicy;
    public readonly ContactAnchorScalePolicy ScalePolicy;
    public readonly bool UsesLegacyAuthoring;
    public readonly ContactQueryPolicy Query;
    public readonly HitPolicyParams HitPolicy;
    public readonly CombatAttackProfile AttackProfile;
    public readonly int DefinitionRevision;

    public ResolvedContactSpec(
        CombatObjectDefinitionSO definition,
        AttackShapePresetSO shapePreset,
        HitShapeMode shapeMode,
        HitShapeSO geometry,
        WeaponSocketSetSO weaponSockets,
        WeaponSocketLayoutSO weaponSocketLayout,
        SpawnSource origin,
        Vector3 localOffset,
        Quaternion localRotation,
        ContactMotionKind motion,
        ContactAnchorBindingMode bindingMode,
        ContactSweepPolicy sweepPolicy,
        ContactAnchorScalePolicy scalePolicy,
        bool usesLegacyAuthoring,
        in ContactQueryPolicy query,
        in HitPolicyParams hitPolicy,
        in CombatAttackProfile attackProfile,
        int definitionRevision)
    {
        Definition = definition;
        ShapePreset = shapePreset;
        ShapeMode = shapeMode;
        Geometry = geometry;
        WeaponSockets = weaponSockets;
        WeaponSocketLayout = weaponSocketLayout;
        Origin = origin;
        LocalOffset = localOffset;
        LocalRotation = localRotation;
        Motion = motion;
        BindingMode = bindingMode;
        SweepPolicy = sweepPolicy;
        ScalePolicy = scalePolicy;
        UsesLegacyAuthoring = usesLegacyAuthoring;
        Query = query;
        HitPolicy = hitPolicy;
        AttackProfile = attackProfile;
        DefinitionRevision = definitionRevision;
    }
}

/// <summary>
/// L1 的 Spawned 快照骨架。后续 Landing 会把旧 Movement/Lifecycle 拆为明确策略，
/// 但 Runtime 已有一个稳定的“只读解析后输入”边界。
/// </summary>
public readonly struct ResolvedSpawnedCombatSpec
{
    public readonly CombatObjectDefinitionSO Definition;
    public readonly CombatObjectArchetype Archetype;
    public readonly CombatExecutionModel ExecutionModel;
    public readonly HitShapeSO Geometry;
    public readonly MovementParams LegacyMovement;
    public readonly LifecycleParams LegacyLifecycle;
    public readonly DamageDefinitionSO LegacyDamage;
    public readonly CombatAttackProfile AttackProfile;
    public readonly CombatOutcomeProfile OutcomeProfile;
    public readonly bool UsesLegacyAuthoring;
    public readonly SpawnSource Origin;
    public readonly Vector3 LocalOffset;
    public readonly Quaternion LocalRotation;
    public readonly TargetFilterParams LegacyTargetFilter;
    public readonly LayerMask QueryLayerMask;
    public readonly TargetProfile TargetProfile;
    public readonly HitPolicyParams HitPolicy;
    public readonly int MaxApplicationsTotal;
    public readonly ResolvedSpawnedRuntimePolicy RuntimePolicy;
    public readonly ResolvedSpawnedSpatialSpec Spatial;
    public readonly CombatObjectDefinitionSO TerminationChildDefinition;
    public readonly int DefinitionRevision;

    public ResolvedSpawnedCombatSpec(
        CombatObjectDefinitionSO definition,
        CombatObjectArchetype archetype,
        CombatExecutionModel executionModel,
        HitShapeSO geometry,
        in MovementParams legacyMovement,
        in LifecycleParams legacyLifecycle,
        DamageDefinitionSO legacyDamage,
        in CombatAttackProfile attackProfile,
        in CombatOutcomeProfile outcomeProfile,
        bool usesLegacyAuthoring,
        SpawnSource origin,
        Vector3 localOffset,
        Quaternion localRotation,
        in TargetFilterParams legacyTargetFilter,
        LayerMask queryLayerMask,
        in TargetProfile targetProfile,
        in HitPolicyParams hitPolicy,
        int maxApplicationsTotal,
        in ResolvedSpawnedRuntimePolicy runtimePolicy,
        in ResolvedSpawnedSpatialSpec spatial,
        CombatObjectDefinitionSO terminationChildDefinition,
        int definitionRevision)
    {
        Definition = definition;
        Archetype = archetype;
        ExecutionModel = executionModel;
        Geometry = geometry;
        LegacyMovement = legacyMovement;
        LegacyLifecycle = legacyLifecycle;
        LegacyDamage = legacyDamage;
        AttackProfile = attackProfile;
        OutcomeProfile = outcomeProfile;
        UsesLegacyAuthoring = usesLegacyAuthoring;
        Origin = origin;
        LocalOffset = localOffset;
        LocalRotation = localRotation;
        LegacyTargetFilter = legacyTargetFilter;
        QueryLayerMask = queryLayerMask;
        TargetProfile = targetProfile;
        HitPolicy = hitPolicy;
        MaxApplicationsTotal = Mathf.Max(1, maxApplicationsTotal);
        RuntimePolicy = runtimePolicy;
        Spatial = spatial;
        TerminationChildDefinition = terminationChildDefinition;
        DefinitionRevision = definitionRevision;
    }
}

public static class CombatObjectSpecResolver
{
    public static bool TryResolveContact(
        CombatObjectDefinitionSO definition,
        in ContactOverrideData eventOverride,
        out ResolvedContactSpec spec,
        out CombatDefinitionValidationResult validation)
    {
        if (!ContactAuthoringAdapter.TryResolveContactAuthoring(
                definition,
                LegacyContactOverrideAdapter.From(in eventOverride),
                out var config,
                out _,
                out validation))
        {
            spec = default;
            return false;
        }

        spec = new ResolvedContactSpec(
            config.Definition,
            config.ShapePreset,
            config.ShapeMode,
            config.Geometry,
            config.WeaponSockets,
            config.WeaponSocketLayout,
            config.Origin.Source,
            config.LocalPosition,
            config.LocalRotation,
            config.LegacyMotion,
            config.BindingMode,
            config.SweepPolicy,
            config.ScalePolicy,
            config.UsesLegacyAuthoring,
            in config.Query,
            in config.HitPolicy,
            in config.AttackProfile,
            config.DefinitionRevision);
        return true;
    }

    public static bool TryResolveSpawned(
        CombatObjectDefinitionSO definition,
        out ResolvedSpawnedCombatSpec spec,
        out CombatDefinitionValidationResult validation)
    {
        return TryResolveSpawned(
            definition,
            CombatDefinitionUseSite.SpawnRequest,
            out spec,
            out validation);
    }

    public static bool TryResolveSpawned(
        CombatObjectDefinitionSO definition,
        CombatDefinitionUseSite useSite,
        out ResolvedSpawnedCombatSpec spec,
        out CombatDefinitionValidationResult validation)
    {
        validation = CombatObjectDefinitionValidator.Validate(
            definition,
            useSite);
        if (!validation.IsValid)
        {
            spec = default;
            return false;
        }

        var schema = CombatObjectArchetypeSchemaRegistry.Get(definition.Archetype);
        var useV2Authoring = definition.SchemaVersion >= CombatObjectSchemaVersion.ArchetypeV2
            && definition.Archetype != CombatObjectArchetype.ActionContact;
        if (useV2Authoring)
        {
            var data = definition.SpawnedData;
            var emptyLegacyLifecycle = default(LifecycleParams);
            var runtimePolicyV2 = SpawnedRuntimePolicyResolver.Resolve(
                in data.RuntimePolicy,
                in emptyLegacyLifecycle);
            var spatialV2 = SpawnedSpatialSpecResolver.Resolve(
                in data.SpatialPolicy,
                in data.Motion,
                in runtimePolicyV2);
            var outcomeV2 = data.Outcome;
            var attackV2 = outcomeV2.Kind == CombatOutcomeAuthoringKind.HitReaction
                ? new CombatAttackProfile { Reaction = outcomeV2.Reaction }
                : CombatAttackProfile.Default;
            var emptyLegacyTargetFilter = default(TargetFilterParams);
            var localRotationV2 = Quaternion.Euler(data.LocalEuler);
            spec = new ResolvedSpawnedCombatSpec(
                definition,
                definition.Archetype,
                schema.ExecutionModel,
                data.Geometry,
                in data.Motion,
                in emptyLegacyLifecycle,
                null,
                in attackV2,
                in outcomeV2,
                usesLegacyAuthoring: false,
                data.Origin,
                data.LocalOffset,
                localRotationV2,
                in emptyLegacyTargetFilter,
                data.QueryPolicy.LayerMask,
                in data.QueryPolicy.Target,
                in data.HitPolicy,
                Mathf.Max(1, data.MaxApplicationsTotal),
                in runtimePolicyV2,
                in spatialV2,
                data.OnExpireSpawn,
                definition.DefinitionRevision);
            return true;
        }

        var runtimePolicy = SpawnedRuntimePolicyResolver.Resolve(
            in definition.SpawnedPolicy,
            in definition.Lifecycle);
        var spatial = SpawnedSpatialSpecResolver.Resolve(
            in definition.SpatialPolicy,
            in definition.Movement,
            in runtimePolicy);
        var targetProfile = definition.QueryPolicy.Target.UnitKinds != UnitKindMask.None
            ? definition.QueryPolicy.Target
            : LegacyTargetProfileAdapter.Convert(in definition.TargetFilter);
        var hitPolicy = definition.HitPolicy.MaxHitsPerTarget > 0
                        && definition.HitPolicy.MaxTargets > 0
            ? CombatHitPolicy.Normalize(in definition.HitPolicy)
            : new HitPolicyParams
            {
                Kind = definition.Lifecycle.MaxHitsPerTarget > 1
                    ? HitPolicyKind.Multi
                    : HitPolicyKind.PerTarget,
                IntervalSeconds = Mathf.Max(0.01f, definition.Lifecycle.TickInterval),
                MaxHitsPerTarget = Mathf.Max(1, definition.Lifecycle.MaxHitsPerTarget),
                MaxTargets = Mathf.Max(1, definition.Lifecycle.MaxTargets),
            };
        var legacyOutcome = definition.Damage != null
            ? CombatOutcomeProfile.FromDamage(definition.Damage)
            : CombatOutcomeProfile.FromReaction(in definition.AttackProfile.Reaction);
        var legacyLocalRotation = Quaternion.Euler(definition.LocalEulerOffset);
        spec = new ResolvedSpawnedCombatSpec(
            definition,
            definition.Archetype,
            schema.ExecutionModel,
            definition.ShapePreset != null ? definition.ShapePreset.Geometry : definition.Shape,
            in definition.Movement,
            in definition.Lifecycle,
            definition.Damage,
            in definition.AttackProfile,
            in legacyOutcome,
            usesLegacyAuthoring: true,
            definition.SpawnSource,
            definition.LocalOffset,
            legacyLocalRotation,
            in definition.TargetFilter,
            definition.QueryLayerMask,
            in targetProfile,
            in hitPolicy,
            Mathf.Max(1, definition.Lifecycle.MaxTargets),
            in runtimePolicy,
            in spatial,
            definition.Lifecycle.OnExpireSpawn,
            definition.DefinitionRevision);
        return true;
    }
}

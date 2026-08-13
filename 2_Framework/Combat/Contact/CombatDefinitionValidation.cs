using System.Collections.Generic;

public enum CombatValidationSeverity : byte
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Blocker = 3,
}

/// <summary>可定位到资产字段和使用点的结构化问题。</summary>
public readonly struct CombatValidationIssue
{
    public readonly string Code;
    public readonly CombatValidationSeverity Severity;
    public readonly CombatDefinitionUseSite UseSite;
    public readonly string FieldPath;
    public readonly string Message;

    public CombatValidationIssue(
        string code,
        CombatValidationSeverity severity,
        CombatDefinitionUseSite useSite,
        string fieldPath,
        string message)
    {
        Code = code;
        Severity = severity;
        UseSite = useSite;
        FieldPath = fieldPath;
        Message = message;
    }
}

/// <summary>解析/验证边界的结果。Editor 可查看全部问题，Runtime 可读取首个阻断原因。</summary>
public sealed class CombatDefinitionValidationResult
{
    readonly List<CombatValidationIssue> _issues = new List<CombatValidationIssue>(4);

    public IReadOnlyList<CombatValidationIssue> Issues => _issues;
    public bool IsValid { get; private set; } = true;

    public void Add(in CombatValidationIssue issue)
    {
        _issues.Add(issue);
        if (issue.Severity >= CombatValidationSeverity.Error)
        {
            IsValid = false;
        }
    }

    public string FirstErrorOrNull()
    {
        for (var i = 0; i < _issues.Count; i++)
        {
            if (_issues[i].Severity >= CombatValidationSeverity.Error)
            {
                return _issues[i].Message;
            }
        }

        return null;
    }
}

public static class CombatObjectDefinitionValidator
{
    public static CombatDefinitionValidationResult Validate(
        CombatObjectDefinitionSO definition,
        CombatDefinitionUseSite useSite)
    {
        var result = new CombatDefinitionValidationResult();
        if (definition == null)
        {
            AddError(result, "CO.NULL", useSite, string.Empty, "Definition is null.");
            return result;
        }

        var isV2Spawned = definition.SchemaVersion >= CombatObjectSchemaVersion.ArchetypeV2
            && definition.Archetype != CombatObjectArchetype.ActionContact;
        if (!isV2Spawned && definition.Shape == null && definition.ShapePreset == null)
        {
            AddError(result, "CO.GEOMETRY.NULL", useSite, "Shape", "Shape/ShapePreset is null.");
        }

        if (!HasConfiguredDefinitionOutcome(definition, isV2Spawned))
        {
            AddError(result, "CO.ATTACK.NULL", useSite, "AttackProfile", "Damage/AttackProfile is not configured.");
        }

        if (definition.Archetype == CombatObjectArchetype.UnclassifiedLegacy)
        {
            if (useSite != CombatDefinitionUseSite.Intrinsic)
            {
                AddError(
                    result,
                    "CO.ARCHETYPE.UNCLASSIFIED",
                    useSite,
                    "Archetype",
                    "Unclassified legacy definitions require explicit migration before use.");
            }

            ValidateLegacyIntrinsic(definition, useSite, result);
            return result;
        }

        if (definition.SchemaVersion == CombatObjectSchemaVersion.Legacy)
        {
            AddError(
                result,
                "CO.SCHEMA.LEGACY",
                useSite,
                "SchemaVersion",
                "A classified definition must use an explicit non-legacy schema version.");
        }

        var schema = CombatObjectArchetypeSchemaRegistry.Get(definition.Archetype);
        if (!schema.Allows(useSite))
        {
            AddError(
                result,
                "CO.USESITE.INVALID",
                useSite,
                "Archetype",
                $"{definition.Archetype} cannot be consumed at {useSite}.");
        }

        if (definition.Archetype == CombatObjectArchetype.ActionContact && definition.ShapePreset == null)
        {
            AddError(
                result,
                "CO.CONTACT.PRESET.NULL",
                useSite,
                "ShapePreset",
                "ActionContact requires an AttackShapePreset.");
        }
        else if (definition.Archetype == CombatObjectArchetype.ActionContact)
        {
            var preset = definition.ShapePreset;
            if (preset.ShapeMode == HitShapeMode.Volume && preset.Geometry == null)
            {
                AddError(
                    result,
                    "CO.CONTACT.GEOMETRY.NULL",
                    useSite,
                    "ShapePreset.Geometry",
                    "Volume ActionContact requires ShapePreset.Geometry.");
            }

            if (preset.ShapeMode == HitShapeMode.WeaponTrace
                && (preset.WeaponSocketLayout == null
                    || preset.WeaponSocketLayout.Bindings == null
                    || preset.WeaponSocketLayout.Bindings.Length == 0))
            {
                AddError(
                    result,
                    "CO.CONTACT.SOCKET_LAYOUT.NULL",
                    useSite,
                    "ShapePreset.WeaponSocketLayout",
                    "WeaponTrace ActionContact requires a baked WeaponSocketLayout.");
            }

            ValidateActionContactAuthoring(definition, useSite, result);
            ValidateQueryTargetRequired(definition, useSite, result);

            if (definition.ActionContactAuthoring.UseExplicitData
                && definition.ActionContactAuthoring.BindingMode
                    == ContactAnchorBindingMode.StaticAtWindowStart
                && preset.ShapeMode == HitShapeMode.WeaponTrace)
            {
                AddError(
                    result,
                    "CO.CONTACT.BINDING.INVALID",
                    useSite,
                    "ActionContactAuthoring.BindingMode",
                    "StaticAtWindowStart + WeaponTrace is not supported in L3; use FollowAnchor.");
            }
        }

        if (schema.ExecutionModel != CombatExecutionModel.ActionWindowBound)
        {
            if (isV2Spawned)
            {
                ValidateV2Spawned(definition, useSite, result);
                ValidateQueryTargetRequired(definition, useSite, result);
            }
            else
            {
                ValidateLegacyIntrinsic(definition, useSite, result);
                ValidateSpawnedPolicy(definition, useSite, result);
                ValidateSpatialPolicy(definition, useSite, result);
            }
        }

        return result;
    }

    static void ValidateActionContactAuthoring(
        CombatObjectDefinitionSO definition,
        CombatDefinitionUseSite useSite,
        CombatDefinitionValidationResult result)
    {
        var authoring = definition.ActionContactAuthoring;
        if (!authoring.UseExplicitData)
        {
            if (useSite == CombatDefinitionUseSite.ContactEvent)
            {
                result.Add(new CombatValidationIssue(
                    "CO.CONTACT.LEGACY.MIGRATION_REQUIRED",
                    CombatValidationSeverity.Warning,
                    useSite,
                    "ActionContactAuthoring",
                    "ActionContact still uses Legacy Preset/Override placement; migrate to CO single source."));
            }

            return;
        }

        if (authoring.Version != ActionContactAuthoringVersion.CombatObjectSingleSourceV1)
        {
            AddError(
                result,
                "CO.CONTACT.AUTHORING.MISSING",
                useSite,
                "ActionContactAuthoring.Version",
                "UseExplicitData requires CombatObjectSingleSourceV1.");
        }

        if (authoring.BindingMode == ContactAnchorBindingMode.StaticAtWindowStart
            && authoring.SweepPolicy == ContactSweepPolicy.BetweenSamples)
        {
            AddError(
                result,
                "CO.CONTACT.SWEEP.STATIC_CONFLICT",
                useSite,
                "ActionContactAuthoring.SweepPolicy",
                "StaticAtWindowStart cannot combine with BetweenSamples.");
        }
    }

    static void ValidateQueryTargetRequired(
        CombatObjectDefinitionSO definition,
        CombatDefinitionUseSite useSite,
        CombatDefinitionValidationResult result)
    {
        // VNext ActionContact / Spawned V2：禁止用 TargetFilter 静默兜底。
        var isV2Spawned = definition.SchemaVersion >= CombatObjectSchemaVersion.ArchetypeV2
            && definition.Archetype != CombatObjectArchetype.ActionContact;
        TargetProfile target;
        string fieldPath;
        if (definition.Archetype == CombatObjectArchetype.ActionContact)
        {
            target = definition.QueryPolicy.Target;
            fieldPath = "QueryPolicy.Target";
        }
        else if (isV2Spawned)
        {
            target = definition.SpawnedData.QueryPolicy.Target;
            fieldPath = "SpawnedData.QueryPolicy.Target";
        }
        else
        {
            return;
        }

        if (target.UnitKinds == UnitKindMask.None)
        {
            AddError(
                result,
                "CO.QUERY.TARGET.MISSING",
                useSite,
                fieldPath,
                "VNext definitions require ContactQueryPolicy.Target (TargetProfile); TargetFilter is not a fallback.");
        }
    }

    static bool HasConfiguredDefinitionOutcome(
        CombatObjectDefinitionSO definition,
        bool isV2Spawned)
    {
        if (isV2Spawned)
        {
            var profile = definition.SpawnedData.Outcome;
            return profile.Kind == CombatOutcomeAuthoringKind.DamageDefinition
                ? profile.DamageDefinition != null
                : HasConfiguredOutcome(in profile.Reaction);
        }

        return definition.Damage != null
            || HasConfiguredOutcome(in definition.AttackProfile);
    }

    static bool HasConfiguredOutcome(in CombatAttackProfile attackProfile)
    {
        return HasConfiguredOutcome(in attackProfile.Reaction);
    }

    static bool HasConfiguredOutcome(in HitReaction reaction)
    {
        return reaction.BaseDamage > 0f
            || reaction.ImpulseForce > 0f
            || reaction.LaunchUpSpeed > 0f
            || reaction.HitStopSeconds > 0f
            || reaction.CameraShakeIntensity > 0f
            || reaction.CameraShakeDuration > 0f
            || reaction.OnHitEffect != null
            || !string.IsNullOrWhiteSpace(reaction.VfxPayload)
            || !string.IsNullOrWhiteSpace(reaction.SfxPayload);
    }

    static void ValidateV2Spawned(
        CombatObjectDefinitionSO definition,
        CombatDefinitionUseSite useSite,
        CombatDefinitionValidationResult result)
    {
        var data = definition.SpawnedData;
        if (!data.UseExplicitData)
        {
            AddError(
                result,
                "CO.V2.EXPLICIT_DATA",
                useSite,
                "SpawnedData.UseExplicitData",
                "ArchetypeV2 Spawned Definition must enable SpawnedData.UseExplicitData.");
        }

        if (data.Geometry == null)
        {
            AddError(
                result,
                "CO.V2.GEOMETRY.NULL",
                useSite,
                "SpawnedData.Geometry",
                "ArchetypeV2 Spawned Definition requires explicit Geometry.");
        }

        if (data.RuntimePolicy.UseExplicitPolicy == false)
        {
            AddError(
                result,
                "CO.V2.RUNTIME_POLICY.LEGACY",
                useSite,
                "SpawnedData.RuntimePolicy",
                "ArchetypeV2 Spawned Definition cannot map Lifetime/Sampling from Legacy Lifecycle.");
        }

        if (data.SpatialPolicy.UseExplicitPolicy == false)
        {
            AddError(
                result,
                "CO.V2.SPATIAL_POLICY.LEGACY",
                useSite,
                "SpawnedData.SpatialPolicy",
                "ArchetypeV2 Spawned Definition cannot map Motion from Legacy Movement.");
        }

        if (data.MaxApplicationsTotal < 1)
        {
            AddError(
                result,
                "CO.V2.HIT.MAX_APPLICATIONS",
                useSite,
                "SpawnedData.MaxApplicationsTotal",
                "ArchetypeV2 Spawned Definition requires MaxApplicationsTotal >= 1.");
        }

        var hitPolicy = data.HitPolicy;
        if (hitPolicy.MaxHitsPerTarget < 1 || hitPolicy.MaxTargets < 1)
        {
            AddError(
                result,
                "CO.V2.HIT.POLICY",
                useSite,
                "SpawnedData.HitPolicy",
                "ArchetypeV2 Spawned Definition requires positive HitPolicy limits.");
        }
    }

    static void ValidateSpatialPolicy(
        CombatObjectDefinitionSO definition,
        CombatDefinitionUseSite useSite,
        CombatDefinitionValidationResult result)
    {
        var runtime = SpawnedRuntimePolicyResolver.Resolve(
            in definition.SpawnedPolicy,
            in definition.Lifecycle);
        var spatial = SpawnedSpatialSpecResolver.Resolve(
            in definition.SpatialPolicy,
            in definition.Movement,
            in runtime);

        if (spatial.CurveTimeDomain == SpawnedCurveTimeDomain.NormalizedLifetime
            && runtime.LifetimeKind != SpawnedLifetimeKind.Timed)
        {
            AddError(
                result,
                "CO.CURVE.TIME_DOMAIN",
                useSite,
                "SpatialPolicy.CurveTimeDomain",
                "NormalizedLifetime curve domain requires a Timed lifetime.");
        }

        if (spatial.CurveTimeDomain == SpawnedCurveTimeDomain.NormalizedTravel
            && spatial.TravelLimit <= 0f)
        {
            AddError(
                result,
                "CO.CURVE.TRAVEL_DOMAIN",
                useSite,
                "Movement.MaxDistance",
                "NormalizedTravel curve domain requires a positive TravelLimit.");
        }

        if (spatial.GeometryEvolution != SpawnedGeometryEvolutionKind.None
            && !SpawnedGeometryCapability.SupportsEvolution(definition.Shape))
        {
            AddError(
                result,
                "CO.GEOMETRY.EVOLUTION_UNSUPPORTED",
                useSite,
                "Shape",
                "Configured geometry evolution requires Sphere, Box, or Capsule capability.");
        }
    }

    static void ValidateSpawnedPolicy(
        CombatObjectDefinitionSO definition,
        CombatDefinitionUseSite useSite,
        CombatDefinitionValidationResult result)
    {
        var policy = definition.SpawnedPolicy;
        if (!policy.UseExplicitPolicy)
        {
            return;
        }

        if (policy.LifetimeKind == SpawnedLifetimeKind.Timed
            && policy.DurationSeconds <= 0f)
        {
            AddError(
                result,
                "CO.LIFETIME.TIMED_DURATION",
                useSite,
                "SpawnedPolicy.DurationSeconds",
                "Timed lifetime requires DurationSeconds > 0.");
        }

        if (policy.SamplingKind == SpawnedSamplingKind.FixedInterval
            && policy.SamplingIntervalSeconds <= 0f)
        {
            AddError(
                result,
                "CO.SAMPLING.INTERVAL",
                useSite,
                "SpawnedPolicy.SamplingIntervalSeconds",
                "FixedInterval sampling requires SamplingIntervalSeconds > 0.");
        }

        if (policy.MaxCatchUpSamplesPerTick < 1)
        {
            AddError(
                result,
                "CO.SAMPLING.CATCHUP_BUDGET",
                useSite,
                "SpawnedPolicy.MaxCatchUpSamplesPerTick",
                "MaxCatchUpSamplesPerTick must be at least 1.");
        }
    }

    static void ValidateLegacyIntrinsic(
        CombatObjectDefinitionSO definition,
        CombatDefinitionUseSite useSite,
        CombatDefinitionValidationResult result)
    {
        if (definition.Lifecycle.MaxHitsPerTarget < 1)
        {
            AddError(result, "CO.HIT.MAX_PER_TARGET", useSite, "Lifecycle.MaxHitsPerTarget", "MaxHitsPerTarget < 1.");
        }

        if (definition.Lifecycle.MaxTargets < 1)
        {
            AddError(result, "CO.HIT.MAX_TARGETS", useSite, "Lifecycle.MaxTargets", "MaxTargets < 1.");
        }
    }

    static void AddError(
        CombatDefinitionValidationResult result,
        string code,
        CombatDefinitionUseSite useSite,
        string fieldPath,
        string message)
    {
        result.Add(new CombatValidationIssue(
            code,
            CombatValidationSeverity.Error,
            useSite,
            fieldPath,
            message));
    }
}

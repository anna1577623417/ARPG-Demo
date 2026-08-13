using NUnit.Framework;
using UnityEngine;

public sealed class CombatObjectSchemaTests
{
    [Test]
    public void LegacyZeroValue_IsNotActionContact()
    {
        var def = ScriptableObject.CreateInstance<CombatObjectDefinitionSO>();
        try
        {
            Assert.AreEqual(CombatObjectArchetype.UnclassifiedLegacy, def.Archetype);
            Assert.AreEqual(CombatObjectSchemaVersion.Legacy, def.SchemaVersion);
            Assert.IsFalse(
                CombatObjectArchetypeSchemaRegistry
                    .Get(def.Archetype)
                    .Allows(CombatDefinitionUseSite.ContactEvent));
        }
        finally
        {
            Object.DestroyImmediate(def);
        }
    }

    [Test]
    public void ActionContactSchema_AllowsOnlyContactUseSite()
    {
        var schema = CombatObjectArchetypeSchemaRegistry.Get(CombatObjectArchetype.ActionContact);

        Assert.AreEqual(CombatExecutionModel.ActionWindowBound, schema.ExecutionModel);
        Assert.IsTrue(schema.Allows(CombatDefinitionUseSite.ContactEvent));
        Assert.IsFalse(schema.Allows(CombatDefinitionUseSite.SpawnRequest));
        Assert.IsFalse(schema.Allows(CombatDefinitionUseSite.TerminationChild));
    }

    [Test]
    public void ProjectileSchema_AllowsSpawnAndTerminationChild()
    {
        var schema = CombatObjectArchetypeSchemaRegistry.Get(CombatObjectArchetype.Projectile);

        Assert.AreEqual(CombatExecutionModel.SpawnedFinite, schema.ExecutionModel);
        Assert.IsFalse(schema.Allows(CombatDefinitionUseSite.ContactEvent));
        Assert.IsTrue(schema.Allows(CombatDefinitionUseSite.SpawnRequest));
        Assert.IsTrue(schema.Allows(CombatDefinitionUseSite.TerminationChild));
        Assert.AreNotEqual(
            CombatFeatureBlock.None,
            schema.RequiredFeatures & CombatFeatureBlock.Motion);
    }

    [Test]
    public void UnclassifiedDefinition_IsRejectedAtUseSite()
    {
        var (def, shape, damage) = MakeLegacyDefinition();
        try
        {
            Assert.IsTrue(def.IsValid(out _), "Legacy intrinsic validation remains compatible.");

            var validation = CombatObjectDefinitionValidator.Validate(
                def,
                CombatDefinitionUseSite.SpawnRequest);

            Assert.IsFalse(validation.IsValid);
            Assert.AreEqual("Unclassified legacy definitions require explicit migration before use.", validation.FirstErrorOrNull());
        }
        finally
        {
            Cleanup(def, shape, damage);
        }
    }

    [Test]
    public void ActionContactResolver_UsesEventOverrideWithoutMutatingPreset()
    {
        var shape = ScriptableObject.CreateInstance<SphereShapeSO>();
        var preset = ScriptableObject.CreateInstance<AttackShapePresetSO>();
        var def = ScriptableObject.CreateInstance<CombatObjectDefinitionSO>();
        try
        {
            preset.Geometry = shape;
            preset.DefaultOrigin = SpawnSource.SelfHandR;
            preset.DefaultLocalOffset = Vector3.forward;
            preset.DefaultMotion = ContactMotionKind.FollowAnchor;

            def.Archetype = CombatObjectArchetype.ActionContact;
            def.SchemaVersion = CombatObjectSchemaVersion.ArchetypeV1;
            def.MigrationState = CombatObjectMigrationState.Classified;
            def.ShapePreset = preset;
            def.AttackProfile = CombatAttackProfile.Default;
            def.QueryPolicy = ContactQueryPolicy.Default;
            def.HitPolicy = HitPolicyParams.Default;
            def.DefinitionRevision = 7;

            var eventOverride = new ContactOverrideData
            {
                OverridePlacement = true,
                Origin = SpawnSource.SelfRootBone,
                LocalOffset = Vector3.right * 2f,
                OverrideMotion = true,
                Motion = ContactMotionKind.SweepBetweenFrames,
            };

            var ok = CombatObjectSpecResolver.TryResolveContact(
                def,
                in eventOverride,
                out var spec,
                out var validation);

            Assert.IsTrue(ok, validation.FirstErrorOrNull());
            Assert.AreEqual(Vector3.right * 2f, spec.LocalOffset);
            Assert.AreEqual(ContactMotionKind.SweepBetweenFrames, spec.Motion);
            Assert.AreEqual(Vector3.forward, preset.DefaultLocalOffset, "Resolver must not mutate authoring data.");
            Assert.AreEqual(7, spec.DefinitionRevision);
        }
        finally
        {
            Object.DestroyImmediate(def);
            Object.DestroyImmediate(preset);
            Object.DestroyImmediate(shape);
        }
    }

    [Test]
    public void ClassifiedDefinition_WithLegacySchema_IsRejected()
    {
        var (def, shape, damage) = MakeLegacyDefinition();
        try
        {
            def.Archetype = CombatObjectArchetype.Projectile;

            var validation = CombatObjectDefinitionValidator.Validate(
                def,
                CombatDefinitionUseSite.SpawnRequest);

            Assert.IsFalse(validation.IsValid);
            StringAssert.Contains("schema", validation.FirstErrorOrNull().ToLowerInvariant());
        }
        finally
        {
            Cleanup(def, shape, damage);
        }
    }

    [Test]
    public void ActionContact_ImpulseOnlyOutcome_IsAConfiguredAttackProfile()
    {
        var shape = ScriptableObject.CreateInstance<SphereShapeSO>();
        var preset = ScriptableObject.CreateInstance<AttackShapePresetSO>();
        var def = ScriptableObject.CreateInstance<CombatObjectDefinitionSO>();
        try
        {
            preset.Geometry = shape;
            def.Archetype = CombatObjectArchetype.ActionContact;
            def.SchemaVersion = CombatObjectSchemaVersion.ArchetypeV1;
            def.MigrationState = CombatObjectMigrationState.Classified;
            def.ShapePreset = preset;
            def.AttackProfile = new CombatAttackProfile
            {
                Reaction = new HitReaction
                {
                    ImpulseLocalDir = Vector3.forward,
                    ImpulseForce = 3f,
                },
            };
            def.QueryPolicy = ContactQueryPolicy.Default;
            def.HitPolicy = HitPolicyParams.Default;

            var validation = CombatObjectDefinitionValidator.Validate(
                def,
                CombatDefinitionUseSite.ContactEvent);

            Assert.IsTrue(validation.IsValid, validation.FirstErrorOrNull());
        }
        finally
        {
            Object.DestroyImmediate(def);
            Object.DestroyImmediate(preset);
            Object.DestroyImmediate(shape);
        }
    }

    [Test]
    public void V2Spawned_RejectsLegacyFallbackAuthoring()
    {
        var shape = ScriptableObject.CreateInstance<SphereShapeSO>();
        var damage = ScriptableObject.CreateInstance<DamageDefinitionSO>();
        var def = ScriptableObject.CreateInstance<CombatObjectDefinitionSO>();
        try
        {
            def.Archetype = CombatObjectArchetype.Projectile;
            def.SchemaVersion = CombatObjectSchemaVersion.ArchetypeV2;
            def.MigrationState = CombatObjectMigrationState.Classified;
            def.Shape = shape;
            def.Damage = damage;

            var validation = CombatObjectDefinitionValidator.Validate(
                def,
                CombatDefinitionUseSite.SpawnRequest);

            Assert.IsFalse(validation.IsValid);
            StringAssert.Contains("UseExplicitData", validation.FirstErrorOrNull());
        }
        finally
        {
            Object.DestroyImmediate(def);
            Object.DestroyImmediate(shape);
            Object.DestroyImmediate(damage);
        }
    }

    [Test]
    public void V2SpawnedResolverUsesExplicitFeatureDataOnly()
    {
        var shape = ScriptableObject.CreateInstance<SphereShapeSO>();
        var def = ScriptableObject.CreateInstance<CombatObjectDefinitionSO>();
        try
        {
            var data = SpawnedCombatAuthoringData.Default;
            data.Geometry = shape;
            data.Motion = MovementParams.DefaultLinear(12f, 18f);
            data.RuntimePolicy = new SpawnedRuntimePolicyAuthoring
            {
                UseExplicitPolicy = true,
                LifetimeKind = SpawnedLifetimeKind.Timed,
                DurationSeconds = 2f,
                SamplingKind = SpawnedSamplingKind.OneAtStart,
                SamplingIntervalSeconds = 0f,
                CatchUpPolicy = SpawnedCatchUpPolicy.PreservePhaseClamp,
                MaxCatchUpSamplesPerTick = 1,
                SourceInvalidation = SpawnSourceInvalidationPolicy.Terminate,
            };
            data.Outcome = CombatOutcomeProfile.FromReaction(new HitReaction
            {
                BaseDamage = 5f,
            });

            def.Archetype = CombatObjectArchetype.Projectile;
            def.SchemaVersion = CombatObjectSchemaVersion.ArchetypeV2;
            def.MigrationState = CombatObjectMigrationState.Migrated;
            def.SpawnedData = data;

            var resolved = CombatObjectSpecResolver.TryResolveSpawned(
                def,
                out var spec,
                out var validation);

            Assert.IsTrue(resolved, validation.FirstErrorOrNull());
            Assert.IsFalse(spec.UsesLegacyAuthoring);
            Assert.AreSame(shape, spec.Geometry);
            Assert.IsNull(spec.LegacyDamage);
            Assert.AreEqual(CombatOutcomeAuthoringKind.HitReaction, spec.OutcomeProfile.Kind);
            Assert.AreEqual(MovementKind.Linear, spec.Spatial.MotionKind);
        }
        finally
        {
            Object.DestroyImmediate(def);
            Object.DestroyImmediate(shape);
        }
    }

    [Test]
    public void ActionContact_ExplicitAuthoringVersion_IsRequiredWhenEnabled()
    {
        var shape = ScriptableObject.CreateInstance<SphereShapeSO>();
        var preset = ScriptableObject.CreateInstance<AttackShapePresetSO>();
        var def = ScriptableObject.CreateInstance<CombatObjectDefinitionSO>();
        try
        {
            preset.Geometry = shape;
            def.Archetype = CombatObjectArchetype.ActionContact;
            def.SchemaVersion = CombatObjectSchemaVersion.ArchetypeV2;
            def.MigrationState = CombatObjectMigrationState.Classified;
            def.ShapePreset = preset;
            def.AttackProfile = CombatAttackProfile.Default;
            def.QueryPolicy = ContactQueryPolicy.Default;
            def.HitPolicy = HitPolicyParams.Default;
            def.ActionContactAuthoring = ActionContactAuthoringData.CreateNewV1();
            def.ActionContactAuthoring.Version = ActionContactAuthoringVersion.LegacyPresetOverride;

            var validation = CombatObjectDefinitionValidator.Validate(
                def,
                CombatDefinitionUseSite.ContactEvent);

            Assert.IsFalse(validation.IsValid);
            StringAssert.Contains("CombatObjectSingleSourceV1", validation.FirstErrorOrNull());
        }
        finally
        {
            Object.DestroyImmediate(def);
            Object.DestroyImmediate(preset);
            Object.DestroyImmediate(shape);
        }
    }

    [Test]
    public void ActionContact_DefaultAuthoring_DoesNotAutoEnableExplicit()
    {
        var def = ScriptableObject.CreateInstance<CombatObjectDefinitionSO>();
        try
        {
            Assert.IsFalse(def.ActionContactAuthoring.UseExplicitData);
            Assert.AreEqual(
                ActionContactAuthoringVersion.LegacyPresetOverride,
                def.ActionContactAuthoring.Version);
        }
        finally
        {
            Object.DestroyImmediate(def);
        }
    }

    static (CombatObjectDefinitionSO, SphereShapeSO, DamageDefinitionSO) MakeLegacyDefinition()
    {
        var shape = ScriptableObject.CreateInstance<SphereShapeSO>();
        var damage = ScriptableObject.CreateInstance<DamageDefinitionSO>();
        var def = ScriptableObject.CreateInstance<CombatObjectDefinitionSO>();
        def.Shape = shape;
        def.Damage = damage;
        def.Lifecycle = LifecycleParams.DefaultMeleeOneShot;
        return (def, shape, damage);
    }

    static void Cleanup(
        CombatObjectDefinitionSO definition,
        SphereShapeSO shape,
        DamageDefinitionSO damage)
    {
        Object.DestroyImmediate(definition);
        Object.DestroyImmediate(shape);
        Object.DestroyImmediate(damage);
    }
}

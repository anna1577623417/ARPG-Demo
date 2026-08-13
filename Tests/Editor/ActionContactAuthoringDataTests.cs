using NUnit.Framework;
using UnityEngine;

/// <summary>224.1 L1 — ActionContact Authoring Adapter 合同（ACD-*）。</summary>
public sealed class ActionContactAuthoringDataTests
{
    [Test]
    public void ACD01_ExplicitCo_IgnoresPresetAndOverride()
    {
        var (def, preset, shape) = MakeContactDefinition(explicitAuthoring: true);
        try
        {
            preset.DefaultLocalOffset = Vector3.forward * 9f;
            preset.DefaultOrigin = SpawnSource.SelfHandL;
            preset.DefaultMotion = ContactMotionKind.StaticAtSpawn;

            def.ActionContactAuthoring.LocalPosition = Vector3.up;
            def.ActionContactAuthoring.Origin = ContactAnchorReference.FromSpawnSource(SpawnSource.SelfHandR);
            def.ActionContactAuthoring.OriginPolicy = ContactOriginPolicy.Explicit;
            def.ActionContactAuthoring.BindingMode = ContactAnchorBindingMode.FollowAnchor;
            def.ActionContactAuthoring.SweepPolicy = ContactSweepPolicy.None;

            var legacy = new ContactOverrideData
            {
                OverridePlacement = false,
                OverrideMotion = false,
                LocalOffset = Vector3.one * 5f,
            };

            Assert.IsTrue(
                ContactAuthoringAdapter.TryResolveContactAuthoring(
                    def,
                    LegacyContactOverrideAdapter.From(in legacy),
                    out var config,
                    out var info,
                    out var validation),
                validation.FirstErrorOrNull());

            Assert.IsFalse(config.UsesLegacyAuthoring);
            Assert.IsFalse(info.UsesLegacyAuthoring);
            Assert.AreEqual(Vector3.up, config.LocalPosition);
            Assert.AreEqual(SpawnSource.SelfHandR, config.Origin.Source);
            Assert.AreEqual(ContactAnchorBindingMode.FollowAnchor, config.BindingMode);
            Assert.AreEqual(ContactSweepPolicy.None, config.SweepPolicy);
            Assert.AreNotEqual(Vector3.forward * 9f, config.LocalPosition);
        }
        finally
        {
            Cleanup(def, preset, shape);
        }
    }

    [Test]
    public void ACD02_LegacyPresetOverride_MatchesPreviousMerge()
    {
        var (def, preset, shape) = MakeContactDefinition(explicitAuthoring: false);
        try
        {
            preset.DefaultOrigin = SpawnSource.SelfHandR;
            preset.DefaultLocalOffset = Vector3.forward;
            preset.DefaultMotion = ContactMotionKind.FollowAnchor;

            var eventOverride = new ContactOverrideData
            {
                OverridePlacement = true,
                Origin = SpawnSource.SelfRootBone,
                LocalOffset = Vector3.right * 2f,
                OverrideMotion = true,
                Motion = ContactMotionKind.SweepBetweenFrames,
            };

            Assert.IsTrue(
                CombatObjectSpecResolver.TryResolveContact(
                    def,
                    in eventOverride,
                    out var spec,
                    out var validation),
                validation.FirstErrorOrNull());

            Assert.IsTrue(spec.UsesLegacyAuthoring);
            Assert.AreEqual(Vector3.right * 2f, spec.LocalOffset);
            Assert.AreEqual(SpawnSource.SelfRootBone, spec.Origin);
            Assert.AreEqual(ContactMotionKind.SweepBetweenFrames, spec.Motion);
            Assert.AreEqual(ContactAnchorBindingMode.FollowAnchor, spec.BindingMode);
            Assert.AreEqual(ContactSweepPolicy.BetweenSamples, spec.SweepPolicy);
        }
        finally
        {
            Cleanup(def, preset, shape);
        }
    }

    [Test]
    public void ACD03_LegacyMotionMapping_IsStable()
    {
        ContactAuthoringAdapter.MapLegacyMotion(
            ContactMotionKind.StaticAtSpawn, out var b0, out var s0);
        Assert.AreEqual(ContactAnchorBindingMode.StaticAtWindowStart, b0);
        Assert.AreEqual(ContactSweepPolicy.None, s0);

        ContactAuthoringAdapter.MapLegacyMotion(
            ContactMotionKind.FollowAnchor, out var b1, out var s1);
        Assert.AreEqual(ContactAnchorBindingMode.FollowAnchor, b1);
        Assert.AreEqual(ContactSweepPolicy.None, s1);

        ContactAuthoringAdapter.MapLegacyMotion(
            ContactMotionKind.SweepBetweenFrames, out var b2, out var s2);
        Assert.AreEqual(ContactAnchorBindingMode.FollowAnchor, b2);
        Assert.AreEqual(ContactSweepPolicy.BetweenSamples, s2);
    }

    [Test]
    public void ACD04_ExplicitMissingVersion_FailsWithCode()
    {
        var (def, preset, shape) = MakeContactDefinition(explicitAuthoring: true);
        try
        {
            def.ActionContactAuthoring.Version = ActionContactAuthoringVersion.LegacyPresetOverride;
            Assert.IsFalse(
                ContactAuthoringAdapter.TryResolveContactAuthoring(
                    def,
                    LegacyContactOverrideAdapter.Empty,
                    out _,
                    out _,
                    out var validation));
            Assert.That(validation.FirstErrorOrNull(), Does.Contain("CombatObjectSingleSourceV1"));
        }
        finally
        {
            Cleanup(def, preset, shape);
        }
    }

    [Test]
    public void ACD05_StaticPlusBetweenSamples_Rejected()
    {
        var (def, preset, shape) = MakeContactDefinition(explicitAuthoring: true);
        try
        {
            def.ActionContactAuthoring.BindingMode = ContactAnchorBindingMode.StaticAtWindowStart;
            def.ActionContactAuthoring.SweepPolicy = ContactSweepPolicy.BetweenSamples;
            Assert.IsFalse(
                ContactAuthoringAdapter.TryResolveContactAuthoring(
                    def,
                    LegacyContactOverrideAdapter.Empty,
                    out _,
                    out _,
                    out var validation));
            Assert.That(validation.FirstErrorOrNull(), Does.Contain("BetweenSamples"));
        }
        finally
        {
            Cleanup(def, preset, shape);
        }
    }

    [Test]
    public void ACD06_UnclassifiedLegacy_RejectedAtContactUseSite()
    {
        var def = ScriptableObject.CreateInstance<CombatObjectDefinitionSO>();
        try
        {
            Assert.IsFalse(
                ContactAuthoringAdapter.TryResolveContactAuthoring(
                    def,
                    LegacyContactOverrideAdapter.Empty,
                    out _,
                    out _,
                    out var validation));
            Assert.IsFalse(validation.IsValid);
        }
        finally
        {
            Object.DestroyImmediate(def);
        }
    }

    [Test]
    public void ACD07_OldAssetDefaultDoesNotEnterExplicitPath()
    {
        var (def, preset, shape) = MakeContactDefinition(explicitAuthoring: false);
        try
        {
            Assert.IsFalse(def.ActionContactAuthoring.UseExplicitData);
            Assert.AreEqual(
                ActionContactAuthoringVersion.LegacyPresetOverride,
                def.ActionContactAuthoring.Version);

            Assert.IsTrue(
                ContactAuthoringAdapter.TryResolveContactAuthoring(
                    def,
                    LegacyContactOverrideAdapter.Empty,
                    out var config,
                    out var info,
                    out var validation),
                validation.FirstErrorOrNull());

            Assert.IsTrue(config.UsesLegacyAuthoring);
            Assert.IsTrue(info.UsesLegacyAuthoring);
        }
        finally
        {
            Cleanup(def, preset, shape);
        }
    }

    [Test]
    public void ACD08_VNextQueryTargetNone_RejectedWithoutTargetFilterFallback()
    {
        var (def, preset, shape) = MakeContactDefinition(explicitAuthoring: true);
        try
        {
            var query = def.QueryPolicy;
            query.Target = default;
            def.QueryPolicy = query;
            def.TargetFilter = default;

            var validation = CombatObjectDefinitionValidator.Validate(
                def,
                CombatDefinitionUseSite.ContactEvent);
            Assert.IsFalse(validation.IsValid);
            Assert.That(validation.FirstErrorOrNull(), Does.Contain("TargetProfile"));
        }
        finally
        {
            Cleanup(def, preset, shape);
        }
    }

    [Test]
    public void ACD_MixedWritableOverrideOnExplicit_Fails()
    {
        var (def, preset, shape) = MakeContactDefinition(explicitAuthoring: true);
        try
        {
            var legacy = new ContactOverrideData { OverridePlacement = true, LocalOffset = Vector3.one };
            Assert.IsFalse(
                ContactAuthoringAdapter.TryResolveContactAuthoring(
                    def,
                    LegacyContactOverrideAdapter.From(in legacy),
                    out _,
                    out _,
                    out var validation));
            Assert.That(validation.FirstErrorOrNull(), Does.Contain("mix"));
        }
        finally
        {
            Cleanup(def, preset, shape);
        }
    }

    static (CombatObjectDefinitionSO def, AttackShapePresetSO preset, SphereShapeSO shape)
        MakeContactDefinition(bool explicitAuthoring)
    {
        var shape = ScriptableObject.CreateInstance<SphereShapeSO>();
        var preset = ScriptableObject.CreateInstance<AttackShapePresetSO>();
        var def = ScriptableObject.CreateInstance<CombatObjectDefinitionSO>();
        preset.Geometry = shape;
        def.Archetype = CombatObjectArchetype.ActionContact;
        def.SchemaVersion = CombatObjectSchemaVersion.ArchetypeV2;
        def.MigrationState = CombatObjectMigrationState.Classified;
        def.ShapePreset = preset;
        def.AttackProfile = CombatAttackProfile.Default;
        def.QueryPolicy = ContactQueryPolicy.Default;
        def.HitPolicy = HitPolicyParams.Default;
        def.DefinitionRevision = 1;
        if (explicitAuthoring)
        {
            def.ActionContactAuthoring = ActionContactAuthoringData.CreateNewV1();
        }

        return (def, preset, shape);
    }

    static void Cleanup(CombatObjectDefinitionSO def, AttackShapePresetSO preset, SphereShapeSO shape)
    {
        Object.DestroyImmediate(def);
        Object.DestroyImmediate(preset);
        Object.DestroyImmediate(shape);
    }
}

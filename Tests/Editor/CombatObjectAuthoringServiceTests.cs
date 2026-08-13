#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>224.1 L2 — Authoring Service / ChangeBus 合同（ACS-*）。</summary>
public sealed class CombatObjectAuthoringServiceTests
{
    [Test]
    public void ACS01_ChangeBinding_OnlyMutatesCo_AndBumpsRevision()
    {
        var def = MakeExplicitDefinition();
        try
        {
            var before = def.DefinitionRevision;
            Assert.IsTrue(
                CombatObjectAuthoringService.TryChangeBinding(
                    def,
                    ContactAnchorBindingMode.StaticAtWindowStart,
                    ContactOriginChangeMode.KeepLocalPose,
                    null,
                    out var failure),
                failure);
            Assert.AreEqual(ContactAnchorBindingMode.StaticAtWindowStart, def.ActionContactAuthoring.BindingMode);
            Assert.AreEqual(before + 1, def.DefinitionRevision);
        }
        finally
        {
            Object.DestroyImmediate(def);
        }
    }

    [Test]
    public void ACS03_KeepWorldPose_WithoutContext_Fails()
    {
        var def = MakeExplicitDefinition();
        try
        {
            Assert.IsFalse(
                CombatObjectAuthoringService.TryChangeOrigin(
                    def,
                    ContactAnchorReference.DefaultStatic,
                    ContactOriginChangeMode.KeepWorldPose,
                    context: null,
                    out var failure));
            Assert.That(failure, Does.Contain("preview anchor"));
        }
        finally
        {
            Object.DestroyImmediate(def);
        }
    }

    [Test]
    public void ACS04_AutoFollow_DefaultsHandR_AutoStatic_DefaultsRoot()
    {
        var def = MakeExplicitDefinition();
        try
        {
            def.ActionContactAuthoring.OriginPolicy = ContactOriginPolicy.Auto;
            Assert.IsTrue(
                CombatObjectAuthoringService.TryChangeBinding(
                    def,
                    ContactAnchorBindingMode.FollowAnchor,
                    ContactOriginChangeMode.KeepLocalPose,
                    null,
                    out _));
            Assert.AreEqual(SpawnSource.SelfHandR, def.ActionContactAuthoring.Origin.Source);

            Assert.IsTrue(
                CombatObjectAuthoringService.TryChangeBinding(
                    def,
                    ContactAnchorBindingMode.StaticAtWindowStart,
                    ContactOriginChangeMode.KeepLocalPose,
                    null,
                    out _));
            Assert.AreEqual(SpawnSource.SelfRootBone, def.ActionContactAuthoring.Origin.Source);
        }
        finally
        {
            Object.DestroyImmediate(def);
        }
    }

    [Test]
    public void ACS05_ExplicitOrigin_PreservedAcrossBindingWhenRemembered()
    {
        var def = MakeExplicitDefinition();
        try
        {
            Assert.IsTrue(
                CombatObjectAuthoringService.TryChangeOrigin(
                    def,
                    ContactAnchorReference.FromSpawnSource(SpawnSource.SelfHandL),
                    ContactOriginChangeMode.KeepLocalPose,
                    null,
                    out _));
            Assert.IsTrue(
                CombatObjectAuthoringService.TryChangeBinding(
                    def,
                    ContactAnchorBindingMode.StaticAtWindowStart,
                    ContactOriginChangeMode.KeepLocalPose,
                    null,
                    out _));
            Assert.IsTrue(
                CombatObjectAuthoringService.TryChangeBinding(
                    def,
                    ContactAnchorBindingMode.FollowAnchor,
                    ContactOriginChangeMode.KeepLocalPose,
                    null,
                    out _));
            Assert.AreEqual(SpawnSource.SelfHandL, def.ActionContactAuthoring.Origin.Source);
        }
        finally
        {
            Object.DestroyImmediate(def);
        }
    }

    [Test]
    public void ACS06_ChangeBus_PublishesOncePerEdit()
    {
        var def = MakeExplicitDefinition();
        var count = 0;
        void Handler(CombatAuthoringChange change) => count++;
        CombatAuthoringChangeBus.Changed += Handler;
        try
        {
            CombatObjectAuthoringService.TryChangeLocalPose(def, Vector3.one, Vector3.zero, out _);
            Assert.AreEqual(1, count);
            Assert.AreEqual(2, def.DefinitionRevision);
        }
        finally
        {
            CombatAuthoringChangeBus.Changed -= Handler;
            Object.DestroyImmediate(def);
        }
    }

    [Test]
    public void ACS07_DuplicateForContact_DoesNotCopyShapeAssetIdentityRequirement()
    {
        var shape = ScriptableObject.CreateInstance<SphereShapeSO>();
        var preset = ScriptableObject.CreateInstance<AttackShapePresetSO>();
        var def = MakeExplicitDefinition();
        try
        {
            preset.Geometry = shape;
            def.ShapePreset = preset;
            var copy = CombatObjectAuthoringService.DuplicateForContact(def, null, null);
            Assert.IsNotNull(copy);
            Assert.AreNotEqual(def.Id, copy.Id);
            Assert.AreSame(def.ShapePreset, copy.ShapePreset, "Variant shares ShapePreset reference by design.");
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(copy));
        }
        finally
        {
            Object.DestroyImmediate(def);
            Object.DestroyImmediate(preset);
            Object.DestroyImmediate(shape);
        }
    }

    static CombatObjectDefinitionSO MakeExplicitDefinition()
    {
        var def = ScriptableObject.CreateInstance<CombatObjectDefinitionSO>();
        def.Archetype = CombatObjectArchetype.ActionContact;
        def.SchemaVersion = CombatObjectSchemaVersion.ArchetypeV2;
        def.MigrationState = CombatObjectMigrationState.Classified;
        def.Id = "test_contact";
        def.DefinitionRevision = 1;
        def.ActionContactAuthoring = ActionContactAuthoringData.CreateNewV1();
        def.QueryPolicy = ContactQueryPolicy.Default;
        def.HitPolicy = HitPolicyParams.Default;
        def.AttackProfile = CombatAttackProfile.Default;
        return def;
    }
}
#endif

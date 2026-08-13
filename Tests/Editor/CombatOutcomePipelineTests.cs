using NUnit.Framework;
using UnityEngine;

public sealed class CombatOutcomePipelineTests
{
    sealed class OutcomeTestEntity : Entity
    {
    }

    [Test]
    public void CandidateBufferAggregatesEntityAndSortsNearestFirst()
    {
        var nearObject = new GameObject("Near");
        var farObject = new GameObject("Far");
        try
        {
            var near = nearObject.AddComponent<OutcomeTestEntity>();
            var nearA = nearObject.AddComponent<SphereCollider>();
            var nearB = nearObject.AddComponent<BoxCollider>();
            nearObject.transform.position = Vector3.forward;

            var far = farObject.AddComponent<OutcomeTestEntity>();
            var farCollider = farObject.AddComponent<SphereCollider>();
            farObject.transform.position = Vector3.forward * 5f;

            var query = ContactQueryPolicy.Default;
            var buffer = new ContactCandidateBuffer(8);
            Assert.IsTrue(buffer.TryAdd(
                farCollider,
                null,
                in query,
                farObject.transform.position,
                Vector3.back));
            Assert.IsTrue(buffer.TryAdd(
                nearA,
                null,
                in query,
                nearObject.transform.position,
                Vector3.back));
            Assert.IsFalse(buffer.TryAdd(
                nearB,
                null,
                in query,
                nearObject.transform.position,
                Vector3.back));

            buffer.SortStable(Vector3.zero);
            Assert.AreEqual(2, buffer.Count);
            Assert.AreSame(near, buffer[0].Target);
            Assert.AreSame(far, buffer[1].Target);
        }
        finally
        {
            Object.DestroyImmediate(nearObject);
            Object.DestroyImmediate(farObject);
        }
    }

    [Test]
    public void LegacySelfOnlyMapsToExplicitSelfProfile()
    {
        var legacy = new TargetFilterParams
        {
            Kind = TargetFilterKind.SelfOnly,
            IncludeDead = false,
        };
        var profile = LegacyTargetProfileAdapter.Convert(in legacy);
        Assert.AreEqual(AllegianceMask.Self, profile.Relations);
        Assert.AreEqual(SelfHitPolicy.Allow, profile.SelfHit);
    }

    [Test]
    public void HealOutcomeNeverUsesNegativeDamage()
    {
        var definition = ScriptableObject.CreateInstance<DamageDefinitionSO>();
        try
        {
            definition.Kind = DamageKind.Heal;
            definition.Amount = 25f;
            var outcome = CombatOutcomeBuilder.FromDamageDefinition(definition);
            Assert.AreEqual(0f, outcome.BaseDamage);
            Assert.AreEqual(25f, outcome.HealAmount);
        }
        finally
        {
            Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void AreaCapabilityDoesNotBecomeClashable()
    {
        var reaction = HitReaction.Default;
        var outcome = CombatOutcomeBuilder.FromHitReaction(in reaction);
        var capabilities = CombatOutcomeBuilder.ResolveCapabilities(
            CombatExecutionModel.SpawnedFinite,
            CombatObjectArchetype.Area,
            HitShapeMode.Volume,
            in outcome);
        Assert.AreEqual(0, capabilities & CombatCapability.Clashable);
    }
}

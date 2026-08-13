using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class SpawnedCombatWorldServiceTests
{
    [Test]
    public void LegacyDurationZeroMapsToExactlyOneSample()
    {
        var owned = MakeDefinition(duration: 0f);
        try
        {
            var world = new SpawnedCombatWorldService();
            var samples = 0;
            var terminations = 0;
            world.SampleDue += _ => samples++;
            world.Terminated += fact =>
            {
                terminations++;
                Assert.AreEqual(
                    SpawnedCombatTerminationReason.OneSampleCompleted,
                    fact.Reason);
            };

            var result = world.Submit(MakeRequest(owned.Definition));
            Assert.IsTrue(result.Accepted, result.Message);
            world.Tick(0.5f);

            Assert.AreEqual(1, samples);
            Assert.AreEqual(1, terminations);
            Assert.AreEqual(0, world.ActiveCount);
        }
        finally
        {
            owned.Dispose();
        }
    }

    [Test]
    public void RecycledSlotIncrementsGenerationAndRejectsStaleHandle()
    {
        var owned = MakeDefinition(duration: 0f);
        try
        {
            var world = new SpawnedCombatWorldService();
            var firstSubmit = world.Submit(MakeRequest(owned.Definition));
            world.Tick(0.01f);
            Assert.IsTrue(world.TryConsumeTicket(firstSubmit.Ticket, out var first));

            var secondSubmit = world.Submit(MakeRequest(owned.Definition));
            world.Tick(0.01f);
            Assert.IsTrue(world.TryConsumeTicket(secondSubmit.Ticket, out var second));

            Assert.AreEqual(first.Slot, second.Slot);
            Assert.Greater(second.Generation, first.Generation);
            Assert.IsFalse(world.TryResolve(first, out _));
        }
        finally
        {
            owned.Dispose();
        }
    }

    [Test]
    public void TerminationChildrenPropagateDepthAndStopAtBudget()
    {
        var owned = MakeDefinition(duration: 0f);
        try
        {
            owned.Definition.Lifecycle.OnExpireSpawn = owned.Definition;
            var world = new SpawnedCombatWorldService(maxLineageDepth: 2);
            var depths = new List<int>();
            world.Terminated += fact => depths.Add(fact.Lineage.Depth);

            Assert.IsTrue(world.Submit(MakeRequest(owned.Definition)).Accepted);
            for (var i = 0; i < 5; i++)
            {
                world.Tick(0.01f);
            }

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, depths);
            Assert.AreEqual(0, world.ActiveCount);
            Assert.AreEqual(0, world.PendingCount);
        }
        finally
        {
            owned.Dispose();
        }
    }

    [Test]
    public void CatchUpIsBoundedAndReportsSkippedSamples()
    {
        var owned = MakeDefinition(duration: 10f);
        try
        {
            owned.Definition.SpawnedPolicy = new SpawnedRuntimePolicyAuthoring
            {
                UseExplicitPolicy = true,
                LifetimeKind = SpawnedLifetimeKind.Timed,
                DurationSeconds = 10f,
                SamplingKind = SpawnedSamplingKind.FixedInterval,
                SamplingIntervalSeconds = 0.1f,
                CatchUpPolicy = SpawnedCatchUpPolicy.PreservePhaseClamp,
                MaxCatchUpSamplesPerTick = 2,
                SourceInvalidation = SpawnSourceInvalidationPolicy.DetachKeepSnapshot,
            };

            var world = new SpawnedCombatWorldService();
            var submit = world.Submit(MakeRequest(owned.Definition));
            Assert.IsTrue(submit.Accepted, submit.Message);
            world.Tick(1f);
            Assert.IsTrue(world.TryConsumeTicket(submit.Ticket, out var handle));
            Assert.IsTrue(world.TryResolve(handle, out var runtime));
            Assert.AreEqual(2, runtime.SampleSequence);
            Assert.Greater(runtime.SkippedSampleCount, 0);
        }
        finally
        {
            owned.Dispose();
        }
    }

    [Test]
    public void SceneUnloadNeverDerivesTerminationChild()
    {
        var owned = MakeDefinition(duration: 10f);
        try
        {
            owned.Definition.Lifecycle.OnExpireSpawn = owned.Definition;
            var world = new SpawnedCombatWorldService();
            SpawnedCombatTerminationFact fact = default;
            world.Terminated += value => fact = value;

            Assert.IsTrue(world.Submit(MakeRequest(owned.Definition)).Accepted);
            world.Tick(0.01f);
            world.ChangeWorld(sceneHandle: 9);

            Assert.AreEqual(SpawnedCombatTerminationReason.SceneUnload, fact.Reason);
            Assert.IsFalse(fact.ChildQueued);
            Assert.AreEqual(0, world.PendingCount);
        }
        finally
        {
            owned.Dispose();
        }
    }

    [Test]
    public void CurveUsesNormalizedLifetimeDomain()
    {
        var owned = MakeDefinition(duration: 2f);
        try
        {
            owned.Definition.Movement = new MovementParams
            {
                Kind = MovementKind.Curve,
                LocalOffsetZOverTime = AnimationCurve.Linear(0f, 0f, 1f, 10f),
            };
            owned.Definition.SpawnedPolicy = new SpawnedRuntimePolicyAuthoring
            {
                UseExplicitPolicy = true,
                LifetimeKind = SpawnedLifetimeKind.Timed,
                DurationSeconds = 2f,
                SamplingKind = SpawnedSamplingKind.FixedInterval,
                SamplingIntervalSeconds = 1f,
                CatchUpPolicy = SpawnedCatchUpPolicy.PreservePhaseClamp,
                MaxCatchUpSamplesPerTick = 4,
                SourceInvalidation = SpawnSourceInvalidationPolicy.DetachKeepSnapshot,
            };

            var positions = new List<Vector3>();
            var world = new SpawnedCombatWorldService();
            world.SampleDue += fact => positions.Add(fact.Position);
            Assert.IsTrue(world.Submit(MakeRequest(owned.Definition)).Accepted);
            world.Tick(1f);

            Assert.AreEqual(2, positions.Count);
            Assert.AreEqual(5f, positions[1].z, 0.001f);
        }
        finally
        {
            owned.Dispose();
        }
    }

    [Test]
    public void LinearTravelLimitTerminatesAtSameBoundary()
    {
        var owned = MakeDefinition(duration: 10f);
        try
        {
            owned.Definition.Movement = MovementParams.DefaultLinear(10f, 3f);
            SpawnedCombatTerminationFact termination = default;
            var world = new SpawnedCombatWorldService();
            world.Terminated += fact => termination = fact;

            Assert.IsTrue(world.Submit(MakeRequest(owned.Definition)).Accepted);
            world.Tick(1f);

            Assert.AreEqual(
                SpawnedCombatTerminationReason.TravelLimit,
                termination.Reason);
            Assert.AreEqual(3f, termination.Position.z, 0.001f);
        }
        finally
        {
            owned.Dispose();
        }
    }

    [Test]
    public void ExpandKeepsBaseShapeCapability()
    {
        var owned = MakeDefinition(duration: 1f);
        try
        {
            var box = ScriptableObject.CreateInstance<BoxShapeSO>();
            owned.Definition.Shape = box;
            owned.Definition.Movement = new MovementParams
            {
                Kind = MovementKind.Expand,
                StartRadius = 1f,
                EndRadius = 2f,
            };

            Assert.IsTrue(CombatObjectSpecResolver.TryResolveSpawned(
                owned.Definition,
                out var spec,
                out var validation), validation.FirstErrorOrNull());
            Assert.AreEqual(MovementKind.Static, spec.Spatial.MotionKind);
            Assert.AreEqual(
                SpawnedGeometryEvolutionKind.LegacyExpand,
                spec.Spatial.GeometryEvolution);
            Assert.IsInstanceOf<BoxShapeSO>(spec.Geometry);
            owned.Definition.Shape = owned.Shape;
            Object.DestroyImmediate(box);
        }
        finally
        {
            owned.Dispose();
        }
    }

    [Test]
    public void GroundUnderTargetWithoutTargetIsRejected()
    {
        var owned = MakeDefinition(duration: 1f);
        try
        {
            var request = MakeRequest(
                owned.Definition,
                SpawnSource.GroundUnderTarget);
            var result = new SpawnedCombatWorldService().Submit(in request);
            Assert.IsFalse(result.Accepted);
        }
        finally
        {
            owned.Dispose();
        }
    }

    [Test]
    public void WorldMetricsRecordAcceptedSampleAndCapacityFacts()
    {
        var owned = MakeDefinition(duration: 1f);
        try
        {
            var world = new SpawnedCombatWorldService();
            var request = MakeRequest(owned.Definition);

            var submit = world.Submit(in request);
            Assert.IsTrue(submit.Accepted);
            world.Tick(0.02f);

            var metrics = world.Metrics.Snapshot();
            Assert.AreEqual(1L, metrics.SubmittedRequests);
            Assert.AreEqual(1L, metrics.AcceptedRequests);
            Assert.AreEqual(0L, metrics.RejectedRequests);
            Assert.AreEqual(1, metrics.PeakActive);
            Assert.AreEqual(1L, metrics.QuerySamples);
            Assert.AreEqual(0L, metrics.BufferSaturations);
        }
        finally
        {
            owned.Dispose();
        }
    }

    [Test]
    public void WorldMetricsRecordRejectedRequest()
    {
        var world = new SpawnedCombatWorldService();
        var request = MakeRequest(null);

        var submit = world.Submit(in request);

        Assert.IsFalse(submit.Accepted);
        var metrics = world.Metrics.Snapshot();
        Assert.AreEqual(1L, metrics.SubmittedRequests);
        Assert.AreEqual(0L, metrics.AcceptedRequests);
        Assert.AreEqual(1L, metrics.RejectedRequests);
    }

    [Test]
    public void V2ProjectileWorldRunsWithoutLegacyDefinitionFields()
    {
        var definition = ScriptableObject.CreateInstance<CombatObjectDefinitionSO>();
        var shape = ScriptableObject.CreateInstance<SphereShapeSO>();
        try
        {
            var data = SpawnedCombatAuthoringData.Default;
            data.Geometry = shape;
            data.Outcome = CombatOutcomeProfile.FromReaction(new HitReaction
            {
                BaseDamage = 10f,
            });
            data.Motion = MovementParams.DefaultStatic;
            data.RuntimePolicy = new SpawnedRuntimePolicyAuthoring
            {
                UseExplicitPolicy = true,
                LifetimeKind = SpawnedLifetimeKind.Timed,
                DurationSeconds = 1f,
                SamplingKind = SpawnedSamplingKind.OneAtStart,
                SamplingIntervalSeconds = 0f,
                CatchUpPolicy = SpawnedCatchUpPolicy.PreservePhaseClamp,
                MaxCatchUpSamplesPerTick = 1,
                SourceInvalidation = SpawnSourceInvalidationPolicy.Terminate,
            };
            definition.Archetype = CombatObjectArchetype.Projectile;
            definition.SchemaVersion = CombatObjectSchemaVersion.ArchetypeV2;
            definition.MigrationState = CombatObjectMigrationState.Migrated;
            definition.SpawnedData = data;

            var world = new SpawnedCombatWorldService();
            var request = MakeRequest(definition);
            var submit = world.Submit(in request);

            Assert.IsTrue(submit.Accepted);
            world.Tick(0.02f);
            Assert.AreEqual(1L, world.Metrics.Snapshot().QuerySamples);
        }
        finally
        {
            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(shape);
        }
    }

    static CombatSpawnRequest MakeRequest(
        CombatObjectDefinitionSO definition,
        SpawnSource placement = SpawnSource.AtSelfPosition)
    {
        var lineage = default(SpawnLineageContext);
        return new CombatSpawnRequest(
            definition,
            null,
            null,
            placement,
            Vector3.zero,
            Quaternion.identity,
            Vector3.forward,
            null,
            "test-event",
            1u,
            in lineage,
            CombatSpawnCause.External,
            "test");
    }

    static OwnedDefinition MakeDefinition(float duration)
    {
        var definition = ScriptableObject.CreateInstance<CombatObjectDefinitionSO>();
        var shape = ScriptableObject.CreateInstance<SphereShapeSO>();
        var damage = ScriptableObject.CreateInstance<DamageDefinitionSO>();
        definition.Archetype = CombatObjectArchetype.Projectile;
        definition.SchemaVersion = CombatObjectSchemaVersion.ArchetypeV1;
        definition.MigrationState = CombatObjectMigrationState.Classified;
        definition.Shape = shape;
        definition.Damage = damage;
        definition.Lifecycle = LifecycleParams.DefaultMeleeOneShot;
        definition.Lifecycle.Duration = duration;
        definition.Movement = MovementParams.DefaultStatic;
        return new OwnedDefinition(definition, shape, damage);
    }

    readonly struct OwnedDefinition
    {
        public readonly CombatObjectDefinitionSO Definition;
        public readonly HitShapeSO Shape;
        public readonly DamageDefinitionSO Damage;

        public OwnedDefinition(
            CombatObjectDefinitionSO definition,
            HitShapeSO shape,
            DamageDefinitionSO damage)
        {
            Definition = definition;
            Shape = shape;
            Damage = damage;
        }

        public void Dispose()
        {
            Object.DestroyImmediate(Shape);
            Object.DestroyImmediate(Damage);
            Object.DestroyImmediate(Definition);
        }
    }
}

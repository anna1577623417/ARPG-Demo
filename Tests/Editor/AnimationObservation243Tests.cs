using NUnit.Framework;
using UnityEngine;

public sealed class AnimationObservation243Tests
{
    [Test]
    public void PlayerObservationSource_CapturesKnownReadOnlyFactsAndLeavesUnavailableFactsUnknown()
    {
        var gameObject = new GameObject("PlayerObservationSource243Tests");
        try
        {
            var player = gameObject.AddComponent<Player>();
            var source = new PlayerAnimationObservationSource243(player);

            Assert.IsTrue(source.TryCapture(out var observation));
            Assert.AreEqual(player.GetInstanceID(), observation.EntityInstanceId);
            Assert.IsTrue(observation.IsKnown(AnimationObservationKnownMask.Entity));
            Assert.IsTrue(observation.IsKnown(AnimationObservationKnownMask.ActionLease));
            Assert.IsTrue(observation.IsKnown(AnimationObservationKnownMask.Grounded));
            Assert.IsFalse(observation.IsKnown(AnimationObservationKnownMask.Stance));
            Assert.IsFalse(observation.IsKnown(AnimationObservationKnownMask.WeaponClass));
            Assert.AreEqual(0u, observation.ActionLeaseVersion);
            Assert.AreEqual(1UL, observation.ObservationSequence);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    static AnimationObservation Observation(
        AnimationObservationKnownMask mask = AnimationObservationKnownMask.Entity | AnimationObservationKnownMask.Grounded,
        float verticalSpeed = 0f,
        int schemaVersion = AnimationObservation.CurrentSchemaVersion)
    {
        return new AnimationObservation(
            7, 12UL, 2UL, "Locomotion", "Walk", 3U, 4UL, true, verticalSpeed,
            Vector2.right, Vector3.forward, Vector3.forward, Vector3.forward, "Default", "Sword", mask, schemaVersion);
    }

    [Test]
    public void UnknownFieldsRemainExplicitInsteadOfInferredFromDefaults()
    {
        var observation = Observation(AnimationObservationKnownMask.Entity);

        Assert.IsTrue(observation.IsKnown(AnimationObservationKnownMask.Entity));
        Assert.IsFalse(observation.IsKnown(AnimationObservationKnownMask.Grounded));
        Assert.IsFalse(observation.IsKnown(AnimationObservationKnownMask.ActionLease));
    }

    [Test]
    public void FiniteValidationRejectsNaN()
    {
        var observation = Observation(verticalSpeed: float.NaN);

        Assert.IsFalse(observation.HasFiniteNumbers());
    }

    [Test]
    public void SchemaSupportIsExact()
    {
        Assert.IsTrue(Observation().IsSchemaSupported);
        Assert.IsFalse(Observation(schemaVersion: AnimationObservation.CurrentSchemaVersion + 1).IsSchemaSupported);
    }
}

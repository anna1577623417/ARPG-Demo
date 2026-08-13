using NUnit.Framework;
using UnityEngine;

public sealed class ActionContactRuntimeContractTests
{
    [Test]
    public void ContactTrackIsSelectedWhenContactDataExists()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        try
        {
            action.ContactEvents.Add(new ContactEvent
            {
                EventId = ContactEventId.NewId(),
                ActiveStart = 0.2f,
                ActiveEnd = 0.4f,
            });

            Assert.AreEqual(
                ActionAttackTrackKind.Contact,
                ActionAttackTrackRuntimePolicy.Select(action));
        }
        finally
        {
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void FactIdentitySurvivesHitResultAdapter()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        try
        {
            var eventId = ContactEventId.NewId();
            var fact = new ContactFact(
                null,
                null,
                action,
                eventId,
                17u,
                4,
                3,
                HitShapeMode.Volume,
                ContactMotionKind.SweepBetweenFrames,
                Vector3.one,
                Vector3.up,
                "Chest",
                2,
                0.1f);

            var hit = ContactFactHitResultAdapter.ToHitResult(in fact);

            Assert.IsTrue(hit.HasContactFact);
            Assert.AreEqual(eventId, hit.ContactFact.ContactEventId);
            Assert.AreEqual(17u, hit.ContactFact.ActionLeaseVersion);
            Assert.AreEqual(4, hit.ContactFact.SampleId);
            Assert.AreSame(action, hit.ContactFact.Action);
        }
        finally
        {
            Object.DestroyImmediate(action);
        }
    }
}

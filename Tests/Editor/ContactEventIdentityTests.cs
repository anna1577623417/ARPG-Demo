using NUnit.Framework;
using UnityEngine;

public sealed class ContactEventIdentityTests
{
    [Test]
    public void NewId_IsStableGuidShapeAndUnique()
    {
        var a = ContactEventId.NewId();
        var b = ContactEventId.NewId();

        Assert.IsTrue(ContactEventId.IsValid(a));
        Assert.IsTrue(ContactEventId.IsValid(b));
        Assert.AreNotEqual(a, b);
    }

    [Test]
    public void ReorderingEvents_DoesNotChangeIdentity()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        try
        {
            var first = new ContactEvent { EventId = ContactEventId.NewId(), DebugName = "A" };
            var second = new ContactEvent { EventId = ContactEventId.NewId(), DebugName = "B" };
            action.ContactEvents.Add(first);
            action.ContactEvents.Add(second);

            action.ContactEvents.Reverse();

            Assert.AreEqual(second.EventId, action.ContactEvents[0].EventId);
            Assert.AreEqual(first.EventId, action.ContactEvents[1].EventId);
        }
        finally
        {
            Object.DestroyImmediate(action);
        }
    }
}

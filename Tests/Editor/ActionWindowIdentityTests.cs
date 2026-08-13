using NUnit.Framework;
using UnityEngine;

/// <summary>224.1 L4 — WindowId / Contact 时间绑定（ACW-*）。</summary>
public sealed class ActionWindowIdentityTests
{
    [Test]
    public void ACW01_NewId_IsUniqueAndValid()
    {
        var a = ActionWindowIdentity.NewId();
        var b = ActionWindowIdentity.NewId();
        Assert.IsTrue(ActionWindowIdentity.IsValid(a));
        Assert.IsTrue(ActionWindowIdentity.IsValid(b));
        Assert.AreNotEqual(a, b);
    }

    [Test]
    public void ACW02_FindById_SurvivesReorder()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        try
        {
            var id = ActionWindowIdentity.NewId();
            action.Windows.Add(new ActionWindow
            {
                WindowId = id,
                DisplayName = "A",
                NormalizedStart = 0.1f,
                NormalizedEnd = 0.2f,
            });
            action.Windows.Add(new ActionWindow
            {
                WindowId = ActionWindowIdentity.NewId(),
                DisplayName = "B",
                NormalizedStart = 0.3f,
                NormalizedEnd = 0.4f,
            });
            action.Windows.Reverse();
            Assert.AreEqual(1, ActionWindowIdentity.FindIndex(action, id));
        }
        finally
        {
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void ACW03_WindowIdBound_UsesWindowRangeNotContactLegacy()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        try
        {
            var windowId = ActionWindowIdentity.NewId();
            action.WindowAuthoringVersion = ActionWindowAuthoringVersion.WindowIdBoundContactV1;
            action.Windows.Add(new ActionWindow
            {
                WindowId = windowId,
                NormalizedStart = 0.2f,
                NormalizedEnd = 0.5f,
            });
            var contact = new ContactEvent
            {
                EventId = ContactEventId.NewId(),
                WindowId = windowId,
                ActiveStart = 0f,
                ActiveEnd = 1f,
            };

            Assert.IsTrue(
                ActionWindowResolver.TryResolveContactWindow(
                    action, in contact, out var window, out var info));
            Assert.IsFalse(info.UsesLegacyRange);
            Assert.AreEqual(0.2f, window.NormalizedStart);
            Assert.AreEqual(0.5f, window.NormalizedEnd);
        }
        finally
        {
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void ACW08_LegacyRange_StillWorksWithoutWindowId()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        try
        {
            action.WindowAuthoringVersion = ActionWindowAuthoringVersion.LegacyRangeOnContact;
            var contact = new ContactEvent
            {
                EventId = ContactEventId.NewId(),
                ActiveStart = 0.1f,
                ActiveEnd = 0.3f,
            };
            Assert.IsTrue(
                ActionWindowResolver.TryResolveContactWindow(
                    action, in contact, out var window, out var info));
            Assert.IsTrue(info.UsesLegacyRange);
            Assert.IsTrue(info.NeedsMigration);
            Assert.AreEqual(0.1f, window.NormalizedStart);
            Assert.AreEqual(0.3f, window.NormalizedEnd);
        }
        finally
        {
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void ACW09_DanglingWindowId_FailsHard()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        try
        {
            action.WindowAuthoringVersion = ActionWindowAuthoringVersion.WindowIdBoundContactV1;
            var contact = new ContactEvent
            {
                EventId = ContactEventId.NewId(),
                WindowId = ActionWindowIdentity.NewId(),
            };
            Assert.IsFalse(
                ActionWindowResolver.TryResolveContactWindow(
                    action, in contact, out _, out var info));
            Assert.That(info.Message, Does.Contain("dangling"));
        }
        finally
        {
            Object.DestroyImmediate(action);
        }
    }
}

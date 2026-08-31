using NUnit.Framework;
using UnityEngine;

public sealed class AnimationTransitionLegacyImporter244Tests
{
    [Test]
    public void ScanSelectedGraph_UsesExplicitSourcesAndBuildsPreviewWithoutWriting()
    {
        var graph = ScriptableObject.CreateInstance<AnimTransitionAuthoringGraph>();
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        var clip = new AnimationClip { name = "attack" };
        action.MainClip = clip;
        action.CrossfadeTime = 0.12f;
        var before = action.CrossfadeTime;
        var binding = new LocomotionStateBinding { LocomotionAction = action };

        try
        {
            graph.EditorSetDomain(AnimTransitionGraphDomain.Action);
            var report = AnimationTransitionLegacyImporter244.ScanSelectedGraph(
                graph,
                new[] { action },
                new[] { binding },
                null);
            var preview = AnimationTransitionLegacyImporter244.BuildPreview(report);

            Assert.AreEqual(1, report.Entries.Count);
            Assert.IsFalse(report.HasConflicts);
            Assert.AreEqual(1, preview.Count);
            Assert.AreEqual("attack", preview[0].Key);
            Assert.AreEqual(AnimationRequestDomain.Action, preview[0].Domain);
            Assert.AreEqual(before, action.CrossfadeTime);
            Assert.IsFalse(report.CanApply);
        }
        finally
        {
            Object.DestroyImmediate(clip);
            Object.DestroyImmediate(action);
            Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void ScanSelectedGraph_BlocksConflictingDurationsWithoutAveraging()
    {
        var graph = ScriptableObject.CreateInstance<AnimTransitionAuthoringGraph>();
        var first = ScriptableObject.CreateInstance<ActionDataSO>();
        var second = ScriptableObject.CreateInstance<ActionDataSO>();
        var firstClip = new AnimationClip { name = "same-key" };
        var secondClip = new AnimationClip { name = "same-key" };
        first.MainClip = firstClip;
        second.MainClip = secondClip;
        first.CrossfadeTime = 0.08f;
        second.CrossfadeTime = 0.2f;

        try
        {
            graph.EditorSetDomain(AnimTransitionGraphDomain.Action);
            var report = AnimationTransitionLegacyImporter244.ScanSelectedGraph(
                graph,
                new[] { first, second },
                null,
                null);
            var preview = AnimationTransitionLegacyImporter244.BuildPreview(report);

            Assert.IsTrue(report.HasConflicts);
            Assert.AreEqual(2, report.Entries.Count);
            Assert.AreEqual(0, preview.Count);
            Assert.That(report.Findings.ToArray(), Has.Some.Contains("Conflict blocked"));
        }
        finally
        {
            Object.DestroyImmediate(firstClip);
            Object.DestroyImmediate(secondClip);
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void ScanSelectedGraph_NullSelectionDoesNotReadOrWriteSources()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.CrossfadeTime = 0.17f;
        try
        {
            var report = AnimationTransitionLegacyImporter244.ScanSelectedGraph(
                null,
                new[] { action },
                null,
                null);
            Assert.AreEqual(0, report.Entries.Count);
            Assert.AreEqual(0.17f, action.CrossfadeTime);
            Assert.IsFalse(report.CanApply);
        }
        finally
        {
            Object.DestroyImmediate(action);
        }
    }
}

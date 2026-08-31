using System.Collections.Generic;
using System.IO;
using GraphProcessor;
using NUnit.Framework;
using UnityEngine;

public sealed class AnimTransitionGraphEditor243Tests
{
    [Test]
    public void Layout_UsesStableColumnsWithoutChangingGraphConfiguration()
    {
        var graph = ScriptableObject.CreateInstance<AnimTransitionAuthoringGraph>();
        try
        {
            var entry = new AnimGraphEntryNode { GUID = "entry" };
            var output = new AnimGraphOutputNode { GUID = "output" };
            graph.AddNode(output);
            graph.AddNode(entry);

            AnimTransitionLayoutService.Layout(graph);

            Assert.Less(entry.position.x, output.position.x);
            Assert.AreEqual("entry", entry.GUID);
            Assert.AreEqual("output", output.GUID);
        }
        finally
        {
            Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void SelectionController_ClearRemovesCommittedSelection()
    {
        var controller = new AnimTransitionGraphSelectionController();
        controller.Clear();
        Assert.AreEqual(0, controller.Current.Count);
        Assert.AreEqual(0, controller.NodeGuids.Count);
        Assert.IsNull(controller.Primary);
    }

    [Test]
    public void History_CreateUndoRedoRestoresTheSameNodeGuid()
    {
        var graph = ScriptableObject.CreateInstance<AnimTransitionAuthoringGraph>();
        var history = new AnimTransitionGraphHistory();
        try
        {
            var command = AnimTransitionCreateNodeCommand.Create<AnimGraphEntryNode>(new Vector2(12f, 24f));
            Assert.IsTrue(history.Execute(graph, command));
            Assert.AreEqual(1, graph.nodes.Count);
            var guid = graph.nodes[0].GUID;

            Assert.IsTrue(history.Undo(graph));
            Assert.AreEqual(0, graph.nodes.Count);

            Assert.IsTrue(history.Redo(graph));
            Assert.AreEqual(1, graph.nodes.Count);
            Assert.AreEqual(guid, graph.nodes[0].GUID);
        }
        finally
        {
            Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void History_DeleteNodeRestoresIncidentEdge()
    {
        var graph = ScriptableObject.CreateInstance<AnimTransitionAuthoringGraph>();
        var history = new AnimTransitionGraphHistory();
        try
        {
            history.Execute(graph, AnimTransitionCreateNodeCommand.Create<AnimGraphEntryNode>(Vector2.zero));
            history.Execute(graph, AnimTransitionCreateNodeCommand.Create<AnimGraphPredicateNode>(Vector2.one));
            var entry = graph.nodes[0];
            var predicate = graph.nodes[1];
            var edge = graph.Connect(predicate.GetPort("request", null), entry.GetPort("request", null), false);
            Assert.IsNotNull(edge);
            Assert.AreEqual(1, graph.edges.Count);
            var edgeGuid = edge.GUID;
            var predicateGuid = predicate.GUID;

            history.Execute(graph, AnimTransitionDeleteNodesCommand.Capture(graph, new[] { predicateGuid }));
            Assert.AreEqual(1, graph.nodes.Count);
            Assert.AreEqual(0, graph.edges.Count);

            history.Undo(graph);
            Assert.AreEqual(2, graph.nodes.Count);
            Assert.AreEqual(1, graph.edges.Count);
            Assert.AreEqual(edgeGuid, graph.edges[0].GUID);
        }
        finally
        {
            Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void History_MoveMultipleNodesIsOneCommand()
    {
        var graph = ScriptableObject.CreateInstance<AnimTransitionAuthoringGraph>();
        var history = new AnimTransitionGraphHistory();
        try
        {
            history.Execute(graph, AnimTransitionCreateNodeCommand.Create<AnimGraphEntryNode>(Vector2.zero));
            history.Execute(graph, AnimTransitionCreateNodeCommand.Create<AnimGraphOutputNode>(Vector2.one));
            var first = graph.nodes[0];
            var second = graph.nodes[1];
            var oldA = first.position;
            var oldB = second.position;
            first.position = new Rect(80f, 90f, oldA.width, oldA.height);
            second.position = new Rect(180f, 190f, oldB.width, oldB.height);
            var move = new AnimTransitionMoveNodesCommand(
                new[] { first.GUID, second.GUID },
                new[] { oldA, oldB },
                new[] { first.position, second.position });
            Assert.IsTrue(move.HasChange);
            history.Execute(graph, move);
            Assert.AreEqual(3, history.UndoCount);

            history.Undo(graph);
            Assert.AreEqual(oldA, graph.nodesPerGUID[first.GUID].position);
            Assert.AreEqual(oldB, graph.nodesPerGUID[second.GUID].position);
        }
        finally
        {
            Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void History_ReplaceIncomingEdgeIsAtomic()
    {
        var graph = ScriptableObject.CreateInstance<AnimTransitionAuthoringGraph>();
        var history = new AnimTransitionGraphHistory();
        try
        {
            history.Execute(graph, AnimTransitionCreateNodeCommand.Create<AnimGraphEntryNode>(Vector2.zero));
            history.Execute(graph, AnimTransitionCreateNodeCommand.Create<AnimGraphEntryNode>(Vector2.one));
            history.Execute(graph, AnimTransitionCreateNodeCommand.Create<AnimGraphPredicateNode>(new Vector2(2f, 2f)));
            var first = graph.nodes[0];
            var second = graph.nodes[1];
            var predicate = graph.nodes[2];
            var oldEdge = graph.Connect(predicate.GetPort("request", null), first.GetPort("request", null), false);
            var created = new AnimTransitionEdgeSnapshot(
                "edge-new",
                second.GUID,
                "request",
                string.Empty,
                predicate.GUID,
                "request",
                string.Empty);
            history.Execute(
                graph,
                new AnimTransitionReplaceIncomingEdgeCommand(AnimTransitionEdgeSnapshot.Capture(oldEdge), created));

            Assert.AreEqual(1, graph.edges.Count);
            Assert.AreEqual(second.GUID, graph.edges[0].outputNode.GUID);
            Assert.AreEqual("edge-new", graph.edges[0].GUID);

            history.Undo(graph);
            Assert.AreEqual(1, graph.edges.Count);
            Assert.AreEqual(first.GUID, graph.edges[0].outputNode.GUID);
            Assert.AreEqual(oldEdge.GUID, graph.edges[0].GUID);
        }
        finally
        {
            Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void History_NewCommandClearsRedoAndIgnoresForeignGraph()
    {
        var graph = ScriptableObject.CreateInstance<AnimTransitionAuthoringGraph>();
        var other = ScriptableObject.CreateInstance<AnimTransitionAuthoringGraph>();
        var history = new AnimTransitionGraphHistory();
        try
        {
            history.Execute(graph, AnimTransitionCreateNodeCommand.Create<AnimGraphEntryNode>(Vector2.zero));
            history.Undo(graph);
            Assert.AreEqual(1, history.RedoCount);
            history.Execute(graph, AnimTransitionCreateNodeCommand.Create<AnimGraphOutputNode>(Vector2.one));
            Assert.AreEqual(0, history.RedoCount);
            Assert.IsFalse(history.Undo(other));
            Assert.AreEqual(1, graph.nodes.Count);
        }
        finally
        {
            Object.DestroyImmediate(graph);
            Object.DestroyImmediate(other);
        }
    }

    [Test]
    public void History_TrimsOldestCommandsAtCapacity()
    {
        var graph = ScriptableObject.CreateInstance<AnimTransitionAuthoringGraph>();
        var history = new AnimTransitionGraphHistory(8);
        try
        {
            for (var i = 0; i < 10; i++)
            {
                history.Execute(graph, AnimTransitionCreateNodeCommand.Create<AnimGraphRerouteNode>(new Vector2(i, 0f)));
            }

            Assert.AreEqual(8, history.UndoCount);
            Assert.AreEqual(10, graph.nodes.Count);
        }
        finally
        {
            Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void CommandSnapshots_DoNotStoreEdgeConditionsOrPriority()
    {
        var scriptsRoot = Application.dataPath + "/GameMain/Scripts/Editor/Animation/TransitionGraph";
        var files = new[]
        {
            Path.Combine(scriptsRoot, "AnimTransitionGraphCommandContracts.cs"),
            Path.Combine(scriptsRoot, "AnimTransitionGraphCommands.cs"),
            Path.Combine(scriptsRoot, "AnimTransitionConnectionPolicy.cs"),
            Path.Combine(scriptsRoot, "AnimTransitionEdgeView.cs"),
            Path.Combine(scriptsRoot, "AnimTransitionEdgeConnectorListener.cs"),
        };
        var forbidden = new[] { "EdgeCondition", "EdgeConditionSet", "int Priority", "Conditions," };
        var violations = new List<string>();
        for (var i = 0; i < files.Length; i++)
        {
            var source = File.ReadAllText(files[i]);
            for (var j = 0; j < forbidden.Length; j++)
            {
                if (source.Contains(forbidden[j]))
                {
                    violations.Add(files[i] + " :: " + forbidden[j]);
                }
            }
        }

        Assert.That(violations, Is.Empty, string.Join("\n", violations));
    }

    [Test]
    public void ConnectionPolicy_RejectsTypeMismatchAndDoesNotWriteHistory()
    {
        var graph = ScriptableObject.CreateInstance<AnimTransitionAuthoringGraph>();
        var history = new AnimTransitionGraphHistory();
        try
        {
            history.Execute(graph, AnimTransitionCreateNodeCommand.Create<AnimGraphEntryNode>(Vector2.zero));
            history.Execute(graph, AnimTransitionCreateNodeCommand.Create<AnimGraphOutputNode>(Vector2.one));
            var entry = graph.nodes[0];
            var output = graph.nodes[1];
            var undoCount = history.UndoCount;
            var decision = AnimTransitionConnectionPolicy.Evaluate(
                graph, entry.GUID, "request", string.Empty, output.GUID, "input", string.Empty, string.Empty);
            Assert.AreEqual(AnimTransitionConnectionKind.Rejected, decision.Kind);
            Assert.That(decision.Reason, Does.Contain("Incompatible"));
            Assert.AreEqual(undoCount, history.UndoCount);
            Assert.AreEqual(0, graph.edges.Count);
        }
        finally
        {
            Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void ConnectionPolicy_ReplaceIncomingIsOneCommand()
    {
        var graph = ScriptableObject.CreateInstance<AnimTransitionAuthoringGraph>();
        var history = new AnimTransitionGraphHistory();
        try
        {
            history.Execute(graph, AnimTransitionCreateNodeCommand.Create<AnimGraphEntryNode>(Vector2.zero));
            history.Execute(graph, AnimTransitionCreateNodeCommand.Create<AnimGraphEntryNode>(Vector2.one));
            history.Execute(graph, AnimTransitionCreateNodeCommand.Create<AnimGraphPredicateNode>(new Vector2(2f, 2f)));
            var first = graph.nodes[0];
            var second = graph.nodes[1];
            var predicate = graph.nodes[2];
            var oldEdge = graph.Connect(predicate.GetPort("request", null), first.GetPort("request", null), false);
            Assert.IsNotNull(oldEdge);
            var decision = AnimTransitionConnectionPolicy.Evaluate(
                graph, second.GUID, "request", string.Empty, predicate.GUID, "request", string.Empty, string.Empty);
            Assert.AreEqual(AnimTransitionConnectionKind.Replace, decision.Kind);
            Assert.AreEqual(1, decision.Removed.Count);
            Assert.AreEqual(oldEdge.GUID, decision.Removed[0].Guid);
            Assert.IsTrue(history.Execute(graph, AnimTransitionConnectionPolicy.ToCommand(decision)));
            Assert.AreEqual(1, graph.edges.Count);
            Assert.AreEqual(second.GUID, graph.edges[0].outputNode.GUID);
            history.Undo(graph);
            Assert.AreEqual(1, graph.edges.Count);
            Assert.AreEqual(first.GUID, graph.edges[0].outputNode.GUID);
        }
        finally
        {
            Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void InsertReroute_OnPlanEdgeRestoresOriginalOnUndo()
    {
        var graph = ScriptableObject.CreateInstance<AnimTransitionAuthoringGraph>();
        var history = new AnimTransitionGraphHistory();
        try
        {
            history.Execute(graph, AnimTransitionCreateNodeCommand.Create<AnimGraphVariantNode>(Vector2.zero));
            history.Execute(graph, AnimTransitionCreateNodeCommand.Create<AnimGraphTransitionPolicyNode>(Vector2.one));
            var variant = graph.nodes[0];
            var policy = graph.nodes[1];
            var edge = graph.Connect(policy.GetPort("input", null), variant.GetPort("plan", null), false);
            Assert.IsNotNull(edge);
            var edgeGuid = edge.GUID;
            Assert.IsTrue(AnimTransitionInsertRerouteCommand.TryCreate(
                graph, edge, new Vector2(40f, 40f), out var command, out var reason), reason);
            Assert.IsTrue(history.Execute(graph, command));
            Assert.AreEqual(3, graph.nodes.Count);
            Assert.AreEqual(2, graph.edges.Count);
            history.Undo(graph);
            Assert.AreEqual(2, graph.nodes.Count);
            Assert.AreEqual(1, graph.edges.Count);
            Assert.AreEqual(edgeGuid, graph.edges[0].GUID);
        }
        finally
        {
            Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void InsertReroute_RejectsRequestPortsWithoutHistory()
    {
        var graph = ScriptableObject.CreateInstance<AnimTransitionAuthoringGraph>();
        var history = new AnimTransitionGraphHistory();
        try
        {
            history.Execute(graph, AnimTransitionCreateNodeCommand.Create<AnimGraphEntryNode>(Vector2.zero));
            history.Execute(graph, AnimTransitionCreateNodeCommand.Create<AnimGraphPredicateNode>(Vector2.one));
            var edge = graph.Connect(graph.nodes[1].GetPort("request", null), graph.nodes[0].GetPort("request", null), false);
            var undoCount = history.UndoCount;
            Assert.IsFalse(AnimTransitionInsertRerouteCommand.TryCreate(graph, edge, Vector2.zero, out _, out var reason));
            Assert.That(reason, Does.Contain("Plan"));
            Assert.AreEqual(undoCount, history.UndoCount);
        }
        finally
        {
            Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void QuickAdd_UndoRemovesCreatedNodeAndEdge()
    {
        var graph = ScriptableObject.CreateInstance<AnimTransitionAuthoringGraph>();
        var history = new AnimTransitionGraphHistory();
        try
        {
            history.Execute(graph, AnimTransitionCreateNodeCommand.Create<AnimGraphEntryNode>(Vector2.zero));
            var entry = graph.nodes[0];
            var predicate = BaseNode.CreateFromType<AnimGraphPredicateNode>(new Vector2(80f, 0f));
            var command = new AnimTransitionQuickAddCommand(
                AnimTransitionNodeSnapshot.Capture(predicate),
                new AnimTransitionEdgeSnapshot(
                    "quick-edge",
                    entry.GUID,
                    "request",
                    string.Empty,
                    predicate.GUID,
                    "request",
                    string.Empty));
            Assert.IsTrue(history.Execute(graph, command));
            Assert.AreEqual(2, graph.nodes.Count);
            Assert.AreEqual(1, graph.edges.Count);
            history.Undo(graph);
            Assert.AreEqual(1, graph.nodes.Count);
            Assert.AreEqual(0, graph.edges.Count);
        }
        finally
        {
            Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void SetNodeProperty_UndoRestoresPredicateConfiguration()
    {
        var graph = ScriptableObject.CreateInstance<AnimTransitionAuthoringGraph>();
        var history = new AnimTransitionGraphHistory();
        try
        {
            history.Execute(graph, AnimTransitionCreateNodeCommand.Create<AnimGraphPredicateNode>(Vector2.zero));
            var node = (AnimGraphPredicateNode)graph.nodes[0];
            var before = AnimTransitionNodeSnapshot.Capture(node);
            node.EditorSetPredicate(AnimGraphPredicateKind.ActionLease, "lease-a");
            var after = AnimTransitionNodeSnapshot.Capture(node);
            Assert.IsTrue(history.Execute(graph, new AnimTransitionSetNodePropertyCommand(before, after)));
            Assert.AreEqual("lease-a", ((AnimGraphPredicateNode)graph.nodesPerGUID[node.GUID]).Operand);
            history.Undo(graph);
            Assert.AreEqual(string.Empty, ((AnimGraphPredicateNode)graph.nodesPerGUID[node.GUID]).Operand);
        }
        finally
        {
            Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void Catalog_FindsPredicateInputForRequestPort()
    {
        Assert.IsTrue(AnimTransitionNodeCatalog.FindCompatibleField(
            typeof(AnimGraphPredicateNode), typeof(AnimGraphRequestPort), true, out var field));
        Assert.AreEqual("request", field);
    }

    [Test]
    public void Layout_EstimateCrossingsDetectsIndependentX()
    {
        var graph = ScriptableObject.CreateInstance<AnimTransitionAuthoringGraph>();
        try
        {
            var leftTop = new AnimGraphVariantNode { GUID = "lt" };
            var rightBottom = new AnimGraphTransitionPolicyNode { GUID = "rb" };
            var leftBottom = new AnimGraphVariantNode { GUID = "lb" };
            var rightTop = new AnimGraphTransitionPolicyNode { GUID = "rt" };
            leftTop.position = new Rect(0f, 0f, 100f, 40f);
            rightBottom.position = new Rect(200f, 200f, 100f, 40f);
            leftBottom.position = new Rect(0f, 200f, 100f, 40f);
            rightTop.position = new Rect(200f, 0f, 100f, 40f);
            graph.AddNode(leftTop);
            graph.AddNode(rightBottom);
            graph.AddNode(leftBottom);
            graph.AddNode(rightTop);
            graph.Connect(rightBottom.GetPort("input", null), leftTop.GetPort("plan", null), false);
            graph.Connect(rightTop.GetPort("input", null), leftBottom.GetPort("plan", null), false);
            Assert.GreaterOrEqual(AnimTransitionLayoutService.EstimateCrossings(graph), 1);
        }
        finally
        {
            Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void EditorSources_DoNotScanWholeProjectOrFindObjectOfType()
    {
        var scriptsRoot = Application.dataPath + "/GameMain/Scripts/Editor/Animation/TransitionGraph";
        var files = Directory.GetFiles(scriptsRoot, "*.cs");
        var forbidden = new[] { "FindObjectOfType", "FindObjectsOfType", "AssetDatabase.FindAssets" };
        var violations = new List<string>();
        for (var i = 0; i < files.Length; i++)
        {
            var source = File.ReadAllText(files[i]);
            for (var j = 0; j < forbidden.Length; j++)
            {
                if (source.Contains(forbidden[j]))
                {
                    violations.Add(Path.GetFileName(files[i]) + " :: " + forbidden[j]);
                }
            }
        }

        Assert.That(violations, Is.Empty, string.Join("\n", violations));
    }
}

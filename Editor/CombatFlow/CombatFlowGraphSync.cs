#if UNITY_EDITOR
using System.Collections.Generic;
using GraphProcessor;
using UnityEditor;
using UnityEngine;

/// <summary>CombatFlowProcessorGraph ↔ CombatGraphAsset 双向同步。</summary>
public static class CombatFlowGraphSync
{
    public static void PushToAuthoring(CombatGraphAsset asset, CombatFlowProcessorGraph view)
    {
        if (asset == null || view == null)
        {
            return;
        }

        view.PruneEdgeMeta();

        var nodeList = new List<CombatFlowNodeAuthoring>();
        for (var i = 0; i < view.nodes.Count; i++)
        {
            if (TryMapNode(view.nodes[i], out var authoring))
            {
                authoring.GraphPosition = view.nodes[i].position.position;
                nodeList.Add(authoring);
            }
        }

        var edgeList = new List<CombatFlowEdgeAuthoring>();
        for (var i = 0; i < view.edges.Count; i++)
        {
            var se = view.edges[i];
            if (se.outputNode == null || se.inputNode == null)
            {
                continue;
            }

            if (!TryResolveNodeId(se.outputNode, out var fromId)
                || !TryResolveNodeId(se.inputNode, out var toId))
            {
                continue;
            }

            var meta = view.GetOrCreateEdgeMeta(se.GUID);
            var authoring = meta.Authoring;
            authoring.FromNodeId = fromId;
            authoring.ToNodeId = toId;
            if (CombatFlowInputConditionSync.TryGetPrimaryInputCondition(
                    authoring.EdgeConditions,
                    out var inputCond))
            {
                CombatFlowInputConditionSync.SyncEdgeFromInputCondition(ref authoring, inputCond);
            }

            meta.Authoring = authoring;
            edgeList.Add(authoring);
        }

        var so = new SerializedObject(asset);
        WriteNodes(so.FindProperty("nodes"), nodeList);
        WriteFlowEdges(so.FindProperty("flowEdges"), edgeList);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }

    public static void PullFromAuthoring(CombatGraphAsset asset, CombatFlowProcessorGraph view)
    {
        if (asset == null || view == null)
        {
            return;
        }

        view.SetOwner(asset);
        while (view.nodes.Count > 0)
        {
            view.RemoveNode(view.nodes[0]);
        }

        view.ClearEdgeMeta();

        var idToNode = new Dictionary<string, BaseNode>();
        var srcNodes = asset.Nodes;
        if (srcNodes != null)
        {
            for (var i = 0; i < srcNodes.Length; i++)
            {
                var bn = CreateProcessorNode(srcNodes[i]);
                if (bn == null)
                {
                    continue;
                }

                view.AddNode(bn);
                idToNode[srcNodes[i].NodeId] = bn;
            }
        }

        var srcEdges = asset.FlowEdges;
        if (srcEdges != null)
        {
            for (var i = 0; i < srcEdges.Length; i++)
            {
                var e = srcEdges[i];
                if (!idToNode.TryGetValue(e.FromNodeId, out var from)
                    || !idToNode.TryGetValue(e.ToNodeId, out var to))
                {
                    continue;
                }

                if (from.outputPorts.Count == 0 || to.inputPorts.Count == 0)
                {
                    continue;
                }

                var se = view.Connect(to.inputPorts[0], from.outputPorts[0]);
                var meta = view.GetOrCreateEdgeMeta(se.GUID);
                meta.Authoring = CloneEdge(e);
            }
        }

        EditorUtility.SetDirty(view);
    }

    public static bool TryEnsureProcessorView(CombatGraphAsset asset, out string error)
    {
        error = null;
        if (asset == null)
        {
            error = "asset=null";
            return false;
        }

        if (asset.ProcessorView != null)
        {
            asset.ProcessorView.SetOwner(asset);
            return true;
        }

        if (!EditorUtility.IsPersistent(asset))
        {
            error = "CombatGraphAsset 尚未保存到磁盘。请先 Ctrl+S 保存 .asset，再创建 Graph 视图子资产。";
            return false;
        }

        var view = ScriptableObject.CreateInstance<CombatFlowProcessorGraph>();
        view.name = asset.name + "_View";
        view.SetOwner(asset);
        AssetDatabase.AddObjectToAsset(view, asset);

        var so = new SerializedObject(asset);
        so.FindProperty("processorView").objectReferenceValue = view;
        so.ApplyModifiedPropertiesWithoutUndo();

        PullFromAuthoring(asset, view);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return true;
    }

    [System.Obsolete("Use TryEnsureProcessorView")]
    public static void EnsureProcessorView(CombatGraphAsset asset)
    {
        TryEnsureProcessorView(asset, out _);
    }

    static bool TryMapNode(BaseNode node, out CombatFlowNodeAuthoring authoring)
    {
        authoring = default;
        switch (node)
        {
            case CombatFlowStartNode start:
                authoring = new CombatFlowNodeAuthoring
                {
                    NodeId = string.IsNullOrEmpty(start.nodeId) ? "Start" : start.nodeId,
                    Kind = CombatFlowNodeKind.Start,
                };
                return true;
            case CombatFlowActionNode action:
                authoring = new CombatFlowNodeAuthoring
                {
                    NodeId = string.IsNullOrEmpty(action.nodeId) ? action.name : action.nodeId,
                    Kind = CombatFlowNodeKind.FlowAction,
                    Action = action.action,
                    TerminalOnComplete = action.terminalOnComplete,
                    TerminalPolicy = action.terminalPolicy,
                };
                return true;
            case CombatFlowRouteSwitchNode route:
                authoring = new CombatFlowNodeAuthoring
                {
                    NodeId = string.IsNullOrEmpty(route.nodeId) ? route.name : route.nodeId,
                    Kind = CombatFlowNodeKind.RouteSwitch,
                    Route = route.route,
                };
                return true;
            case CombatFlowEndNode end:
                authoring = new CombatFlowNodeAuthoring
                {
                    NodeId = string.IsNullOrEmpty(end.nodeId) ? "Idle" : end.nodeId,
                    Kind = CombatFlowNodeKind.End,
                };
                return true;
            default:
                return false;
        }
    }

    static bool TryResolveNodeId(BaseNode node, out string nodeId)
    {
        nodeId = null;
        switch (node)
        {
            case CombatFlowStartNode start:
                nodeId = string.IsNullOrEmpty(start.nodeId) ? "Start" : start.nodeId;
                return true;
            case CombatFlowActionNode action:
                nodeId = string.IsNullOrEmpty(action.nodeId) ? "Action" : action.nodeId;
                return true;
            case CombatFlowRouteSwitchNode route:
                nodeId = string.IsNullOrEmpty(route.nodeId) ? "Route" : route.nodeId;
                return true;
            case CombatFlowEndNode end:
                nodeId = string.IsNullOrEmpty(end.nodeId) ? "Idle" : end.nodeId;
                return true;
            default:
                return false;
        }
    }

    static BaseNode CreateProcessorNode(in CombatFlowNodeAuthoring src)
    {
        BaseNode node;
        switch (src.Kind)
        {
            case CombatFlowNodeKind.Start:
                node = new CombatFlowStartNode { nodeId = src.NodeId };
                break;
            case CombatFlowNodeKind.FlowAction:
                node = new CombatFlowActionNode
                {
                    nodeId = src.NodeId,
                    action = src.Action,
                    terminalOnComplete = src.TerminalOnComplete,
                    terminalPolicy = src.TerminalPolicy,
                };
                break;
            case CombatFlowNodeKind.RouteSwitch:
                node = new CombatFlowRouteSwitchNode { nodeId = src.NodeId, route = src.Route };
                break;
            case CombatFlowNodeKind.End:
                node = new CombatFlowEndNode { nodeId = src.NodeId };
                break;
            default:
                return null;
        }

        node.position.position = src.GraphPosition;
        return node;
    }

    static CombatFlowEdgeAuthoring CloneEdge(in CombatFlowEdgeAuthoring src)
    {
        return new CombatFlowEdgeAuthoring
        {
            FromNodeId = src.FromNodeId,
            ToNodeId = src.ToNodeId,
            EdgeKind = src.EdgeKind,
            Transition = src.Transition,
            Label = src.Label,
            InputSlot = src.InputSlot,
            InputSemantic = src.InputSemantic,
            InputModifier = src.InputModifier,
            Conditions = src.Conditions,
            ConditionRefs = src.ConditionRefs,
            EdgeConditions = src.EdgeConditions,
            Priority = src.Priority,
            TargetRoute = src.TargetRoute,
            LateWindowSeconds = src.LateWindowSeconds,
        };
    }

    static void WriteNodes(SerializedProperty prop, List<CombatFlowNodeAuthoring> nodes)
    {
        prop.arraySize = nodes.Count;
        for (var i = 0; i < nodes.Count; i++)
        {
            var el = prop.GetArrayElementAtIndex(i);
            var n = nodes[i];
            el.FindPropertyRelative(nameof(CombatFlowNodeAuthoring.NodeId)).stringValue = n.NodeId;
            el.FindPropertyRelative(nameof(CombatFlowNodeAuthoring.Kind)).enumValueIndex = (int)n.Kind;
            el.FindPropertyRelative(nameof(CombatFlowNodeAuthoring.Action)).objectReferenceValue = n.Action;
            el.FindPropertyRelative(nameof(CombatFlowNodeAuthoring.Route)).objectReferenceValue = n.Route;
            el.FindPropertyRelative(nameof(CombatFlowNodeAuthoring.GraphPosition)).vector2Value = n.GraphPosition;
        }
    }

    static void WriteFlowEdges(SerializedProperty prop, List<CombatFlowEdgeAuthoring> edges)
    {
        prop.arraySize = edges.Count;
        for (var i = 0; i < edges.Count; i++)
        {
            var el = prop.GetArrayElementAtIndex(i);
            var e = edges[i];
            el.FindPropertyRelative(nameof(CombatFlowEdgeAuthoring.FromNodeId)).stringValue = e.FromNodeId;
            el.FindPropertyRelative(nameof(CombatFlowEdgeAuthoring.ToNodeId)).stringValue = e.ToNodeId;
            el.FindPropertyRelative(nameof(CombatFlowEdgeAuthoring.EdgeKind)).enumValueIndex = (int)e.EdgeKind;
            el.FindPropertyRelative(nameof(CombatFlowEdgeAuthoring.Transition)).enumValueIndex = (int)e.Transition;
            el.FindPropertyRelative(nameof(CombatFlowEdgeAuthoring.Label)).stringValue = e.Label;
            el.FindPropertyRelative(nameof(CombatFlowEdgeAuthoring.InputSlot)).enumValueIndex = (int)e.InputSlot;
            el.FindPropertyRelative(nameof(CombatFlowEdgeAuthoring.InputSemantic)).enumValueIndex = (int)e.InputSemantic;
            el.FindPropertyRelative(nameof(CombatFlowEdgeAuthoring.InputModifier)).enumValueIndex = (int)e.InputModifier;
            el.FindPropertyRelative(nameof(CombatFlowEdgeAuthoring.Priority)).intValue = e.Priority;
            el.FindPropertyRelative(nameof(CombatFlowEdgeAuthoring.TargetRoute)).objectReferenceValue = e.TargetRoute;
            el.FindPropertyRelative(nameof(CombatFlowEdgeAuthoring.LateWindowSeconds)).floatValue = e.LateWindowSeconds;
            WriteConditions(el.FindPropertyRelative(nameof(CombatFlowEdgeAuthoring.Conditions)), e.Conditions);
            WriteConditionRefs(el.FindPropertyRelative(nameof(CombatFlowEdgeAuthoring.ConditionRefs)), e.ConditionRefs);
            WriteEdgeConditions(el.FindPropertyRelative(nameof(CombatFlowEdgeAuthoring.EdgeConditions)), e.EdgeConditions);
        }
    }

    static void WriteEdgeConditions(SerializedProperty arr, EdgeConditionSO[] refs)
    {
        var len = refs != null ? refs.Length : 0;
        arr.arraySize = len;
        for (var i = 0; i < len; i++)
        {
            arr.GetArrayElementAtIndex(i).objectReferenceValue = refs[i];
        }
    }

    static void WriteConditionRefs(SerializedProperty arr, CombatFlowConditionDefinition[] refs)
    {
        var len = refs != null ? refs.Length : 0;
        arr.arraySize = len;
        for (var i = 0; i < len; i++)
        {
            arr.GetArrayElementAtIndex(i).objectReferenceValue = refs[i];
        }
    }

    static void WriteConditions(SerializedProperty arr, SkillTransitionCondition[] conds)
    {
        if (conds == null || conds.Length == 0)
        {
            arr.arraySize = 0;
            return;
        }

        arr.arraySize = conds.Length;
        for (var i = 0; i < conds.Length; i++)
        {
            var c = conds[i];
            var el = arr.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("kind").enumValueIndex = (int)c.Kind;
            el.FindPropertyRelative("requiredMoveDirection").enumValueIndex = (int)c.RequiredMoveDirection;
        }
    }
}
#endif

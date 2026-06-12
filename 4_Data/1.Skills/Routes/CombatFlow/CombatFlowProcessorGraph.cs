using System;
using System.Collections.Generic;
using GraphProcessor;
using UnityEngine;

/// <summary>147.1 GraphProcessor 视图 — 与 <see cref="CombatGraphAsset"/> 双向同步；运行时不用。</summary>
public sealed class CombatFlowProcessorGraph : BaseGraph
{
    [SerializeField] CombatGraphAsset ownerAsset;
    [SerializeField] List<CombatFlowProcessorEdgeMeta> edgeMeta = new();

    public CombatGraphAsset OwnerAsset => ownerAsset;
    public IReadOnlyList<CombatFlowProcessorEdgeMeta> EdgeMeta => edgeMeta;

    public void SetOwner(CombatGraphAsset asset) => ownerAsset = asset;

    public void ClearEdgeMeta() => edgeMeta.Clear();

    public CombatFlowProcessorEdgeMeta GetOrCreateEdgeMeta(string edgeGuid)
    {
        for (var i = 0; i < edgeMeta.Count; i++)
        {
            if (edgeMeta[i].EdgeGuid == edgeGuid)
            {
                return edgeMeta[i];
            }
        }

        var meta = new CombatFlowProcessorEdgeMeta { EdgeGuid = edgeGuid };
        edgeMeta.Add(meta);
        return meta;
    }

    public void RemoveEdgeMeta(string edgeGuid)
    {
        for (var i = edgeMeta.Count - 1; i >= 0; i--)
        {
            if (edgeMeta[i].EdgeGuid == edgeGuid)
            {
                edgeMeta.RemoveAt(i);
            }
        }
    }

    public void PruneEdgeMeta()
    {
        if (edges == null || edges.Count == 0)
        {
            edgeMeta.Clear();
            return;
        }

        var live = new HashSet<string>();
        for (var i = 0; i < edges.Count; i++)
        {
            if (!string.IsNullOrEmpty(edges[i].GUID))
            {
                live.Add(edges[i].GUID);
            }
        }

        for (var i = edgeMeta.Count - 1; i >= 0; i--)
        {
            if (!live.Contains(edgeMeta[i].EdgeGuid))
            {
                edgeMeta.RemoveAt(i);
            }
        }
    }
}

[Serializable]
public sealed class CombatFlowProcessorEdgeMeta
{
    public string EdgeGuid;
    public CombatFlowEdgeAuthoring Authoring = new()
    {
        EdgeKind = CombatFlowEdgeKind.Flow,
        Transition = CombatFlowTransitionMode.OnInput,
        InputSlot = SkillEntrySlot.Any,
    };

    public string EdgeGuidProperty => EdgeGuid;
}

/// <summary>Flow 边端口标记类型（GraphProcessor 需具体类型）。</summary>
public struct CombatFlowPort
{
}

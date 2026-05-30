using System;
using UnityEngine;

/// <summary>
/// 战斗行为图资产 — 注册 Route 池 + 从 Idle 出发的显式边（124.1 Combat-Flow MVP）。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/SkillRoute/Combat Graph", fileName = "Combat_Graph_")]
public sealed class CombatGraphAsset : ScriptableObject
{
    [SerializeField, Tooltip("空闲节点 ID；Route 自然结束后回到此节点。")]
    private string idleNodeId = "Idle";

    [SerializeField, Tooltip("本图允许解析的 Route 池（Resolver 校验 targetRoute 须在此列表内）。")]
    private SkillRouteDefinition[] registeredRoutes;

    [SerializeField, Tooltip("从 fromNode 出发的边；同 from 按 priority 升序求值，首条满足者中标。")]
    private CombatGraphEdge[] edges;

    public string IdleNodeId => string.IsNullOrEmpty(idleNodeId) ? "Idle" : idleNodeId;
    public SkillRouteDefinition[] RegisteredRoutes => registeredRoutes;
    public CombatGraphEdge[] Edges => edges;

    public bool ContainsRoute(SkillRouteDefinition route)
    {
        if (route == null || registeredRoutes == null)
        {
            return false;
        }

        for (var i = 0; i < registeredRoutes.Length; i++)
        {
            if (registeredRoutes[i] == route)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>图边：触发语义 + 条件 + 目标 Route。</summary>
[Serializable]
public struct CombatGraphEdge
{
    [SerializeField] private string fromNodeId;
    [SerializeField] private string toNodeId;
    [SerializeField] private SkillRouteDefinition targetRoute;
    [SerializeField] private SkillEntrySlot triggerSlot;
    [SerializeField] private InputSemanticType triggerSemantic;
    [SerializeField] private SkillTransitionCondition[] conditions;
    [SerializeField] private int priority;

    public string FromNodeId => fromNodeId;
    public string ToNodeId => toNodeId;
    public SkillRouteDefinition TargetRoute => targetRoute;
    public SkillEntrySlot TriggerSlot => triggerSlot;
    public InputSemanticType TriggerSemantic => triggerSemantic;
    public SkillTransitionCondition[] Conditions => conditions;
    public int Priority => priority;
}

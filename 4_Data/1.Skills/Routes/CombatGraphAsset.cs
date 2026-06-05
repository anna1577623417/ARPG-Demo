using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 147.1 Combat Flow Authoring 资产 — 资源池 + 流转图；运行时读 <see cref="CompiledData"/>。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/SkillRoute/Combat Flow Graph", fileName = "CombatFlow_")]
public sealed class CombatGraphAsset : ScriptableObject
{
    [Header("Resource Pools")]
    [SerializeField, Tooltip("Action 资源池；FlowAction 节点只引用池内 Action。")]
    ActionDataSO[] actionPool;

    [FormerlySerializedAs("registeredRoutes")]
    [SerializeField, Tooltip("Route 资源池；Runner 校验 TargetRoute / RouteSwitch 须在此列表内。")]
    SkillRouteDefinition[] routePool;

    [SerializeField, Tooltip("Condition 资产池；flowEdges.ConditionRefs 须引用此列表内资产。")]
    CombatFlowConditionDefinition[] conditionPool;

    [Header("Flow Graph (Authoring)")]
    [SerializeField] CombatFlowNodeAuthoring[] nodes;
    [SerializeField] CombatFlowEdgeAuthoring[] flowEdges;

    [Header("Compiled Runtime")]
    [SerializeField] CombatFlowData compiledData = new();
    [SerializeField] bool compileValid;
    [SerializeField, TextArea(2, 6)] string lastCompileReport;

    [SerializeField, HideInInspector]
    string idleNodeId = "Idle";

    [SerializeField, HideInInspector]
    CombatFlowProcessorGraph processorView;

    public ActionDataSO[] ActionPool => actionPool;
    public SkillRouteDefinition[] RoutePool => routePool;
    public CombatFlowConditionDefinition[] ConditionPool => conditionPool;
    public SkillRouteDefinition[] RegisteredRoutes => routePool;
    public CombatFlowNodeAuthoring[] Nodes => nodes;
    public CombatFlowEdgeAuthoring[] FlowEdges => flowEdges;
    public CombatFlowData CompiledData => compiledData;
    public bool CompileValid => compileValid;
    public string LastCompileReport => lastCompileReport;
    public CombatFlowProcessorGraph ProcessorView => processorView;

    public string IdleNodeId
    {
        get
        {
            if (compiledData != null && !string.IsNullOrEmpty(compiledData.IdleNodeId))
            {
                return compiledData.IdleNodeId;
            }

            return string.IsNullOrEmpty(idleNodeId) ? "Idle" : idleNodeId;
        }
    }

    public bool HasValidCompile => compileValid && compiledData != null && !compiledData.IsEmpty;

    public bool ContainsRoute(SkillRouteDefinition route)
    {
        if (route == null || routePool == null)
        {
            return false;
        }

        for (var i = 0; i < routePool.Length; i++)
        {
            if (routePool[i] == route)
            {
                return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    public void EditorSetCompileResult(CombatFlowData data, bool valid, string report)
    {
        compiledData = data ?? new CombatFlowData();
        compileValid = valid;
        lastCompileReport = report ?? string.Empty;
    }

    public void EditorSetProcessorView(CombatFlowProcessorGraph view)
    {
        processorView = view;
    }
#endif
}

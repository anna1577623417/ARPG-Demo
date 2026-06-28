using System;
using GraphProcessor;
using UnityEngine;

[Serializable, NodeMenuItem("Combat Flow/Start")]
public sealed class CombatFlowStartNode : BaseNode
{
    public string nodeId = "Start";

    [Output(name = "Out", allowMultiple = true)]
    public CombatFlowPort output;

    public override string name => string.IsNullOrEmpty(nodeId) ? "Start" : nodeId;
    public override Color color => new(0.35f, 0.75f, 0.45f);
}

[Serializable, NodeMenuItem("Combat Flow/Flow Action")]
public sealed class CombatFlowActionNode : BaseNode
{
    public string nodeId = "Action";
    public ActionDataSO action;

    /// <summary>186.1 — 段结束后视为图终结（无需连 End 节点）。</summary>
    public bool terminalOnComplete;

    /// <summary>186.1 — 终结归位策略；仅 terminalOnComplete=true 时生效。</summary>
    public CombatFlowTerminalPolicy terminalPolicy;

    [Input(name = "In", allowMultiple = true)]
    public CombatFlowPort input;

    [Output(name = "Out", allowMultiple = true)]
    public CombatFlowPort output;

    public override string name
    {
        get
        {
            var baseName = string.IsNullOrEmpty(nodeId) ? "FlowAction" : nodeId;
            // 186.1 — 终结节点角标：⊥（FallbackToEntry / GoIdle / KeepCurrent 共用同一标记）
            return terminalOnComplete ? baseName + " ⊥" : baseName;
        }
    }

    public override Color color
    {
        get
        {
            if (action == null)
            {
                return new Color(0.35f, 0.55f, 0.95f);
            }

            return ActionIntentRouting.ResolveGraphParticipation(action) switch
            {
                GraphParticipation.SourceOnly => new Color(0.42f, 0.82f, 0.62f),
                GraphParticipation.None => new Color(0.55f, 0.55f, 0.55f),
                _ => new Color(0.35f, 0.55f, 0.95f),
            };
        }
    }
}

[Serializable, NodeMenuItem("Combat Flow/Route Switch")]
public sealed class CombatFlowRouteSwitchNode : BaseNode
{
    public string nodeId = "Route";
    public SkillRouteDefinition route;

    [Input(name = "In", allowMultiple = true)]
    public CombatFlowPort input;

    [Output(name = "Out", allowMultiple = true)]
    public CombatFlowPort output;

    public override string name => string.IsNullOrEmpty(nodeId) ? "RouteSwitch" : nodeId;
    public override Color color => new(0.85f, 0.65f, 0.25f);
}

[Serializable, NodeMenuItem("Combat Flow/End")]
public sealed class CombatFlowEndNode : BaseNode
{
    public string nodeId = "End";

    [Input(name = "In", allowMultiple = true)]
    public CombatFlowPort input;

    public override string name => string.IsNullOrEmpty(nodeId) ? "End" : nodeId;
    public override Color color => new(0.55f, 0.55f, 0.55f);
}

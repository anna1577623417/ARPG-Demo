using System;
using UnityEngine;

/// <summary>147.1 Combat Flow 图内节点种类（非输入 Entry；输入仍由 SkillEntry+Route 负责）。</summary>
public enum CombatFlowNodeKind : byte
{
    Start = 0,
    FlowAction = 1,
    RouteSwitch = 2,
    End = 3,
}

/// <summary>147.1 流转边种类。</summary>
public enum CombatFlowEdgeKind : byte
{
    Flow = 0,
    Interrupt = 1,
}

/// <summary>边触发模式：段结束 / 输入意图 / 立即（编译期校验）。</summary>
public enum CombatFlowTransitionMode : byte
{
    OnSegmentComplete = 0,
    OnInput = 1,
    Immediate = 2,
}

/// <summary>149.3 — Graph 已启用且当前上下文未命中边时的策略。</summary>
public enum CombatFlowGraphMissPolicy : byte
{
    /// <summary>回落 Default Entry 解析。</summary>
    FallbackToEntry = 0,

    /// <summary>动作进行中且无匹配边：丢弃意图（不回 Entry）。Idle/Start 未命中仍回落 Entry。</summary>
    Block = 1,
}

/// <summary>Combat Graph 内节点（只引用资源 GUID，不嵌入 Action 内容）。</summary>
[Serializable]
public struct CombatFlowNodeAuthoring
{
    [Tooltip("图内唯一 ID，如 Start / AttackA / End。")]
    public string NodeId;

    public CombatFlowNodeKind Kind;

    [Tooltip("FlowAction：引用 Action 池内动作。")]
    public ActionDataSO Action;

    [Tooltip("RouteSwitch：切换到另一条 Route 入口。")]
    public SkillRouteDefinition Route;

    [Tooltip("可视化编辑器预留位置。")]
    public Vector2 GraphPosition;
}

/// <summary>Combat Graph 内边：描述动作/Route 之间何时切换。</summary>
[Serializable]
public struct CombatFlowEdgeAuthoring
{
    public string FromNodeId;
    public string ToNodeId;

    public CombatFlowEdgeKind EdgeKind;
    public CombatFlowTransitionMode Transition;

    [Tooltip("Inspector / 调试标签，如 Combo / Dodge。")]
    public string Label;

    [Tooltip("OnInput 时可选：过滤物理槽位（非 SkillEntry 入口解析）。")]
    public SkillEntrySlot InputSlot;

    [Tooltip("OnInput 时可选：过滤语义。")]
    public InputSemanticType InputSemantic;

    [Tooltip("内联条件叶子；与 ConditionRefs 在编译时 AND 合并。")]
    public SkillTransitionCondition[] Conditions;

    [Tooltip("Condition 资产池引用；编译时展开为 Conditions 的一部分。")]
    public CombatFlowConditionDefinition[] ConditionRefs;

    [Tooltip("同 from 节点出边升序求值。")]
    public int Priority;

    [Tooltip("走此边时施放的 Route；空则沿用当前 Route / Combo 链。")]
    public SkillRouteDefinition TargetRoute;

    [Tooltip("149.3 — 动作自然结束后容错秒数（绝对时间）；0=无 Late Window。仅 OnInput 边常用。")]
    public float LateWindowSeconds;
}

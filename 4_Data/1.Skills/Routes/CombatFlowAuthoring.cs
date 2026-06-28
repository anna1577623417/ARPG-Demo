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

/// <summary>
/// OnInput 边修饰键过滤（Any=0 为默认值，兼容未配置修饰键的旧边）。
/// </summary>
public enum CombatFlowInputModifier : byte
{
    Any = 0,
    /// <summary>要求 intent 无修饰键（纯 InputSlot 短按）。</summary>
    None = 1,
    Shift = 2,
    Space = 3,
}

/// <summary>149.3 — Graph 已启用且当前上下文未命中边时的策略。</summary>
public enum CombatFlowGraphMissPolicy : byte
{
    /// <summary>回落 Default Entry 解析。</summary>
    FallbackToEntry = 0,

    /// <summary>动作进行中且无匹配边：丢弃意图（不回 Entry）。Idle/Start 未命中仍回落 Entry。</summary>
    Block = 1,
}

/// <summary>
/// 186.1 — 终结节点策略：取代必须画线连出 End 节点的硬约束。
/// </summary>
public enum CombatFlowTerminalPolicy : byte
{
    /// <summary>默认：段结束后回落 Default Entry 解析（等价于既有 End→Entry 路径）。</summary>
    FallbackToEntry = 0,

    /// <summary>显式归 Idle（不解析 Entry）。</summary>
    GoIdle = 1,

    /// <summary>当前 Action 自然结束，由 Runner 默认行为接管（不主动归位）。</summary>
    KeepCurrent = 2,
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

    [Tooltip("186.1 — 此节点的 Action / Route 段结束后是否视为图终结（无需画线连 End）；OnSegmentComplete 触发。")]
    public bool TerminalOnComplete;

    [Tooltip("186.1 — 终结归位策略；仅 TerminalOnComplete=true 时生效。")]
    public CombatFlowTerminalPolicy TerminalPolicy;
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

    [Tooltip("OnInput 时可选：组合键修饰槽位（Any=不检查；None=要求无修饰键；Shift=Shift+InputSlot）。")]
    public CombatFlowInputModifier InputModifier;

    [Tooltip("内联条件叶子；与 ConditionRefs 在编译时 AND 合并。")]
    public SkillTransitionCondition[] Conditions;

    [Tooltip("Condition 资产池引用；编译时展开为 Conditions 的一部分。")]
    public CombatFlowConditionDefinition[] ConditionRefs;

    [Tooltip("185.2 语义条件（Input / Phase / EventWindow / CombatContext）；AND 组合。")]
    public EdgeConditionSO[] EdgeConditions;

    [Tooltip("同 from 节点出边升序求值。")]
    public int Priority;

    [Tooltip("走此边时施放的 Route；空则沿用当前 Route / Combo 链。")]
    public SkillRouteDefinition TargetRoute;

    [Tooltip("149.3 — 动作自然结束后容错秒数（绝对时间）；0=无 Late Window。仅 OnInput 边常用。")]
    public float LateWindowSeconds;
}

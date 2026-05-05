/// <summary>
/// 战斗标签语义根节点（固定六类，勿随意增删重命名）。
/// <para>Event 为一次性信号，不进入 <see cref="GameplayTagContainer"/> 持久轨。</para>
/// </summary>
public enum TagCategory
{
    /// <summary>行为状态：正在做什么（与 FSM / Action / Motion 强相关）。位域见 <see cref="StateTag"/>。</summary>
    State = 0,

    /// <summary>持续影响：Buff / Debuff / CC / 元素附着等。位域见 <see cref="StatusTag"/>。</summary>
    Status = 1,

    /// <summary>实体能力闸：当前是否「有资格」执行某类行为（多来源聚合结果）。位域见 <see cref="EntityCapabilityTag"/>。</summary>
    Ability = 2,

    /// <summary>规则层：免疫、弱点、甲类型等低频突变属性。位域见 <see cref="MechanicTag"/>。</summary>
    Mechanic = 3,

    /// <summary>阵营：伤害过滤、AI 目标等。位域见 <see cref="FactionTag"/>。</summary>
    Faction = 4,

    /// <summary>离散事件标签（不入容器）；见 <see cref="CombatEventTag"/>。</summary>
    Event = 5,
}

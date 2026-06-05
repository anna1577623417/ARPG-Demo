using UnityEngine;

/// <summary>
/// 147.1 可复用 Flow 条件资产 — Graph 边引用；运行时读编译后的 <see cref="SkillTransitionCondition"/> 快照。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/SkillRoute/Combat Flow Condition", fileName = "FlowCondition_")]
public sealed class CombatFlowConditionDefinition : ScriptableObject
{
    [SerializeField, Tooltip("编辑器显示名；空则用 asset 名。")]
    string displayName;

    [SerializeField, Tooltip("条件叶子；多条引用在边上 AND 组合。")]
    SkillTransitionCondition condition;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public SkillTransitionCondition Condition => condition;
}

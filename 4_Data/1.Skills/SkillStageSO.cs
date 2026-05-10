using UnityEngine;

/// <summary>
/// 技能单个阶段：Action + Task + 可选命中形状与目标过滤。
/// </summary>
[CreateAssetMenu(menuName = "Skill/Skill Stage", fileName = "SkillStage")]
public class SkillStageSO : ScriptableObject
{
    [Header("Presentation")]
    public ActionDataSO action;

    [Header("Tasks")]
    public AbilityTaskSO[] tasks;

    [Header("Transitions")]
    public StageTransitionType transitionType = StageTransitionType.Auto;
    public float transitionWindow;
    public bool requireConfirmInput;

    [Header("Detection")]
    public HitShapeSO hitShape;
    public TargetFilterSO targetFilter;
}

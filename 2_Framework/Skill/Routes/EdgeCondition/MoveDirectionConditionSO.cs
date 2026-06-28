using UnityEngine;

/// <summary>
/// 186.1 — 移动方向条件（吸收 SkillContextGroupDefinition.RequiredMoveDirection 维度）。
/// <para>None=不检查；其余值要求 ctx.MoveDirection 完全一致。</para>
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Skill/EdgeCondition/MoveDirection", fileName = "EdgeCondition_MoveDir_")]
public sealed class MoveDirectionConditionSO : EdgeConditionSO
{
    [Tooltip("None=不检查移动方向；其余值要求当前移动方向严格一致。")]
    public MoveDirection8 RequiredDirection = MoveDirection8.None;

    [Tooltip("true=反向语义：当前方向必须不是 RequiredDirection（None 时永真）。")]
    public bool Negate;

    public override bool Evaluate(in EdgeContext ctx)
    {
        if (RequiredDirection == MoveDirection8.None)
        {
            return true;
        }

        var match = ctx.MoveDirection == RequiredDirection;
        return Negate ? !match : match;
    }
}

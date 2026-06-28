using UnityEngine;

[CreateAssetMenu(menuName = "GameMain/Skill/EdgeCondition/Phase", fileName = "EdgeCondition_Phase_")]
public sealed class PhaseConditionSO : EdgeConditionSO
{
    [Tooltip("勾选的相位中任一成立即通过（OR）；多条 SO 串联实现 AND。")]
    public PhaseMask AllowedPhases;

    [Tooltip("勾选的相位中任一成立即失败；用于禁止在某相位。")]
    public PhaseMask BlockedPhases;

    public override bool Evaluate(in EdgeContext ctx)
    {
        var p = ctx.Phase;
        var hits = PhaseMask.None;
        if (p.IsGrounded)
        {
            hits |= PhaseMask.Grounded;
        }

        if (p.IsAirborne)
        {
            hits |= PhaseMask.Airborne;
        }

        if (p.IsAscending)
        {
            hits |= PhaseMask.Ascending;
        }

        if (p.IsApex)
        {
            hits |= PhaseMask.Apex;
        }

        if (p.IsDescending)
        {
            hits |= PhaseMask.Descending;
        }

        if ((BlockedPhases & hits) != 0)
        {
            return false;
        }

        if (AllowedPhases == PhaseMask.None)
        {
            return true;
        }

        return (AllowedPhases & hits) != 0;
    }
}

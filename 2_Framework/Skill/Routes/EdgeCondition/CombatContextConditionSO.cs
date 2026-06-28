using UnityEngine;

[CreateAssetMenu(menuName = "GameMain/Skill/EdgeCondition/CombatContext", fileName = "EdgeCondition_CombatContext_")]
public sealed class CombatContextConditionSO : EdgeConditionSO
{
    public EdgeCombatContextFlag RequireAll;
    public EdgeCombatContextFlag RequireNone;

    public override bool Evaluate(in EdgeContext ctx)
    {
        if (RequireAll == EdgeCombatContextFlag.None && RequireNone == EdgeCombatContextFlag.None)
        {
            return true;
        }

        var c = ctx.Combat;
        if (RequireNone != EdgeCombatContextFlag.None)
        {
            for (var bit = 0; bit < 16; bit++)
            {
                var flag = (EdgeCombatContextFlag)(1 << bit);
                if (flag == EdgeCombatContextFlag.None)
                {
                    continue;
                }

                if ((RequireNone & flag) != 0 && c.HasFlag(flag))
                {
                    return false;
                }
            }
        }

        if (RequireAll == EdgeCombatContextFlag.None)
        {
            return true;
        }

        for (var bit = 0; bit < 16; bit++)
        {
            var flag = (EdgeCombatContextFlag)(1 << bit);
            if (flag == EdgeCombatContextFlag.None)
            {
                continue;
            }

            if ((RequireAll & flag) != 0 && !c.HasFlag(flag))
            {
                return false;
            }
        }

        return true;
    }
}

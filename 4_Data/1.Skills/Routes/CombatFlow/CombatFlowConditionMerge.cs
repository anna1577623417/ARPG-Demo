using System.Collections.Generic;

/// <summary>147.1 内联条件 + Condition 资产 → 编译用扁平 AND 数组。</summary>
public static class CombatFlowConditionMerge
{
    public static SkillTransitionCondition[] Merge(
        SkillTransitionCondition[] inline,
        CombatFlowConditionDefinition[] conditionRefs)
    {
        var count = inline != null ? inline.Length : 0;
        var refCount = conditionRefs != null ? conditionRefs.Length : 0;
        if (count == 0 && refCount == 0)
        {
            return System.Array.Empty<SkillTransitionCondition>();
        }

        var list = new List<SkillTransitionCondition>(count + refCount);
        if (inline != null)
        {
            for (var i = 0; i < inline.Length; i++)
            {
                list.Add(inline[i]);
            }
        }

        if (conditionRefs != null)
        {
            for (var i = 0; i < conditionRefs.Length; i++)
            {
                var def = conditionRefs[i];
                if (def == null)
                {
                    continue;
                }

                list.Add(def.Condition);
            }
        }

        return list.Count == 0 ? System.Array.Empty<SkillTransitionCondition>() : list.ToArray();
    }
}

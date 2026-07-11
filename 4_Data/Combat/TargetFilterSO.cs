using UnityEngine;

/// <summary>目标过滤（SkillStage 可选；CombatObject 已改用 <see cref="TargetFilterParams"/>）。</summary>
public abstract class TargetFilterSO : ScriptableObject
{
    public abstract bool Passes(Entity caster, Entity target);
}

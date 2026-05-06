using UnityEngine;

/// <summary>目标过滤（SkillStage 可选）。</summary>
public abstract class TargetFilterSO : ScriptableObject
{
    public abstract bool Passes(Entity caster, Entity target);
}

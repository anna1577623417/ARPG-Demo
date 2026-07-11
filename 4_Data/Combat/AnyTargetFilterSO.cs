using System;
using UnityEngine;

/// <summary>SkillStage 遗留过滤器；CombatObject 请用 <see cref="TargetFilterParams"/>。</summary>
[Obsolete("CombatObject 使用 TargetFilterParams；仅 SkillStage 遗留引用保留。")]
[CreateAssetMenu(menuName = "Combat/Target Filter/Any")]
public sealed class AnyTargetFilterSO : TargetFilterSO
{
    public override bool Passes(Entity caster, Entity target) => target != null && target != caster;
}

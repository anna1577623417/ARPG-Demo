using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Target Filter/Any")]
public sealed class AnyTargetFilterSO : TargetFilterSO
{
    public override bool Passes(Entity caster, Entity target) => target != null && target != caster;
}

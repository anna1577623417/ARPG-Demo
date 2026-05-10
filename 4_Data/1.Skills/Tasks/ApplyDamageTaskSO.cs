using UnityEngine;

[CreateAssetMenu(menuName = "Skill/Task/Apply Damage")]
public sealed class ApplyDamageTaskSO : AbilityTaskSO
{
    [Min(0f)] public float damageMultiplier = 1f;
    [Min(0.01f)] public float radius = 1.5f;
    public LayerMask hitMask = ~0;

    public override AbilityTask CreateInstance() => new ApplyDamageTask(this);
}

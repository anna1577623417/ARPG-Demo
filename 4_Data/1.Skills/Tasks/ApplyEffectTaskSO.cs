using UnityEngine;

[CreateAssetMenu(menuName = "Skill/Task/Apply Effect")]
public sealed class ApplyEffectTaskSO : AbilityTaskSO
{
    public BuffDefinitionSO buff;
    public TargetType applyTo;

    public override AbilityTask CreateInstance() => new ApplyEffectTask(this);
}

using UnityEngine;

[CreateAssetMenu(menuName = "Skill/Task/Modify Cooldown")]
public sealed class ModifyCooldownTaskSO : AbilityTaskSO
{
    public SkillSlotType targetSlot = SkillSlotType.Skill_Primary_01;
    public CooldownOp op = CooldownOp.ReduceFlat;
    public float value = 2f;

    public override AbilityTask CreateInstance() => new ModifyCooldownTask(this);
}

using UnityEngine;

[CreateAssetMenu(menuName = "GameMain/Skill/EdgeCondition/Input", fileName = "EdgeCondition_Input_")]
public sealed class InputConditionSO : EdgeConditionSO
{
    [Tooltip("匹配的意图类型（None=任意 Skill Entry）。")]
    public GameplayIntentKind IntentKind = GameplayIntentKind.None;

    [Tooltip("匹配的槽位（Any=不检查）。")]
    public SkillEntrySlot Slot = SkillEntrySlot.Any;

    [Tooltip("语义过滤（None=不检查）。")]
    public InputSemanticType RequireSemantic = InputSemanticType.None;

    [Tooltip("组合键修饰槽位（Any=不检查；Shift 等）。")]
    public SkillEntrySlot RequiredModifierSlot = SkillEntrySlot.Any;

    public override bool Evaluate(in EdgeContext ctx)
    {
        var i = ctx.Intent;
        if (IntentKind != GameplayIntentKind.None && i.Kind != IntentKind)
        {
            return false;
        }

        if (Slot != SkillEntrySlot.Any && i.EntrySlot != Slot)
        {
            return false;
        }

        if (RequireSemantic != InputSemanticType.None && i.Semantic != RequireSemantic)
        {
            return false;
        }

        if (RequiredModifierSlot != SkillEntrySlot.Any && i.ModifierSlot != RequiredModifierSlot)
        {
            return false;
        }

        return true;
    }
}

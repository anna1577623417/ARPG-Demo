#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// EdgeConditions 内 InputConditionSO 与边 Context Input 字段的单向同步（SO → 边编译字段）。
/// </summary>
public static class CombatFlowInputConditionSync
{
    public static bool TryGetPrimaryInputCondition(EdgeConditionSO[] conditions, out InputConditionSO input)
    {
        input = null;
        if (conditions == null)
        {
            return false;
        }

        for (var i = 0; i < conditions.Length; i++)
        {
            if (conditions[i] is InputConditionSO candidate)
            {
                input = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>将 InputConditionSO 映射到边的 OnInput 过滤字段（编译产物与 Runner 一致）。</summary>
    public static void SyncEdgeFromInputCondition(ref CombatFlowEdgeAuthoring edge, InputConditionSO input)
    {
        if (input == null)
        {
            return;
        }

        var slot = ResolveSlot(input);
        if (slot != SkillEntrySlot.Any)
        {
            edge.InputSlot = slot;
        }

        if (input.RequireSemantic != InputSemanticType.None)
        {
            edge.InputSemantic = input.RequireSemantic;
        }

        edge.InputModifier = MapModifier(input.RequiredModifierSlot);
    }

    public static SkillEntrySlot ResolveSlot(InputConditionSO input)
    {
        if (input.Slot != SkillEntrySlot.Any)
        {
            return input.Slot;
        }

        if (GameplayIntent.TryIntentKindToSlot(input.IntentKind, out var fromKind))
        {
            return fromKind;
        }

        return SkillEntrySlot.Any;
    }

    public static CombatFlowInputModifier MapModifier(SkillEntrySlot modifierSlot)
    {
        switch (modifierSlot)
        {
            case SkillEntrySlot.Shift:
                return CombatFlowInputModifier.Shift;
            case SkillEntrySlot.Space:
                return CombatFlowInputModifier.Space;
            default:
                return CombatFlowInputModifier.Any;
        }
    }
}
#endif

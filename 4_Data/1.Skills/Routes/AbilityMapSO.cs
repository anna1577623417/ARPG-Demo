using UnityEngine;

/// <summary>
/// Loadout 级 CombatFlow 流转闸门：物理槽位 + 能力语义 → 准入规则（138/137 终态）。
/// 不用于 Action 打断窗；起手见 SkillRouteDefinition.abilityGateRules。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/SkillRoute/Ability/Ability Map (Combat Flow)", fileName = "AbilityMap_")]
public sealed class AbilityMapSO : ScriptableObject
{
    [System.Serializable]
    public struct Binding
    {
        public SkillEntrySlot slot;
        public AbilitySemantic ability;
        public AbilityGateRuleSO rule;
        public bool enabled;
    }

    [SerializeField] Binding[] bindings;

    public Binding[] Bindings => bindings;

    public bool TryGetRule(SkillEntrySlot slot, AbilitySemantic ability, out AbilityGateRuleSO rule)
    {
        rule = null;
        if (bindings == null)
        {
            return false;
        }

        for (var i = 0; i < bindings.Length; i++)
        {
            var b = bindings[i];
            if (!b.enabled || b.slot != slot || b.ability != ability)
            {
                continue;
            }

            rule = b.rule;
            return rule != null;
        }

        return false;
    }
}

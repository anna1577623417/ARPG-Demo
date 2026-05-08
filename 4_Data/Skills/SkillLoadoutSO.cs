using System;
using UnityEngine;

/// <summary>角色技能装配（槽位绑定）。槽位：<see cref="SkillSlotType"/>（Primary / Secondary / Ability1 / Ability2 / Dodge / Ultimate / Jump）。</summary>
[CreateAssetMenu(menuName = "Skill/Skill Loadout", fileName = "SkillLoadout")]
public class SkillLoadoutSO : ScriptableObject
{
    [Tooltip("逐项绑定；未绑定的槽 SkillSystem 可走 Moveset（若 Player 勾选允许回退）。")]
    [Serializable]
    public struct SlotBinding
    {
        public SkillSlotType slot;
        public SkillDataSO skill;

        [Tooltip("HUD 按键提示（纯展示）。应与 Input Actions / Rebind 一致，例如 LMB、RMB、Shift。")]
        public string hudKeyLabel;
    }

    public SlotBinding[] bindings;

    public SkillDataSO Resolve(SkillSlotType slot)
    {
        if (bindings == null)
        {
            return null;
        }

        for (var i = 0; i < bindings.Length; i++)
        {
            if (bindings[i].slot == slot)
            {
                return bindings[i].skill;
            }
        }

        return null;
    }
}

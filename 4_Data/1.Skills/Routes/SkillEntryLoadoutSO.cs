using System;
using UnityEngine;

/// <summary>
/// 角色技能装配（Loadout） — 槽位 → SkillEntryDefinition 的总线。
///
/// ═══ 与 SkillLoadoutSO（旧）的区别 ═══
///   · 旧 Loadout：Slot → SkillData，一槽一技能。
///   · 新 Loadout：Slot → SkillEntry → N 个 Route，一槽多招（蓄力/连段/方向/派生 同槽共存）。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/SkillRoute/Entry/Skill Entry Loadout", fileName = "Loadout_")]
public class SkillEntryLoadoutSO : ScriptableObject
{
    [Serializable]
    public struct EntryBinding
    {
        [SerializeField, Tooltip("槽位（与 SkillEntryDefinition.Slot 必须一致；不一致以 Loadout 优先）。")]
        private SkillEntrySlot slot;

        [SerializeField, Tooltip("入口资产。")]
        private SkillEntryDefinition entry;

        [SerializeField, Tooltip("HUD 按键提示（纯显示，与 .inputactions 绑定一致）。")]
        private string hudKeyLabel;

        public SkillEntrySlot Slot => slot;
        public SkillEntryDefinition Entry => entry;
        public string HudKeyLabel => hudKeyLabel;
    }

    [SerializeField, Tooltip("逐项绑定；同一槽位多次出现以第一项为准。")]
    private EntryBinding[] bindings;

    [Header("Combat Flow & Context (136.1)")]
    [SerializeField, Tooltip("本 Loadout 的战斗流转图；不再挂 Player 上。")]
    CombatGraphAsset combatFlow;

    [SerializeField, Tooltip("上下文语义组（Directional/地面/滞空等 → Group）。")]
    SkillContextGroupDefinition[] contextGroups;

    [Header("Combat Flow Gate (138)")]
    [SerializeField, Tooltip(
        "CombatGraph 边流转准入：TriggerSlot + Ability 语义 → AbilityGateRule。\n" +
        "技能起手见各 Route.abilityGateRules；Action 打断见 ActionWindow.InterruptibleByCategories。")]
    AbilityMapSO abilityMap;

    public EntryBinding[] Bindings => bindings;
    public CombatGraphAsset CombatFlow => combatFlow;
    public SkillContextGroupDefinition[] ContextGroups => contextGroups;
    public AbilityMapSO AbilityMap => abilityMap;

    /// <summary>查询 — 0-GC 路径，运行时安全。未找到返回 null。</summary>
    public SkillEntryDefinition Resolve(SkillEntrySlot slot)
    {
        if (bindings == null)
        {
            return null;
        }

        for (var i = 0; i < bindings.Length; i++)
        {
            if (bindings[i].Slot == slot)
            {
                return bindings[i].Entry;
            }
        }

        return null;
    }

    /// <summary>HUD 用：按 slot 取键位标签。</summary>
    public string ResolveKeyLabel(SkillEntrySlot slot)
    {
        if (bindings == null)
        {
            return string.Empty;
        }

        for (var i = 0; i < bindings.Length; i++)
        {
            if (bindings[i].Slot == slot)
            {
                return bindings[i].HudKeyLabel ?? string.Empty;
            }
        }

        return string.Empty;
    }
}

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
    [SerializeField, Tooltip("启用后运行时装配 CombatGraph 并参与 Contextual 解析；关闭则退化为 Entry+Interrupt 单轨。")]
    bool combatFlowEnabled = true;

    [SerializeField, Tooltip("147.1 战斗流转图模板；运行时读 CompiledData，非 SkillEntry 输入入口。")]
    CombatGraphAsset combatFlow;

    [SerializeField, Tooltip("上下文语义组（Directional/地面/滞空等 → Group）。")]
    SkillContextGroupDefinition[] contextGroups;

    [Header("Locomotion Graph Context (157.2/157.3)")]
    [SerializeField, Tooltip("JumpStart/JumpLoop/JumpLand 与 Graph SourceOnly 节点绑定；未配则 Graph 仍用 Start/Idle。")]
    LocomotionGraphContextBinding locomotionGraphContext;

    [Header("Combat Flow Gate (138)")]
    [SerializeField, Tooltip(
        "147.1 Flow 边 OnInput 流转时的 Ability 准入（Slot→Ability→Rule）。\n" +
        "技能起手见 Route.abilityGateRules；Action 打断见 ActionWindow。")]
    AbilityMapSO abilityMap;

    [Header("Airborne Interrupt Policy (168.3 — 角色画像)")]
    [SerializeField, Tooltip(
        "声明本 Loadout 对应的角色在空中三阶段（Ascending/ApexHold/Descending）允许被哪些 ActionCategory 中断。\n" +
        "仅决定『空中状态能否被打断』；具体技能能否释放仍由 AbilityGateService 白名单判定。\n" +
        "未配置（全 None）→ 运行时 fallback 到 Universal 预设，保持现行手感。")]
    AirInterruptPolicy airInterruptPolicy = new()
    {
        AscendingAllowed  = ActionCategoryPresets.AllPillar,
        ApexAllowed       = ActionCategoryPresets.AllPillar,
        DescendingAllowed = ActionCategoryPresets.AllPillar,
        ApexVyThreshold   = 1.5f,
        GroundCoyoteSeconds = 0.08f,
    };

    public AirInterruptPolicy AirInterruptPolicy => airInterruptPolicy;

    public EntryBinding[] Bindings => bindings;
    public bool CombatFlowEnabled => combatFlowEnabled;
    public CombatGraphAsset CombatFlow => combatFlow;
    public SkillContextGroupDefinition[] ContextGroups => contextGroups;
    public LocomotionGraphContextBinding LocomotionGraphContext => locomotionGraphContext;
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

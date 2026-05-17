using System;
using UnityEngine;

/// <summary>
/// 玩家级输入语义配置（Ver4.3.7+ Phase F）。
///
/// ═══ 设计契约（《112.1 重构》Charge 分层条款）═══
///   · TapThreshold / ComboWindow / EnableDirectional 属于"玩家手感"，**不是**技能属性 —
///     从 ChargeRoute / ComboRoute 资产中迁出，集中在这里。
///   · Player 在 Init / Loadout 切换时把本 SO 的每槽位配置注入 InputSemanticResolver。
///   · 缺省项（未在本 SO 配置的槽位）回落到 ChargeRoute.TapThreshold / ComboRoute.ComboResetTime
///     作为兼容入口，使现有资产无需立刻迁移。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/SkillRoute/Input/Semantic Config", fileName = "SemanticConfig_")]
public sealed class SemanticConfigSO : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        [SerializeField] SkillEntrySlot slot;

        [SerializeField, Min(0f),
         Tooltip("HoldThreshold：按住越过此秒数派发 Charge intent（≤0 = 该槽位无 Charge 分流）。")]
        float tapThreshold;

        [SerializeField, Min(0f),
         Tooltip("ComboWindow：上一连段自然结束后，允许多久内按下衔接下一段（≤0 = 无 Combo）。")]
        float comboWindow;

        [SerializeField, Tooltip("启用方向 Modifier：Space/翻滚 等需要 WASD 缓冲共同决策的槽位勾上。")]
        bool enableDirectional;

        public SkillEntrySlot Slot => slot;
        public float TapThreshold => tapThreshold;
        public float ComboWindow => comboWindow;
        public bool EnableDirectional => enableDirectional;
    }

    [Header("Per-Slot")]
    [SerializeField] Entry[] entries;

    public Entry[] Entries => entries;

    /// <summary>查询某槽位是否在本 SO 中显式配置；返回 true 时 cfg 被填，否则 cfg 保留 default。</summary>
    public bool TryResolve(SkillEntrySlot slot, out InputSemanticResolver.PerSlotConfig cfg)
    {
        cfg = default;
        if (entries == null) return false;

        for (var i = 0; i < entries.Length; i++)
        {
            if (entries[i].Slot != slot) continue;
            cfg = new InputSemanticResolver.PerSlotConfig
            {
                TapThreshold = entries[i].TapThreshold,
                ComboWindow = entries[i].ComboWindow,
                EnableDirectional = entries[i].EnableDirectional,
            };
            return true;
        }

        return false;
    }
}

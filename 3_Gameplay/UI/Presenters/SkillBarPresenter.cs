using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能栏 Presenter：
/// · Inspector 手动摆槽：列表绑定场景中已有 SkillSlotView；
/// · 动态模式：根据 Player.SkillLoadout 实例化槽位预制体（数量随装配变化），并写入 hudKeyLabel。
/// </summary>
[AddComponentMenu("GameMain/UI/Skill Bar Presenter")]
public sealed class SkillBarPresenter : MonoBehaviour
{
    /// <summary>Inspector 静态列表 / 运行时按 Loadout 生成。</summary>
    public enum LayoutMode
    {
        InspectorSlots = 0,
        InstantiateFromPlayerLoadout = 1,
    }

    [System.Serializable]
    public struct SlotBinding
    {
        [Tooltip("Player 上的技能槽位类型。")]
        public SkillSlotType Slot;

        [Tooltip("场景中对应槽位的 SkillSlotView 组件。")]
        public SkillSlotView View;
    }

    [Header("Layout")]
    [SerializeField] LayoutMode layoutMode = LayoutMode.InspectorSlots;

    [Tooltip("动态模式：槽位实例化的父节点（一般为 SkillSlotGroup）。")]
    [SerializeField] Transform slotsRoot;

    [Tooltip("动态模式：技能槽预制体根节点须挂 SkillSlotView（建议同物体挂 SkillCooldownTicker）。")]
    [SerializeField] SkillSlotView slotPrefab;

    [Header("Bindings (InspectorSlots only)")]
    [SerializeField] List<SlotBinding> slotBindings = new List<SlotBinding>();

    Player m_player;
    bool m_bound;

    readonly List<SlotBinding> _activeBindings = new List<SlotBinding>(8);
    readonly List<GameObject> _spawnedInstances = new List<GameObject>(8);

    public void Bind(Player player)
    {
        if (player == null)
        {
            return;
        }

        if (m_bound)
        {
            Unbind();
        }

        m_player = player;
        m_bound = true;

        BuildActiveBindings(player);

        for (var i = 0; i < _activeBindings.Count; i++)
        {
            var b = _activeBindings[i];
            ApplyFixedDataToSlot(b);

            if (b.View == null)
            {
                continue;
            }

            var ticker = b.View.GetComponent<SkillCooldownTicker>();
            if (ticker == null)
            {
                ticker = b.View.gameObject.AddComponent<SkillCooldownTicker>();
            }

            ticker.Configure(player, b.Slot, b.View);
        }
    }

    public void Unbind()
    {
        for (var i = 0; i < _activeBindings.Count; i++)
        {
            var b = _activeBindings[i];
            if (b.View == null)
            {
                continue;
            }

            var ticker = b.View.GetComponent<SkillCooldownTicker>();
            ticker?.Configure(null, b.Slot, b.View);
        }

        ClearSpawnedPrefabs();
        _activeBindings.Clear();

        m_player = null;
        m_bound = false;
    }

    void OnDisable() => Unbind();
    void OnDestroy() => Unbind();

    void BuildActiveBindings(Player player)
    {
        _activeBindings.Clear();

        if (layoutMode == LayoutMode.InstantiateFromPlayerLoadout)
        {
            BuildFromLoadout(player);
            return;
        }

        for (var i = 0; i < slotBindings.Count; i++)
        {
            _activeBindings.Add(slotBindings[i]);
        }
    }

    void BuildFromLoadout(Player player)
    {
        ClearSpawnedPrefabs();

        var loadout = player.SkillLoadout;
        if (loadout == null || loadout.bindings == null || slotsRoot == null || slotPrefab == null)
        {
            return;
        }

        for (var i = 0; i < loadout.bindings.Length; i++)
        {
            var row = loadout.bindings[i];
            if (row.skill == null)
            {
                continue;
            }

            var instance = Instantiate(slotPrefab, slotsRoot);
            _spawnedInstances.Add(instance.gameObject);

            var view = instance.GetComponent<SkillSlotView>();
            if (view == null)
            {
                continue;
            }

            _activeBindings.Add(new SlotBinding { Slot = row.slot, View = view });

            if (!string.IsNullOrEmpty(row.hudKeyLabel))
            {
                view.SetKeyHint(row.hudKeyLabel);
            }
        }
    }

    void ClearSpawnedPrefabs()
    {
        for (var i = 0; i < _spawnedInstances.Count; i++)
        {
            var go = _spawnedInstances[i];
            if (go != null)
            {
                Destroy(go);
            }
        }

        _spawnedInstances.Clear();
    }

    void ApplyFixedDataToSlot(SlotBinding binding)
    {
        if (binding.View == null || m_player == null)
        {
            return;
        }

        if (!m_player.TryGetSkillRuntime(binding.Slot, out var runtime) || runtime == null)
        {
            binding.View.SetIcon(null);
            binding.View.SetCooldownProgress(1f);
            binding.View.SetCooldownRemaining(0f);
            binding.View.SetLevel(0);
            binding.View.SetHighlight(0f);
            binding.View.SetAvailable(false);
            return;
        }

        if (runtime.Data != null)
        {
            binding.View.SetIcon(runtime.Data.icon);
        }

        binding.View.SetLevel(runtime.Level);
    }
}

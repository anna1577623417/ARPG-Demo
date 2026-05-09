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

    [Header("Input Key Hint")]
    [Tooltip("可选覆盖：不填则优先使用 Player 上的 InputReader。")]
    [SerializeField] InputReader inputReaderOverride;

    [Tooltip("可选覆盖：不填时运行期自动查找场景中的 RebindManager。")]
    [SerializeField] RebindManager rebindManager;

    [Tooltip("当未找到输入映射时，KeyHint 的最终兜底文案。")]
    [SerializeField] string emptyKeyHintFallback = "";

    Player m_player;
    InputReader m_inputReader;
    RebindManager m_rebindManager;
    bool m_bound;

    readonly List<SlotBinding> _activeBindings = new List<SlotBinding>(8);
    readonly List<GameObject> _spawnedInstances = new List<GameObject>(8);
    readonly Dictionary<SkillSlotType, string> _slotFallbackHints = new Dictionary<SkillSlotType, string>(8);

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
        m_inputReader = inputReaderOverride != null ? inputReaderOverride : player.InputReader;
        m_bound = true;

        BuildActiveBindings(player);
        BindRebindEvents();

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
        _slotFallbackHints.Clear();
        UnbindRebindEvents();

        m_player = null;
        m_inputReader = null;
        m_bound = false;
    }

    void OnDisable() => Unbind();
    void OnDestroy() => Unbind();

    void BuildActiveBindings(Player player)
    {
        _activeBindings.Clear();
        _slotFallbackHints.Clear();

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
                _slotFallbackHints[row.slot] = row.hudKeyLabel;
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

        RefreshKeyHint(binding);

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

    void RefreshKeyHint(SlotBinding binding)
    {
        if (binding.View == null)
        {
            return;
        }

        var hint = string.Empty;
        if (m_inputReader != null)
        {
            hint = m_inputReader.GetSkillSlotBindingDisplayString(binding.Slot);
        }

        if (string.IsNullOrEmpty(hint) && _slotFallbackHints.TryGetValue(binding.Slot, out var fallbackHint))
        {
            hint = fallbackHint;
        }

        if (string.IsNullOrEmpty(hint))
        {
            hint = emptyKeyHintFallback;
        }

        binding.View.SetKeyHint(hint);
    }

    void RefreshAllKeyHints()
    {
        for (var i = 0; i < _activeBindings.Count; i++)
        {
            RefreshKeyHint(_activeBindings[i]);
        }
    }

    void BindRebindEvents()
    {
        m_rebindManager = rebindManager != null ? rebindManager : FindFirstObjectByType<RebindManager>();
        if (m_rebindManager == null)
        {
            return;
        }

        m_rebindManager.OnBindingsChanged += HandleBindingsChanged;
    }

    void UnbindRebindEvents()
    {
        if (m_rebindManager == null)
        {
            return;
        }

        m_rebindManager.OnBindingsChanged -= HandleBindingsChanged;
        m_rebindManager = null;
    }

    void HandleBindingsChanged()
    {
        if (!m_bound)
        {
            return;
        }

        RefreshAllKeyHints();
    }
}

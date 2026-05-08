using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家资源 HUD Presenter — 把 Player.Resources / Player.Stats 的事件流翻译成 ResourceBarView 的 SetNormalized / SetText。
///
/// ═══ 职责（v4.3 三层架构的中间桥梁）═══
///
/// · 持 Player 引用 + ResourceBarView 引用
/// · 订阅 IResourcePool.OnCurrentChanged 与 IStatSet.OnFinalValueChanged
/// · OnDestroy 解订阅（铁规：禁止内存泄漏）
/// · View 不知道 Player 存在，Runtime 不知道 View 存在——本类是唯一桥梁
///
/// ═══ 数据频率分层处理 ═══
///
/// · CurrentHP 变化（中频）   → IResourcePool.OnCurrentChanged → 重算归一化 → SetNormalized + SetText
/// · MaxHealth 变化（低频）   → IStatSet.OnFinalValueChanged   → 重算归一化（current 不变但 max 变了）→ SetNormalized + SetText
/// · Buffer 缓动（60 fps）   → ResourceBarView 内部 Update 自驱动，本 Presenter 完全不参与
/// </summary>
[AddComponentMenu("GameMain/UI/Player HUD Presenter")]
public sealed class PlayerHUDPresenter : MonoBehaviour
{
    [Serializable]
    public struct ResourceBinding
    {
        [Tooltip("绑定的资源类型（HP / Stamina / MP / Energy 等）。")]
        public ResourceType Type;

        [Tooltip("对应这条资源的 View（场景中的资源条 GameObject 上的组件）。")]
        public ResourceBarView View;

        [Tooltip("当 Max 来自 StatSet（例：MaxHealth / MaxStamina）时，订阅哪个 StatType。设为 None 表示 Max 不通过 Stat 计算。")]
        public StatType MaxStatLink;

        [Tooltip("文本格式化模板。占位符：{cur}=取整当前值，{max}=取整最大值。例：'{cur} / {max}' 或 '{cur}'。")]
        public string TextFormat;
    }

    [Header("Bindings")]
    [SerializeField] List<ResourceBinding> resourceBindings = new List<ResourceBinding>();

    Player m_player;
    IResourcePool m_resources;
    IStatSet m_stats;
    bool m_bound;

    // ─── 生命周期 ─────────────────────────────────────────────────────────────

    /// <summary>由父级 HUD（PlayerStatusHud）在 OnPropsSet 时调用一次。</summary>
    public void Bind(Player player)
    {
        if (player == null)
        {
            Debug.LogWarning("[PlayerHUDPresenter] Bind 收到 null player，HUD 将保持空白。", this);
            return;
        }

        // 防御：重复 Bind 时先清理上一次订阅
        if (m_bound)
        {
            Unbind();
        }

        m_player = player;
        m_resources = player.Resources;
        m_stats = player.Stats;

        if (m_resources != null)
        {
            m_resources.OnCurrentChanged += OnResourceCurrentChanged;
        }

        if (m_stats != null)
        {
            m_stats.OnFinalValueChanged += OnStatFinalChanged;
        }

        m_bound = true;

        // 首帧推送：避免 HUD 启动时一片空白
        for (var i = 0; i < resourceBindings.Count; i++)
        {
            PushBindingToView(resourceBindings[i], forceSync: true);
        }
    }

    public void Unbind()
    {
        if (!m_bound) return;

        if (m_resources != null) m_resources.OnCurrentChanged -= OnResourceCurrentChanged;
        if (m_stats != null) m_stats.OnFinalValueChanged -= OnStatFinalChanged;

        m_resources = null;
        m_stats = null;
        m_player = null;
        m_bound = false;
    }

    void OnDestroy() => Unbind();
    void OnDisable() => Unbind();

    // ─── 事件处理 ─────────────────────────────────────────────────────────────

    void OnResourceCurrentChanged(ResourceType type, float oldValue, float newValue)
    {
        for (var i = 0; i < resourceBindings.Count; i++)
        {
            if (resourceBindings[i].Type == type)
            {
                PushBindingToView(resourceBindings[i], forceSync: false);
            }
        }
    }

    void OnStatFinalChanged(StatType type, float value)
    {
        // Stat 变化只影响那些通过 MaxStatLink 关联的资源（如 MaxHealth → HP 条）
        for (var i = 0; i < resourceBindings.Count; i++)
        {
            if (resourceBindings[i].MaxStatLink == type)
            {
                PushBindingToView(resourceBindings[i], forceSync: false);
            }
        }
    }

    // ─── 推送到 View ──────────────────────────────────────────────────────────

    void PushBindingToView(ResourceBinding binding, bool forceSync)
    {
        if (binding.View == null || m_resources == null) return;

        var current = m_resources.GetCurrent(binding.Type);
        var max = m_resources.GetMax(binding.Type);
        var norm = max > 0.0001f ? Mathf.Clamp01(current / max) : 0f;

        if (forceSync)
        {
            binding.View.ForceSync(norm);
        }
        else
        {
            binding.View.SetNormalized(norm);
        }

        if (!string.IsNullOrEmpty(binding.TextFormat))
        {
            binding.View.SetText(FormatText(binding.TextFormat, current, max));
        }
    }

    static string FormatText(string template, float current, float max)
    {
        // 极简占位符替换；零依赖、零 GC 字符串。复杂格式可后续替换为 StringBuilder。
        return template
            .Replace("{cur}", Mathf.CeilToInt(current).ToString())
            .Replace("{max}", Mathf.CeilToInt(max).ToString());
    }
}

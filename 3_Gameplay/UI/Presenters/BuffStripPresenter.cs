using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Buff 条 Presenter：订阅 <see cref="IBuffStack"/> 的 Applied/Removed，维护 RuntimeId → 图标映射；
/// 每帧从栈中拉取剩余时间（Buff 为低频事件，时间推进在 Entity.Tick 里）。
/// </summary>
[AddComponentMenu("GameMain/UI/Buff Strip Presenter")]
public sealed class BuffStripPresenter : MonoBehaviour
{
    [SerializeField] Transform iconsRoot;

    [Tooltip("单个 Buff 图标预制体（须含 BuffIconView）。")]
    [SerializeField] BuffIconView iconPrefab;

    [Tooltip("同时显示的最大实例数；超出时不再创建。")]
    [SerializeField] int maxVisibleInstances = 16;

    Player m_player;
    IBuffStack m_buffs;
    readonly Dictionary<int, BuffIconView> m_views = new Dictionary<int, BuffIconView>(8);
    bool m_bound;

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
        m_buffs = m_player.Buffs;
        if (m_buffs == null)
        {
            return;
        }

        m_buffs.OnApplied += OnApplied;
        m_buffs.OnRemoved += OnRemoved;
        m_bound = true;

        for (var i = 0; i < m_buffs.ActiveInstanceCount; i++)
        {
            var inst = m_buffs.GetActiveInstanceAt(i);
            if (inst.RuntimeId == 0)
            {
                continue;
            }

            OnApplied(inst);
        }
    }

    public void Unbind()
    {
        if (!m_bound || m_buffs == null)
        {
            ClearAllViews();
            m_bound = false;
            m_player = null;
            m_buffs = null;
            return;
        }

        m_buffs.OnApplied -= OnApplied;
        m_buffs.OnRemoved -= OnRemoved;
        ClearAllViews();
        m_bound = false;
        m_player = null;
        m_buffs = null;
    }

    void OnDestroy() => Unbind();
    void OnDisable() => Unbind();

    void Update()
    {
        if (!m_bound || m_buffs == null)
        {
            return;
        }

        foreach (var kv in m_views)
        {
            if (!m_buffs.TryGetInstance(kv.Key, out var inst))
            {
                continue;
            }

            var def = inst.Definition;
            var n = def != null ? m_buffs.Count(def) : 1;
            kv.Value.Refresh(in inst, n);
        }
    }

    void OnApplied(BuffInstance inst)
    {
        if (iconPrefab == null || iconsRoot == null)
        {
            return;
        }

        if (inst.RuntimeId == 0 || m_views.ContainsKey(inst.RuntimeId))
        {
            return;
        }

        if (m_views.Count >= maxVisibleInstances)
        {
            return;
        }

        var v = Instantiate(iconPrefab, iconsRoot);
        m_views[inst.RuntimeId] = v;
        var n = inst.Definition != null ? m_buffs.Count(inst.Definition) : 1;
        v.Refresh(in inst, n);
    }

    void OnRemoved(BuffInstance inst)
    {
        if (!m_views.TryGetValue(inst.RuntimeId, out var v))
        {
            return;
        }

        m_views.Remove(inst.RuntimeId);
        if (v != null)
        {
            Destroy(v.gameObject);
        }
    }

    void ClearAllViews()
    {
        foreach (var kv in m_views)
        {
            if (kv.Value != null)
            {
                Destroy(kv.Value.gameObject);
            }
        }

        m_views.Clear();
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 178.1 W5 — 离线烘焙 Clip×Clip 稀疏数据库（资料 §285-358）。
/// <para>仅存储 PoseCost &gt; 阈值的对；100 Clip 实测 200~500 条，体积 &lt; 50KB。</para>
/// <para>运行时 OnEnable 构建 Dictionary 提供 O(1) 查询。</para>
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Animation/Transition Database", fileName = "TransitionDatabase_")]
public sealed class TransitionDatabaseSO : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public AnimationClip FromClip;
        public AnimationClip ToClip;

        [Range(0f, 1f)] public float PoseCost;
        [Range(0f, 0.5f)] public float RecommendedBlend;

        [Tooltip("节奏可对齐（足相位接近）—— 允许走 PhaseMatch 模式。")]
        public bool PhaseMatchSafe;

        [Tooltip("From 结尾的足相位（0=左支撑 / 0.5=右支撑）。")]
        public float FootPhase;
    }

    [SerializeField] List<Entry> m_entries = new(64);

    [SerializeField, Tooltip("上次烘焙时间戳（YYYY-MM-DD HH:mm）；调试用。")]
    string m_lastBakedAt;

    [SerializeField, Tooltip("上次烘焙覆盖的 Clip 对数（含全量未过滤前的）。")]
    int m_lastBakedTotalPairs;

    [SerializeField, Tooltip("上次烘焙写入 DB 的条目数（超阈值）。")]
    int m_lastBakedWrittenEntries;

    Dictionary<(int, int), Entry> _lookup;

    public IReadOnlyList<Entry> Entries => m_entries;
    public string LastBakedAt => m_lastBakedAt;
    public int LastBakedTotalPairs => m_lastBakedTotalPairs;
    public int LastBakedWrittenEntries => m_lastBakedWrittenEntries;

    void OnEnable() => RebuildLookup();
    void OnValidate() => RebuildLookup();

    void RebuildLookup()
    {
        _lookup = new Dictionary<(int, int), Entry>(m_entries?.Count ?? 0);
        if (m_entries == null) return;
        for (var i = 0; i < m_entries.Count; i++)
        {
            var e = m_entries[i];
            if (e.FromClip == null || e.ToClip == null) continue;
            var key = (e.FromClip.GetInstanceID(), e.ToClip.GetInstanceID());
            _lookup[key] = e;
        }
    }

    public bool TryGet(AnimationClip from, AnimationClip to, out Entry e)
    {
        e = default;
        if (from == null || to == null) return false;
        if (_lookup == null) RebuildLookup();
        return _lookup.TryGetValue((from.GetInstanceID(), to.GetInstanceID()), out e);
    }

    // ─── Editor / Baker 入口 ───────────────────────────────────

#if UNITY_EDITOR
    public void EditorClearEntries()
    {
        m_entries.Clear();
        _lookup?.Clear();
    }

    public void EditorAddEntry(in Entry e)
    {
        m_entries.Add(e);
        if (_lookup != null && e.FromClip != null && e.ToClip != null)
        {
            _lookup[(e.FromClip.GetInstanceID(), e.ToClip.GetInstanceID())] = e;
        }
    }

    public void EditorSetBakeStats(string timestamp, int totalPairs, int written)
    {
        m_lastBakedAt = timestamp;
        m_lastBakedTotalPairs = totalPairs;
        m_lastBakedWrittenEntries = written;
    }
#endif
}

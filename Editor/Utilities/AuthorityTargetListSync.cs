#if UNITY_EDITOR
using System;
using System.Collections.Generic;

/// <summary>
/// Authority → Target 列表通用 Validate / Sync / AutoFix（160.2 L1）。
/// </summary>
public static class AuthorityTargetListSync
{
    public static AuthorityTargetSyncReport<TKey> Validate<TKey>(
        IReadOnlyCollection<TKey> authority,
        IReadOnlyList<TKey> targetKeys,
        IEqualityComparer<TKey> comparer)
    {
        comparer ??= EqualityComparer<TKey>.Default;
        var report = new AuthorityTargetSyncReport<TKey>();
        if (authority == null || authority.Count == 0)
        {
            return report;
        }

        var targetCounts = new Dictionary<TKey, int>(comparer);
        if (targetKeys != null)
        {
            for (var i = 0; i < targetKeys.Count; i++)
            {
                var key = targetKeys[i];
                if (targetCounts.TryGetValue(key, out var count))
                {
                    targetCounts[key] = count + 1;
                }
                else
                {
                    targetCounts[key] = 1;
                }
            }
        }

        foreach (var key in authority)
        {
            if (!targetCounts.TryGetValue(key, out var count) || count == 0)
            {
                report.Missing.Add(key);
            }
        }

        if (targetKeys != null)
        {
            var authoritySet = new HashSet<TKey>(authority, comparer);
            for (var i = 0; i < targetKeys.Count; i++)
            {
                var key = targetKeys[i];
                if (!authoritySet.Contains(key) && !report.Unused.Contains(key))
                {
                    report.Unused.Add(key);
                }
            }
        }

        foreach (var pair in targetCounts)
        {
            if (pair.Value > 1)
            {
                report.Duplicate.Add(pair.Key);
            }
        }

        return report;
    }

    /// <summary>仅追加 authority 中缺失的 key；已有 Target 行不覆盖。</summary>
    public static int SyncAppendMissing<TKey, TRow>(
        IList<TRow> target,
        IReadOnlyCollection<TKey> authority,
        Func<TKey, TRow> factory,
        Func<TRow, TKey> getKey,
        IEqualityComparer<TKey> comparer)
    {
        if (target == null || authority == null || factory == null || getKey == null)
        {
            return 0;
        }

        comparer ??= EqualityComparer<TKey>.Default;
        var existing = new HashSet<TKey>(comparer);
        for (var i = 0; i < target.Count; i++)
        {
            existing.Add(getKey(target[i]));
        }

        var added = 0;
        foreach (var key in authority)
        {
            if (existing.Contains(key))
            {
                continue;
            }

            target.Add(factory(key));
            existing.Add(key);
            added++;
        }

        return added;
    }

    /// <summary>补 Missing + 去 Duplicate（保留首条）。不删除 Unused。</summary>
    public static int AutoFixSafe<TKey, TRow>(
        IList<TRow> target,
        IReadOnlyCollection<TKey> authority,
        Func<TKey, TRow> factory,
        Func<TRow, TKey> getKey,
        IEqualityComparer<TKey> comparer,
        out int removedDuplicates)
    {
        removedDuplicates = 0;
        if (target == null)
        {
            return 0;
        }

        var added = SyncAppendMissing(target, authority, factory, getKey, comparer);
        removedDuplicates = RemoveDuplicatesKeepFirst(target, getKey, comparer);
        return added;
    }

    /// <summary>删除 Target 中不在 authority 内的行；调用方须先 DisplayDialog 确认。</summary>
    public static int RemoveUnused<TKey, TRow>(
        IList<TRow> target,
        IReadOnlyCollection<TKey> authority,
        Func<TRow, TKey> getKey,
        IEqualityComparer<TKey> comparer)
    {
        if (target == null || authority == null || getKey == null)
        {
            return 0;
        }

        comparer ??= EqualityComparer<TKey>.Default;
        var authoritySet = new HashSet<TKey>(authority, comparer);
        var removed = 0;
        for (var i = target.Count - 1; i >= 0; i--)
        {
            var key = getKey(target[i]);
            if (!authoritySet.Contains(key))
            {
                target.RemoveAt(i);
                removed++;
            }
        }

        return removed;
    }

    static int RemoveDuplicatesKeepFirst<TKey, TRow>(
        IList<TRow> target,
        Func<TRow, TKey> getKey,
        IEqualityComparer<TKey> comparer)
    {
        comparer ??= EqualityComparer<TKey>.Default;
        var seen = new HashSet<TKey>(comparer);
        var removed = 0;
        for (var i = target.Count - 1; i >= 0; i--)
        {
            var key = getKey(target[i]);
            if (!seen.Add(key))
            {
                target.RemoveAt(i);
                removed++;
            }
        }

        return removed;
    }
}
#endif

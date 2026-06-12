#if UNITY_EDITOR
using System.Collections.Generic;

/// <summary>
/// Authority → Target 列表校验结果（160.2 L1）。
/// </summary>
public sealed class AuthorityTargetSyncReport<TKey>
{
    public List<TKey> Missing { get; } = new List<TKey>();
    public List<TKey> Unused { get; } = new List<TKey>();
    public List<TKey> Duplicate { get; } = new List<TKey>();

    public bool HasErrors => Duplicate.Count > 0;
    public bool HasWarnings => Missing.Count > 0 || Unused.Count > 0;
    public bool IsClean => !HasErrors && !HasWarnings;
}

/// <summary>
/// 各 SO 实现 Key 提取与默认行工厂（160.2 L1）。
/// </summary>
public interface IAuthorityTargetSyncAdapter<TKey, TRow>
{
    IReadOnlyList<TKey> GetAuthorityKeys();
    TKey GetRowKey(TRow row);
    TRow CreateDefaultRow(TKey key);
}
#endif

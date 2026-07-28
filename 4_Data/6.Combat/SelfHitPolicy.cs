/// <summary>
/// 217.2 L2 — 自伤策略（相对施法者本体 / 拥有物）。
/// </summary>
public enum SelfHitPolicy : byte
{
    /// <summary>永不命中施法者本体（默认近战）。</summary>
    Never = 0,

    /// <summary>可命中自己（完整结算）。</summary>
    Allow = 1,

    /// <summary>不打本体，可打 Owned 关系目标（分身/召唤物）。</summary>
    AllowOwnedOnly = 2,
}

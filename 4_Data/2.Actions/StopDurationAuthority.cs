/// <summary>238.1 — Stop 会话有效时长的权威来源。</summary>
public enum StopDurationAuthority : byte
{
    /// <summary>兼容 234.6：取物理、Clip 墙钟、Action 默认时长的租约最大值。</summary>
    LegacyLease = 0,

    /// <summary>有效时长直接等于本次物理 Stop 完成时长。</summary>
    PhysicsStop = 1,

    /// <summary>显式使用 Action 默认时长；不自动宣称物理—表现同步。</summary>
    ActionDefault = 2,
}

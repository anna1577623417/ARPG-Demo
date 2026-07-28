using System;

/// <summary>
/// 217.2 L2 — 相对施法者的关系掩码（可多选）。
/// </summary>
[Flags]
public enum AllegianceMask : byte
{
    None = 0,

    Self = 1 << 0,
    Owned = 1 << 1,
    Friendly = 1 << 2,
    Hostile = 1 << 3,
    Neutral = 1 << 4,
}

using System;

/// <summary>
/// HUD 合成展示态 — 由 <see cref="IRouteRuntimeHandle.PresentationState"/> 只读暴露。
/// </summary>
[Flags]
public enum SkillPresentationState
{
    None = 0,
    Ready = 1 << 0,
    Cooling = 1 << 1,
    ResourceBlocked = 1 << 2,
    CastBlocked = 1 << 3,
    Charging = 1 << 4,
    ComboWindow = 1 << 5,
    MultiStagePending = 1 << 6,
}

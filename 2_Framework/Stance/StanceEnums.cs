/// <summary>
/// 177.1 W0 — Stance 系统共用枚举。
/// <para>单值枚举（非 Flags）：同一时刻只能处于一个 Stance；LockOn+Crouch 等组合用 LocomotionFeatureFlags 叠加。</para>
/// </summary>
public enum StanceId : byte
{
    Normal = 0,
    Crouch = 1,
    LockOn = 2,
    Sheathed = 3,
    Bow = 4,
    Shield = 5,
    TwoHand = 6,
    Mounted = 7,
    Aerial = 8,
    Swim = 9,
}

/// <summary>Stance 切换请求原因（用于日志 / Telemetry）。</summary>
public enum StanceTransitionReason : byte
{
    None = 0,
    PlayerInput = 1,     // 玩家主动按键
    AutoFromAir = 2,     // 起跳进入 Aerial
    AutoFromLand = 3,    // 落地回 Normal
    AutoFromMount = 4,   // 上下马
    ScriptForced = 5,    // 剧情 / NPC 触发
    RejectedHeadblock = 6, // 头顶障碍拒绝起立
}

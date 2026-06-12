/// <summary>
/// 8 向锁定移动方位（局部空间）—— 159.1 L1+ §3.3。
///
/// 仅当 <see cref="LocomotionStateBinding.State"/> = <see cref="LocomotionStateId.StrafeLocomotion"/> 时有意义。
/// <para>None = 默认（用于 Binding 配置默认 fallback）。</para>
/// <para>由 PlayerLocomotionState 在 LockedOn + hasInput 时按 MovementIntent vs forward 局部空间 atan2 计算（5.3）。</para>
/// </summary>
public enum StrafeDirection8 : byte
{
    None         = 0,
    Forward      = 1,  // F   (前)
    ForwardLeft  = 2,  // FL  (前左)
    ForwardRight = 3,  // FR  (前右)
    Backward     = 4,  // B   (后)
    BackwardLeft = 5,  // BL  (后左)
    BackwardRight= 6,  // BR  (后右)
    Left         = 7,  // L   (左)
    Right        = 8,  // R   (右)
}

/// <summary>
/// 4 向原地转身 —— 159.1 L1+ §3.3。
///
/// 仅当 <see cref="LocomotionStateBinding.State"/> = <see cref="LocomotionStateId.TurnInPlaceDirected"/> 时有意义。
/// <para>由 <see cref="TurnResolver.TurnInfo"/>（Type=Turn90/Turn180 + Direction=±1）映射而来（5.4）。</para>
/// </summary>
public enum TurnDirection4 : byte
{
    None     = 0,
    Left90   = 1,
    Right90  = 2,
    Left180  = 3,
    Right180 = 4,
}

/// <summary>
/// Binding 对 Walk / Run 速度档位的过滤 —— 159.1 L1+ §3.3。
///
/// 主要用途：<see cref="LocomotionStateId.StrafeLocomotion"/> 同一方向的走 / 跑两条 Binding 区分。
/// 起停四态（WalkEnd / RunEnd / WalkStart / RunStart）已通过独立 State 区分，本字段在那些 State 上保持 <see cref="Any"/> 即可。
/// </summary>
public enum LocomotionRunRequirement : byte
{
    /// <summary>不区分走 / 跑（任何档位都匹配）。</summary>
    Any      = 0,

    /// <summary>仅按走速时匹配（!WantsRun）。</summary>
    WalkOnly = 1,

    /// <summary>仅按跑速时匹配（WantsRun）。</summary>
    RunOnly  = 2,
}

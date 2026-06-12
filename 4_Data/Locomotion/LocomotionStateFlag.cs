using System;
using UnityEngine;

/// <summary>
/// Locomotion 逻辑状态注册位（C 158.2 §5.1）。
/// Inspector 下拉顺序 = 本枚举声明顺序（相关联状态分组排列）。
/// </summary>
[Flags]
public enum LocomotionStateFlag : int
{
    None = 0,

    // ═══ 基础 ═══
    Idle = 1 << 0,
    Dead = 1 << 3,

    // ═══ 走：循环 + 起停 ═══
    Walk      = 1 << 1,
    WalkStart = 1 << 16,
    WalkEnd   = 1 << 14,

    // ═══ 跑：循环 + 起停 ═══
    Run      = 1 << 13,
    RunStart = 1 << 17,
    RunEnd   = 1 << 15,

    // ═══ 空 / 跳：滞空循环 + 起停 ═══
    /// <summary>滞空 JumpLoop ContinuousClip（Id=3；原 Air）。</summary>
    [InspectorName("Air/JumpLoop")]
    AirJumpLoop = 1 << 2,

    JumpStart = 1 << 7,
    JumpLand  = 1 << 8,

    // ═══ 转身 ═══
    TurnInPlaceDirected = 1 << 19,

    // ═══ 锁定方向化（159.1 L1+）═══
    StrafeLocomotion = 1 << 18,

    // ═══ Obsolete（迁移期；Inspector 置底）═══
    [Obsolete("159.3：使用 Walk。位 1<<1 不变。")]
    Move = Walk,

    [Obsolete("159.4：使用 AirJumpLoop（Inspector 显示 Air/JumpLoop）。位 1<<2 不变。")]
    Air = AirJumpLoop,

    [Obsolete("159.1 L0：使用 WalkEnd / RunEnd 拆分版。")]
    Stop = 1 << 4,

    [Obsolete("159.1 L0：使用 WalkStart / RunStart 拆分版。")]
    RunStartLegacy = 1 << 6,

    [Obsolete("161.2：使用 TurnInPlaceDirected（4 向 Composite）。")]
    TurnInPlace = 1 << 9,

    [Obsolete("161.2：使用 StrafeLocomotion（8 向×走跑）。")]
    StrafeLeft = 1 << 10,

    [Obsolete("161.2：使用 StrafeLocomotion。")]
    StrafeRight = 1 << 11,

    [Obsolete("161.2：使用 StrafeLocomotion（Backward 向）。")]
    BackWalk = 1 << 12,

    [Obsolete("162.1：使用 TurnInPlaceDirected。位 1<<5 不变。")]
    Pivot = 1 << 5,
}

/// <summary>
/// <see cref="LocomotionStateFlag"/> 工具方法（避免 boxing / 重复位运算）。
/// </summary>
public static class LocomotionStateFlagExtensions
{
    /// <summary>
    /// Enabled States 下拉 / Bindings 排序的权威顺序（= 本文件枚举声明顺序）。
    /// 禁止用 <see cref="Enum.GetValues"/> —— [Flags] 会按位值排序，与 Inspector 声明序不一致。
    /// </summary>
    public static readonly LocomotionStateFlag[] InspectorMenuOrder =
    {
        LocomotionStateFlag.Idle,
        LocomotionStateFlag.Dead,
        LocomotionStateFlag.Walk,
        LocomotionStateFlag.WalkStart,
        LocomotionStateFlag.WalkEnd,
        LocomotionStateFlag.Run,
        LocomotionStateFlag.RunStart,
        LocomotionStateFlag.RunEnd,
        LocomotionStateFlag.AirJumpLoop,
        LocomotionStateFlag.JumpStart,
        LocomotionStateFlag.JumpLand,
        LocomotionStateFlag.TurnInPlaceDirected,
        LocomotionStateFlag.StrafeLocomotion,
    };

    /// <summary>应从 EnabledStates 剥离的废弃位（不含 Move/Air 等同名别名）。</summary>
    public static LocomotionStateFlag ObsoleteEnabledBitsMask
    {
        get
        {
#pragma warning disable CS0618
            return LocomotionStateFlag.Stop
                   | LocomotionStateFlag.RunStartLegacy
                   | LocomotionStateFlag.TurnInPlace
                   | LocomotionStateFlag.StrafeLeft
                   | LocomotionStateFlag.StrafeRight
                   | LocomotionStateFlag.BackWalk
                   | LocomotionStateFlag.Pivot;
#pragma warning restore CS0618
        }
    }

    public static bool IsEnabledIn(this LocomotionStateFlag state, LocomotionStateFlag enabledSet)
    {
        return state != LocomotionStateFlag.None && (enabledSet & state) == state;
    }

    public static LocomotionStateFlag SanitizeEnabledStates(LocomotionStateFlag flags) =>
        flags & ~ObsoleteEnabledBitsMask;
}

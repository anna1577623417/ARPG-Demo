using System;
using UnityEngine;

/// <summary>
/// Locomotion 状态标识（单值，<b>非 Flags</b>）。
/// Inspector EnumPopup 顺序 = 本枚举声明顺序（相关联状态分组排列）。
/// 数值 Id N ↔ Flag 位 (1 &lt;&lt; (N-1))；改名不改数值。
/// </summary>
public enum LocomotionStateId : byte
{
    None = 0,

    // ═══ 基础 ═══
    Idle = 1,
    Dead = 4,

    // ═══ 走：循环 + 起停 ═══
    Walk      = 2,
    WalkStart = 17,
    WalkEnd   = 15,

    // ═══ 跑：循环 + 起停 ═══
    Run      = 14,
    RunStart = 18,
    RunEnd   = 16,

    // ═══ 空 / 跳：滞空循环 + 起停 ═══
    /// <summary>滞空 JumpLoop ContinuousClip（原 Air=3）。</summary>
    [InspectorName("Air/JumpLoop")]
    AirJumpLoop = 3,

    JumpStart = 8,
    JumpLand  = 9,

    /// <summary>164.1/165.1 L11：中高度落地（EnableTieredLanding 时由 Profile 查表）。</summary>
    JumpLandHeavy = 21,

    /// <summary>164.1/165.1 L11：高高度翻滚落地（EnableTieredLanding 时由 Profile 查表）。</summary>
    JumpLandRoll = 22,

    // ═══ 转身 ═══
    TurnInPlaceDirected = 20,

    // ═══ 锁定方向化（159.1 L1+）═══
    StrafeLocomotion = 19,

    // ═══ Obsolete（迁移期；Inspector 置底）═══
    [Obsolete("159.3：使用 Walk。Id=2 不变。")]
    Move = Walk,

    [Obsolete("159.4：使用 AirJumpLoop（Inspector 显示 Air/JumpLoop）。Id=3 不变。")]
    Air = AirJumpLoop,

    [Obsolete("159.1 L0：使用 WalkEnd / RunEnd 拆分版。")]
    Stop = 5,

    [Obsolete("159.1 L0：使用 WalkStart=17 / RunStart=18 拆分版。")]
    RunStartLegacy = 7,

    [Obsolete("161.2：使用 TurnInPlaceDirected。")]
    TurnInPlace = 10,

    [Obsolete("161.2：使用 StrafeLocomotion。")]
    StrafeLeft = 11,

    [Obsolete("161.2：使用 StrafeLocomotion。")]
    StrafeRight = 12,

    [Obsolete("161.2：使用 StrafeLocomotion。")]
    BackWalk = 13,

    [Obsolete("162.1：使用 TurnInPlaceDirected.Left180/Right180。Id=6 不变。")]
    Pivot = 6,
}

/// <summary>
/// <see cref="LocomotionStateId"/> ↔ <see cref="LocomotionStateFlag"/> 双向转换 + 性质分类。
/// </summary>
public static class LocomotionStateIdExtensions
{
    public static LocomotionStateFlag ToFlag(this LocomotionStateId id)
    {
        if (id == LocomotionStateId.None) return LocomotionStateFlag.None;
        return (LocomotionStateFlag)(1 << ((int)id - 1));
    }

    public static LocomotionStateId ToId(this LocomotionStateFlag flag)
    {
        var v = (int)flag;
        if (v == 0) return LocomotionStateId.None;
        if ((v & (v - 1)) != 0) return LocomotionStateId.None;
        for (var i = 0; i < 32; i++)
        {
            if (v == (1 << i)) return (LocomotionStateId)(i + 1);
        }

        return LocomotionStateId.None;
    }

    public static bool IsDiscrete(this LocomotionStateId id)
    {
        switch (id)
        {
#pragma warning disable CS0618
            case LocomotionStateId.Stop:
            case LocomotionStateId.RunStartLegacy:
#pragma warning restore CS0618
            case LocomotionStateId.JumpStart:
            case LocomotionStateId.JumpLand:
            case LocomotionStateId.JumpLandHeavy:
            case LocomotionStateId.JumpLandRoll:
            case LocomotionStateId.Dead:
            case LocomotionStateId.WalkEnd:
            case LocomotionStateId.RunEnd:
            case LocomotionStateId.WalkStart:
            case LocomotionStateId.RunStart:
                return true;
            case LocomotionStateId.TurnInPlaceDirected:
                return false;
            default:
                return false;
        }
    }

    public static bool IsContinuous(this LocomotionStateId id)
    {
        switch (id)
        {
            case LocomotionStateId.Idle:
            case LocomotionStateId.Walk:
            case LocomotionStateId.AirJumpLoop:
            case LocomotionStateId.Run:
            case LocomotionStateId.StrafeLocomotion:
            case LocomotionStateId.TurnInPlaceDirected:
                return true;
            default:
                return false;
        }
    }

    /// <summary>废弃 Locomotion 态（Detect 不产出；Bindings 应删除）。</summary>
    public static bool IsObsoleteLocomotionState(this LocomotionStateId id)
    {
#pragma warning disable CS0618
        return id == LocomotionStateId.Stop
               || id == LocomotionStateId.RunStartLegacy
               || id == LocomotionStateId.TurnInPlace
               || id == LocomotionStateId.StrafeLeft
               || id == LocomotionStateId.StrafeRight
               || id == LocomotionStateId.BackWalk
               || id == LocomotionStateId.Pivot;
#pragma warning restore CS0618
    }
}

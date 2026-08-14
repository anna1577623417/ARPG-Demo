using System;
using UnityEngine;

/// <summary>
/// InheritPhysics 积分停止标定。Loop 走封顶积分；Tap 走离散配方。
/// </summary>
[Serializable]
public struct InheritPhysicsSettings
{
    [HideInInspector]
    public float MinSpeed;

    [Tooltip("最大滑行速度 V_max 回退（米/秒）。Play 优先用松开时的 WalkSpeed/RunSpeed。")]
    [Min(0.01f)]
    public float MaxSpeed;

    [HideInInspector]
    public float MinDistance;

    [Tooltip("满速停止标定回退（米）。FullSpeedStopDistance 未填时作为 D_ref。")]
    [Min(0f)]
    public float MaxDistance;

    [Tooltip("满速停止距离 D_ref（米）。>0 时作为积分标定；0 = 未填，回退 MaxDistance。")]
    [Min(0f)]
    public float FullSpeedStopDistance;

    [HideInInspector]
    public float MinDuration;

    [HideInInspector]
    public float MaxDuration;

    [Tooltip("最大物理刹车时间 T_max（秒）。0 = 用 2·D_ref/V_max。")]
    [Min(0f)]
    public float MaxBrakeSeconds;

    [Tooltip("点按判定窗（秒）。heldSec≤此值走 Tap 配方。0 = 0.15。")]
    [Min(0f)]
    public float TapWindowSeconds;

    [Tooltip("点按表现租约 T_tap（秒）。0 = 0.15。Tap 租约不吃满段 Clip 墙钟。")]
    [Min(0f)]
    public float TapPresentationSeconds;

    [Tooltip("点按固定位移（米）。与入场速度无关。0 = 运行时按 0.1。")]
    [Min(0f)]
    public float TapStopDistance;

    [Tooltip("点按 Clip 起播归一化。与 TapTailSeconds 同为 0 = Auto（最后 T_tap 秒）。拖 Segment 左柄写入。")]
    [Range(0f, 1f)]
    public float TapTailStartNormalized;

    [Tooltip("点按尾段墙钟（秒）。0 且起播也为 0 = Auto。拖 Segment 左右柄跨度写入，同时抬租约下限。")]
    [Min(0f)]
    public float TapTailSeconds;

    [Tooltip("连点最大发数。仅无限连点关闭时采用。1 = 仅首发；大于 1 为最多发数。0 对既有资产仍视为无限。")]
    [Min(0)]
    public int TapChainMax;

    [Tooltip("无限连点。开启后忽略连点最大发，字段只读灰显。")]
    public bool TapChainUnlimited;

    [Tooltip("是否在 X 轴（左右）施加滑行位移。")]
    public bool AffectX;

    [Tooltip("是否在 Y 轴（垂直）施加滑行位移。一般不勾。")]
    public bool AffectY;

    [Tooltip("是否在 Z 轴（前后）施加滑行位移。Run/Walk 急停默认勾选。")]
    public bool AffectZ;

    public static InheritPhysicsSettings Default => new()
    {
        MinSpeed = 1f,
        MaxSpeed = 8f,
        MinDistance = 0.2f,
        MaxDistance = 2.5f,
        FullSpeedStopDistance = 0f,
        MinDuration = 0.10f,
        MaxDuration = 0.45f,
        MaxBrakeSeconds = 0f,
        TapWindowSeconds = 0.15f,
        TapPresentationSeconds = 0.15f,
        TapStopDistance = 0.1f,
        TapTailStartNormalized = 0f,
        TapTailSeconds = 0f,
        TapChainMax = 0,
        TapChainUnlimited = true,
        AffectX = false,
        AffectY = false,
        AffectZ = true,
    };
}

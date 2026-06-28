using System;
using UnityEngine;

/// <summary>
/// 182.1 — InheritPhysics 运行时映射：入场速度 → 滑行距离 / 滑行时长。
///
/// 单位与策划语言（与 StatType 系统对齐）：
///   · 速度 m/s：参考 StatType.WalkSpeed (默认 5)、StatType.RunSpeed (默认 8)
///   · 距离 m  ：玩家角色尺度约 1.8m 高，1 步 ≈ 0.6m
///   · 时长 s  ：典型急停动画 0.3~0.6s
///
/// 推荐配置（紧贴角色移速曲线）：
///   · 走路停止：MinSpeed≈WalkSpeed*0.5  / MaxSpeed≈WalkSpeed*1.1
///   · 跑步停止：MinSpeed≈RunSpeed*0.5   / MaxSpeed≈RunSpeed*1.2
/// </summary>
[Serializable]
public struct InheritPhysicsSettings
{
    [Tooltip("入场速度下限（米/秒）。\n" +
             "玩家入场速度 ≤ 此值时，按 (MinDistance, MinDuration) 滑行。\n" +
             "参考：StatType.WalkSpeed 默认 5 m/s；StatType.RunSpeed 默认 8 m/s。")]
    [Min(0f)]
    public float MinSpeed;

    [Tooltip("入场速度上限（米/秒）。\n" +
             "玩家入场速度 ≥ 此值时，按 (MaxDistance, MaxDuration) 滑行。\n" +
             "之间速度按线性插值。")]
    [Min(0.01f)]
    public float MaxSpeed;

    [Tooltip("最短滑行距离（米）。\n" +
             "入场速度处于 MinSpeed 时的滑行米数。\n" +
             "参考：1 步 ≈ 0.6m；角色身高 ≈ 1.8m。")]
    [Min(0f)]
    public float MinDistance;

    [Tooltip("最长滑行距离（米）。\n" +
             "入场速度处于 MaxSpeed 时的滑行米数。")]
    [Min(0f)]
    public float MaxDistance;

    [Tooltip("最短滑行时长（秒）。\n" +
             "入场速度处于 MinSpeed 时的减速时间。")]
    [Min(0.01f)]
    public float MinDuration;

    [Tooltip("最长滑行时长（秒）。\n" +
             "入场速度处于 MaxSpeed 时的减速时间。\n" +
             "典型急停动画 0.3~0.6 秒。")]
    [Min(0.01f)]
    public float MaxDuration;

    [Tooltip("是否在 X 轴（左右）施加滑行位移。")]
    public bool AffectX;

    [Tooltip("是否在 Y 轴（垂直）施加滑行位移。一般不勾。")]
    public bool AffectY;

    [Tooltip("是否在 Z 轴（前后）施加滑行位移。Run/Walk 急停默认勾选。")]
    public bool AffectZ;

    /// <summary>
    /// 默认值贴近玩家 RunSpeed (8 m/s) 配置（与 PlayerStats_General.asset 对齐）。
    /// 策划新建 Action 时即可获得合理的初始急停手感。
    /// </summary>
    public static InheritPhysicsSettings Default => new()
    {
        MinSpeed = 1f,    // ≈ WalkSpeed (5) × 0.2，缓步时几乎不滑
        MaxSpeed = 8f,    // = RunSpeed 上限
        MinDistance = 0.2f,  // ~1/3 步
        MaxDistance = 2.5f,  // ~4 步
        MinDuration = 0.10f,
        MaxDuration = 0.45f, // 典型 RunEnd 时长
        AffectX = false,
        AffectY = false,
        AffectZ = true,      // 默认仅前后向滑行
    };
}

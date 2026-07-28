using System;
using UnityEngine;

/// <summary>
/// 216.3 M5 L1 — 防御片段（Guard / Parry / Invincible 窗）。
/// <para>挂在 <see cref="ActionDataSO.DefenseClips"/>；TL Guard 轨编辑 Active 区间。</para>
/// <para>Guard：运行时由 <c>GuardVolumeProvider</c> 在 Active 内生成前向 Volume。</para>
/// </summary>
[Serializable]
public struct DefenseClip
{
    [Tooltip("调试名；空则用 Kind。")]
    public string DebugName;

    [Tooltip("防御窗起点（归一化 0~1）。")]
    [Range(0f, 1f)]
    public float ActiveStart;

    [Tooltip("防御窗终点（归一化 0~1）。")]
    [Range(0f, 1f)]
    public float ActiveEnd;

    [Tooltip("Guard / Parry / Invincible。")]
    public DefenseKind Kind;

    [Header("Guard Volume（仅 Kind=Guard）")]
    [Tooltip("前向扇形半角（度）。默认 120 = 正面宽扇。")]
    [Range(1f, 180f)]
    public float GuardAngleDegrees;

    [Tooltip("前向有效距离（米）。")]
    [Min(0.1f)]
    public float GuardRange;

    [Tooltip("可选：用 HitShape 精确体积；空则用 Angle+Range 扇形近似。")]
    public HitShapeSO GuardShape;

    public string ResolvedName =>
        string.IsNullOrEmpty(DebugName) ? Kind.ToString() : DebugName;
}

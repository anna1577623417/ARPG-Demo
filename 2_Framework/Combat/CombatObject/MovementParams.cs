using System;
using UnityEngine;

/// <summary>
/// 188.3 W4 — Movement 参数 struct（嵌入 CombatObjectDefinitionSO）。
/// <para>Kind 决定 CombatObjectRuntime.ComputeWorldPosition 走哪条分支；其它字段按 Kind 选用。</para>
/// </summary>
[Serializable]
public struct MovementParams
{
    public MovementKind Kind;

    // ─── Linear / Homing ────────────────────────────────────
    [Header("Linear / Homing")]
    [Tooltip("m/s；Linear=匀速；Homing=向目标飞的基速。")]
    [Min(0f)] public float Speed;

    [Tooltip("最大位移距离（米）；到达后位置锁定。Linear 用。")]
    [Min(0f)] public float MaxDistance;

    // ─── Curve（P2 W11）────────────────────────────────────
    [Header("Curve (P2)")]
    [Tooltip("局部 X 偏移随归一化时间 t∈[0,1] 变化（米）。")]
    public AnimationCurve LocalOffsetXOverTime;

    public AnimationCurve LocalOffsetYOverTime;

    public AnimationCurve LocalOffsetZOverTime;

    // ─── Expand（P2 W12）───────────────────────────────────
    [Header("Expand (P2)")]
    [Min(0f)] public float StartRadius;
    [Min(0f)] public float EndRadius;

    [Tooltip("0=匀速扩张；EaseOut/Bell 等可控变速。")]
    public AnimationCurve ExpandCurve;

    // ─── Homing（P2 W13）───────────────────────────────────
    [Header("Homing (P2)")]
    [Tooltip("每秒转向速率（度）；0=直线无追踪。")]
    [Min(0f)] public float TurnRateDegPerSec;

    /// <summary>188.3 — 默认 Static 行为（兼容旧资产）。</summary>
    public static MovementParams DefaultStatic => new()
    {
        Kind = MovementKind.Static,
        Speed = 0f,
        MaxDistance = 0f,
    };

    /// <summary>188.3 — 飞弹（Linear）模板。</summary>
    public static MovementParams DefaultLinear(float speed = 20f, float maxDist = 15f) => new()
    {
        Kind = MovementKind.Linear,
        Speed = speed,
        MaxDistance = maxDist,
    };
}

using System;
using UnityEngine;

/// <summary>
/// 216.3 M1 — 攻击判定片段【作者数据】：一次攻击「何时开判 / 用什么形状 / 从哪发起 / 打多远 / 打谁」。
/// <para>设计师视角：拖 <see cref="ActiveStart"/>/<see cref="ActiveEnd"/> 决定判定开关时机（归一化），
/// 不直接编碰撞体尺寸（见 216.2）。运行时由 <c>AttackInstance</c> 在 Active 区间内 Sweep 判定。</para>
/// <para>M2 追加 <c>HitPolicyParams Policy</c>；M3 追加 <c>HitReaction Reaction</c>（经 Resolver→Event 消费）。</para>
/// </summary>
[Serializable]
public struct HitClip
{
    [Tooltip("调试名（连段区分：Slash1 / Slash2…）。")]
    public string DebugName;

    [Header("Active 区间（判定开关，归一化 0~1）")]
    [Range(0f, 1f)] public float ActiveStart;
    [Range(0f, 1f)] public float ActiveEnd;

    [Header("Shape Provider（判定几何）")]
    [Tooltip("216.3 M4 L3：Volume=HitShape；WeaponTrace=Socket 多点扫掠。二选一，不双跑。")]
    public HitShapeMode ShapeMode;

    [Tooltip("Volume 模式：判定形状（Sphere/Capsule/Box…）。")]
    public HitShapeSO Shape;

    [Tooltip("WeaponTrace 模式：武器 Socket 集合。")]
    public WeaponSocketSetSO WeaponSockets;

    [Header("Origin（判定发起点，语义化）")]
    [Tooltip("判定发起源（骨骼/根/手…），复用 CombatObject 的 SpawnSource 语义。")]
    public SpawnSource Origin;

    [Tooltip("相对 Origin 的局部偏移。")]
    public Vector3 OriginOffset;

    [Tooltip("相对 Origin 的局部欧拉旋转。")]
    public Vector3 OriginEuler;

    [Header("Reach（能打多远，语义参数）")]
    [Tooltip("语义「攻击够到的距离」。M6 可视化用；映射到 Shape 尺寸/前伸由 Provider 解释。")]
    [Min(0f)] public float Reach;

    [Header("Target / Query")]
    [Tooltip("目标筛选（阵营/队伍），复用 214.4 TargetFilterParams。")]
    public TargetFilterParams Filter;

    [Tooltip("物理查询层掩码。")]
    public LayerMask QueryLayerMask;

    [Header("Hit Policy（命中策略）")]
    [Tooltip("216.3 M2：Single / PerTarget / PerSwing / Interval / Continuous / Multi。")]
    public HitPolicyParams Policy;

    [Header("Hit Reaction（命中反应）")]
    [Tooltip("216.3 M3：伤害/击退/HitStop/震屏/FX/SFX。判定层不消费；经 Resolver→CombatEvent。")]
    public HitReaction Reaction;
}

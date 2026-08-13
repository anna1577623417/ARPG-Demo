using System;
using UnityEngine;

/// <summary>
/// Action Contact 在 Active Window 内的空间运动语义。
/// Legacy（224.1）：新数据请使用 BindingMode + SweepPolicy；本枚举仅 Adapter/旧 Runtime 读取，L8 删除。
/// </summary>
public enum ContactMotionKind : byte
{
    StaticAtSpawn = 0,
    FollowAnchor = 1,
    SweepBetweenFrames = 2,
}

/// <summary>
/// 纯几何之上的可复用摆放预设。Definition 拥有攻击语义，Preset 拥有默认空间外观。
/// Legacy Placement/Motion 字段仅供迁移 Adapter；新 ActionContact 空间真相在 CO.ActionContactAuthoring。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Combat/Attack Shape Preset", fileName = "AttackShapePreset_")]
public sealed class AttackShapePresetSO : ScriptableObject
{
    public HitShapeMode ShapeMode = HitShapeMode.Volume;
    public HitShapeSO Geometry;
    public WeaponSocketSetSO WeaponSockets;
    public WeaponSocketLayoutSO WeaponSocketLayout;

    [Tooltip("Legacy：DefaultOrigin。新路径请写 CombatObject.ActionContactAuthoring.Origin。")]
    public SpawnSource DefaultOrigin = SpawnSource.SelfRootBone;

    [Tooltip("Legacy：DefaultLocalOffset。")]
    public Vector3 DefaultLocalOffset;

    [Tooltip("Legacy：DefaultLocalEuler。")]
    public Vector3 DefaultLocalEuler;

    [Tooltip("Legacy：DefaultMotion。新路径请写 BindingMode + SweepPolicy。")]
    public ContactMotionKind DefaultMotion = ContactMotionKind.SweepBetweenFrames;
}

/// <summary>统一 Physics 粗筛与 Gameplay 目标语义。</summary>
[Serializable]
public struct ContactQueryPolicy
{
    public LayerMask LayerMask;
    public QueryTriggerInteraction TriggerInteraction;
    public TargetProfile Target;

    public static ContactQueryPolicy Default => new ContactQueryPolicy
    {
        LayerMask = ~0,
        TriggerInteraction = QueryTriggerInteraction.Collide,
        Target = TargetProfile.DamageEnemyCombatants,
    };
}

/// <summary>
/// Definition 的攻击载荷入口。L1 先复用已生产验证的 HitReaction，
/// 后续 Outcome Landing 可在不改变 Contact Definition 引用关系的前提下扩展。
/// </summary>
[Serializable]
public struct CombatAttackProfile
{
    public HitReaction Reaction;

    public static CombatAttackProfile Default => new CombatAttackProfile
    {
        Reaction = HitReaction.Default,
    };
}

public enum CombatOutcomeAuthoringKind : byte
{
    HitReaction = 0,
    DamageDefinition = 1,
}

/// <summary>
/// V2 的统一 Outcome 作者输入。Runtime 只接收解析后的 OutcomeSet；
/// DamageDefinition/HitReaction 仅作为两种明确的作者 Adapter，不再隐式择优。
/// </summary>
[Serializable]
public struct CombatOutcomeProfile
{
    public CombatOutcomeAuthoringKind Kind;
    public HitReaction Reaction;
    public DamageDefinitionSO DamageDefinition;

    public static CombatOutcomeProfile FromReaction(in HitReaction reaction) =>
        new CombatOutcomeProfile
        {
            Kind = CombatOutcomeAuthoringKind.HitReaction,
            Reaction = reaction,
            DamageDefinition = null,
        };

    public static CombatOutcomeProfile FromDamage(DamageDefinitionSO definition) =>
        new CombatOutcomeProfile
        {
            Kind = CombatOutcomeAuthoringKind.DamageDefinition,
            Reaction = default,
            DamageDefinition = definition,
        };
}

/// <summary>
/// 事件级可选覆盖；默认只覆盖空间摆放和 Motion，不复制攻击载荷。
/// Legacy（224.1）：新作者 UI 不得再写入；仅 Adapter 只读，L8 删除。
/// </summary>
[Serializable]
public struct ContactOverrideData
{
    public bool OverridePlacement;
    public SpawnSource Origin;
    public Vector3 LocalOffset;
    public Vector3 LocalEuler;
    public bool OverrideMotion;
    public ContactMotionKind Motion;
}

/// <summary>稳定身份生成集中在一处；数组重排不改变 EventId，复制必须生成新值。</summary>
public static class ContactEventId
{
    public static string NewId() => Guid.NewGuid().ToString("N");

    public static bool IsValid(string value) =>
        !string.IsNullOrWhiteSpace(value) && Guid.TryParseExact(value, "N", out _);
}

/// <summary>Action Timeline 上的一次 Action Contact Active Window。</summary>
[Serializable]
public struct ContactEvent
{
    [Tooltip("稳定事件身份；不使用数组索引作为 Scene/Runtime 身份。")]
    public string EventId;

    public string DebugName;

    [Tooltip("224.1 — 绑定 ActionWindow.WindowId。有效且 Action.WindowAuthoringVersion=V1 时为唯一时间源。")]
    public string WindowId;

    [Tooltip("Legacy：ActiveStart。新路径请用 WindowId；L8 前只读。")]
    [Range(0f, 1f)] public float ActiveStart;

    [Tooltip("Legacy：ActiveEnd。")]
    [Range(0f, 1f)] public float ActiveEnd;

    [Tooltip("只接受 Archetype=ActionContact。")]
    public CombatObjectDefinitionSO Definition;

    [Tooltip("Legacy Placement/Motion Override；新作者 UI 不得写入。")]
    public ContactOverrideData Override;
}

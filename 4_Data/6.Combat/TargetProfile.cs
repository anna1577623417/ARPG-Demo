using System;
using UnityEngine;

/// <summary>
/// 217.2 L2 — Contact/CombatObject 目标配置：关系 × 单位类 × 自伤策略。
/// <para>裁决见 <c>TargetProfileEvaluator</c>；CombatObject 仍用 <see cref="TargetFilterParams"/>。</para>
/// </summary>
[Serializable]
public struct TargetProfile
{
    [Tooltip("允许的相对关系（Self/Owned/Friendly/Hostile/Neutral）。")]
    public AllegianceMask Relations;

    [Tooltip("允许的单位主类；None=全关（217.1 D2 安全默认）。")]
    public UnitKindMask UnitKinds;

    [Tooltip("自伤策略；默认 Never。")]
    public SelfHitPolicy SelfHit;

    [Tooltip("IncludeDead=true 时允许命中已死亡 Entity。")]
    public bool IncludeDead;

    [Tooltip("RequireSelectable=true 时跳过不可选中目标（L2 占位，暂未接 Tag）。")]
    public bool RequireSelectable;

    public static UnitKindMask MaskFor(UnitKind kind) =>
        (UnitKindMask)(1 << (int)kind);

    /// <summary>217.1 例 A — 近战伤害：敌+中立战斗单位，不打自己。</summary>
    public static TargetProfile DamageEnemyCombatants => new TargetProfile
    {
        Relations = AllegianceMask.Hostile | AllegianceMask.Neutral,
        UnitKinds = UnitKindMask.StandardCombatants,
        SelfHit = SelfHitPolicy.Never,
        IncludeDead = false,
    };

    /// <summary>仅敌对阵营战斗单位（Hook T0 等价）。</summary>
    public static TargetProfile HostileCombatantsOnly => new TargetProfile
    {
        Relations = AllegianceMask.Hostile,
        UnitKinds = UnitKindMask.StandardCombatants,
        SelfHit = SelfHitPolicy.Never,
        IncludeDead = false,
    };

    /// <summary>友方+自身治疗。</summary>
    public static TargetProfile HealAllies => new TargetProfile
    {
        Relations = AllegianceMask.Friendly | AllegianceMask.Self,
        UnitKinds = UnitKindMask.Hero | UnitKindMask.HeroClone,
        SelfHit = SelfHitPolicy.Allow,
        IncludeDead = false,
    };

    /// <summary>仅敌方小兵清线（217.1 例 C）。</summary>
    public static TargetProfile ClearMinionsOnly => new TargetProfile
    {
        Relations = AllegianceMask.Hostile,
        UnitKinds = UnitKindMask.Minion,
        SelfHit = SelfHitPolicy.Never,
        IncludeDead = false,
    };

    /// <summary>217.1 例 D — 仅 Owned 召唤物/分身（SelfHit AllowOwnedOnly）。</summary>
    public static TargetProfile DamageOwnedSummons => new TargetProfile
    {
        Relations = AllegianceMask.Owned,
        UnitKinds = UnitKindMask.Summon | UnitKindMask.HeroClone,
        SelfHit = SelfHitPolicy.AllowOwnedOnly,
        IncludeDead = false,
    };
}

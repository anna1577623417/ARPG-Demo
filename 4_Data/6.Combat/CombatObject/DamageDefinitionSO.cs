using UnityEngine;

/// <summary>
/// 188.3 W5 — 伤害定义资产（CombatObject 命中后的效果描述）。
/// <para>命中时 CombatObjectRuntime 填 DamageInfo → 调既有 DamagePipeline.Compute 走 4 Stage。</para>
/// <para>OnHitEffect 与 180.1 Effect/Buff 子系统对接（命中即 Apply）。</para>
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Combat/Damage Definition", fileName = "DamageDef_")]
public sealed class DamageDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    public string DisplayName;

    [Header("Kind")]
    public DamageKind Kind = DamageKind.Instant;

    [Header("Base Amount")]
    [Tooltip("基础伤害值（Heal Kind 时为治疗量）。走 DamagePipeline → 实际值由 4 Stage 计算。")]
    [Min(0f)] public float Amount = 10f;

    [Header("Force（Knockback / Launch）")]
    [Tooltip("击退方向（局部空间；x=右 / z=前）。Knockback Kind 时由 CombatObject 朝向旋转后施加到 Motor。")]
    public Vector3 KnockbackLocalDir = Vector3.forward;

    [Tooltip("击退力度（米/秒 起始速度）。")]
    [Min(0f)] public float KnockbackForce = 8f;

    [Tooltip("升龙向上推力（米/秒）。Launch Kind 时使用。")]
    [Min(0f)] public float LaunchUpSpeed = 12f;

    [Header("On Hit Effect (180.1 Apply)")]
    [Tooltip("命中目标后挂的 Effect（如燃烧 / 减速）。InstantPlusEffect Kind 时生效，其它 Kind 也可挂。")]
    public EffectDefinitionSO OnHitEffect;

    [Tooltip("OnHitEffect 来源类别（用于按 Source 回收）。")]
    public EffectSourceKind OnHitEffectSource = EffectSourceKind.Skill;
}

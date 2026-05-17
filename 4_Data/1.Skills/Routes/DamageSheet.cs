using System;
using UnityEngine;

/// <summary>
/// 伤害模板（入口语义层 — 每个 Route / Stage 一份）。
/// 仅承载「本招打多少 / 怎么按等级缩放 / 是否暴击豁免」等静态描述；
/// 实际伤害计算由 DamagePipeline 在 HitFrame 命中时合成 CombatContext 后跑四段。
/// </summary>
[Serializable]
public struct DamageSheet
{
    [SerializeField, Min(0f), Tooltip("基础伤害。配置 0 视为非伤害招式（控制 / 位移 / Buff 类）。")]
    private float baseDamage;

    [SerializeField, Min(0f), Tooltip("攻击力倍率 — 进入 BaseDamageStage 时与角色 Attack 相乘。")]
    private float attackScale;

    [SerializeField, Tooltip("按等级缩放曲线 — X = 技能等级，Y = 倍率。为空视作恒 1。")]
    private AnimationCurve damageByLevel;

    [SerializeField, Tooltip("Charge 倍率合成口径：若 Route 是 ChargeRoute，本招最终乘以 ChargeProgress 曲线。")]
    private bool multiplyByChargeProgress;

    [SerializeField, Range(0f, 1f), Tooltip("暴击率覆盖（>0 时覆盖角色基础暴击）。配 -1 视作未覆盖，使用角色 Stats。")]
    private float critChanceOverride;

    [SerializeField, Min(1f), Tooltip("暴击倍率覆盖（>0 时覆盖角色基础暴伤）。配 1 视作未覆盖。")]
    private float critMultiplierOverride;

    public float BaseDamage => baseDamage;
    public float AttackScale => attackScale;
    public bool MultiplyByChargeProgress => multiplyByChargeProgress;
    public float CritChanceOverride => critChanceOverride;
    public float CritMultiplierOverride => critMultiplierOverride;

    /// <summary>等级倍率采样；曲线缺失返回 1。</summary>
    public float SampleLevelScale(int level)
    {
        if (damageByLevel == null || damageByLevel.length == 0)
        {
            return 1f;
        }

        return Mathf.Max(0f, damageByLevel.Evaluate(level));
    }

    /// <summary>非伤害招式判定（基础 0 且未配等级曲线）。</summary>
    public bool IsNonDamage()
    {
        return baseDamage <= 0.0001f && (damageByLevel == null || damageByLevel.length == 0);
    }

    public static DamageSheet None => new DamageSheet
    {
        baseDamage = 0f,
        attackScale = 1f,
        damageByLevel = null,
        multiplyByChargeProgress = false,
        critChanceOverride = -1f,
        critMultiplierOverride = 1f,
    };
}

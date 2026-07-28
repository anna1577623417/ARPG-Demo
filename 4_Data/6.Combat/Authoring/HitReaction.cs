using System;
using UnityEngine;

/// <summary>
/// 216.3 M3 — 命中反应（作者数据）：设计师在 HitClip 上一处编「命中后全部效果」。
/// <para>L1 仅数据结构；L2 经 CombatResolver → CombatEvent 订阅方消费（伤害/击退/HitStop…）。
/// 判定层（AttackInstance）<b>不</b>直接读本结构写 HP。</para>
/// </summary>
[Serializable]
public struct HitReaction
{
    [Header("Damage")]
    [Tooltip("基础伤害（进 DamagePipeline 的 BaseDamage；Stats 修正见 M3 L3）。")]
    [Min(0f)]
    public float BaseDamage;

    [Header("Impulse / Launch")]
    [Tooltip("击退方向（攻击者局部：z=前）。")]
    public Vector3 ImpulseLocalDir;

    [Tooltip("击退力度（米/秒）。0 = 无击退。")]
    [Min(0f)]
    public float ImpulseForce;

    [Tooltip("向上升龙速度（米/秒）。0 = 无 Launch。")]
    [Min(0f)]
    public float LaunchUpSpeed;

    [Header("HitStop / Camera")]
    [Tooltip("命中顿帧秒数。0 = 无 HitStop。")]
    [Min(0f)]
    public float HitStopSeconds;

    [Tooltip("震屏强度。0 = 无。")]
    [Min(0f)]
    public float CameraShakeIntensity;

    [Tooltip("震屏时长（秒）。")]
    [Min(0f)]
    public float CameraShakeDuration;

    [Header("Presentation Payloads")]
    [Tooltip("VFX 载荷键（表现层解析；可空）。")]
    public string VfxPayload;

    [Tooltip("SFX 载荷键（表现层解析；可空）。")]
    public string SfxPayload;

    [Header("On Hit Effect")]
    [Tooltip("命中后挂的 Effect（M3 L3 接 EffectStack）。")]
    public EffectDefinitionSO OnHitEffect;

    public static HitReaction Default => new HitReaction
    {
        BaseDamage = 10f,
        ImpulseLocalDir = Vector3.forward,
        ImpulseForce = 0f,
        LaunchUpSpeed = 0f,
        HitStopSeconds = 0.06f,
        CameraShakeIntensity = 0f,
        CameraShakeDuration = 0.12f,
        VfxPayload = string.Empty,
        SfxPayload = string.Empty,
        OnHitEffect = null,
    };
}

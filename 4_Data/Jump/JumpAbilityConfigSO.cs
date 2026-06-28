using UnityEngine;

/// <summary>
/// 175.2 W0 — Jump 基线配置 SO（策划编辑；每条 Variant 引用 1 份 BaseConfig）。
/// 运行时由 JumpModifierStack 把本 SO 复制成可改写副本 <see cref="JumpAbilityConfig"/>。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Jump/Jump Ability Config", fileName = "JumpAbilityConfig_")]
public sealed class JumpAbilityConfigSO : ScriptableObject
{
    [Header("基础冲量")]
    [Tooltip("起跳初速 m/s。LocomotionTuningSO.JumpForce 默认值 12。")]
    [Min(0f)] public float JumpForce = 12f;

    [Tooltip("可跳次数上限（1=单跳；蓝羽 Modifier 可叠加）。")]
    [Min(1f)] public float MaxJumpCount = 1f;

    [Header("输入容错")]
    [Tooltip("Coyote Time：离地后多少秒内仍允许起跳。")]
    [Min(0f)] public float CoyoteTimeSec = 0.10f;

    [Tooltip("Jump Buffer：落地前多少秒内按 Space 也算成功起跳。")]
    [Min(0f)] public float JumpBufferSec = 0.15f;

    [Header("重力分段")]
    [Tooltip("Apex 阶段重力倍率（|Vy|≤Threshold 时使用）。")]
    [Min(0f)] public float ApexGravityScale = 0.45f;

    [Tooltip("下落重力倍率。")]
    [Min(0f)] public float FallGravityScale = 1.6f;

    [Tooltip("Variable Height：松开 Space 后剩余持续秒；超出后强切大重力。")]
    [Min(0f)] public float VariableHeightMaxHoldSec = 0.20f;

    [Tooltip("Apex 悬停额外秒数（Modifier.HoverBoots 增加）。")]
    [Min(0f)] public float ExtraHangTimeSec = 0f;

    [Header("空中机动")]
    [Tooltip("空中玩家位移影响倍率 0~1。")]
    [Range(0f, 1f)] public float AirMoveMultiplier = 0.6f;

    [Tooltip("Variant 默认水平冲量加成（LongJump 设 8）。")]
    public float HorizontalBoost = 0f;

    [Header("GroundPound")]
    [Tooltip("下砸冲击波半径（米）。")]
    [Min(0f)] public float GroundPoundShockwaveRadius = 3f;

    /// <summary>175.2 — 复制为运行时副本。Recompute 链不污染本 SO。</summary>
    public JumpAbilityConfig ToRuntime() => new()
    {
        JumpForce = JumpForce,
        MaxJumpCount = MaxJumpCount,
        CoyoteTimeSec = CoyoteTimeSec,
        JumpBufferSec = JumpBufferSec,
        ApexGravityScale = ApexGravityScale,
        FallGravityScale = FallGravityScale,
        AirMoveMultiplier = AirMoveMultiplier,
        HorizontalBoost = HorizontalBoost,
        ExtraHangTimeSec = ExtraHangTimeSec,
        VariableHeightMaxHoldSec = VariableHeightMaxHoldSec,
        GroundPoundShockwaveRadius = GroundPoundShockwaveRadius,
    };
}

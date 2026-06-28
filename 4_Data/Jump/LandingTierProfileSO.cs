using UnityEngine;

/// <summary>
/// 175.2 W5 — 落地表现分层配置（5 阶 + Pound 特化）。
/// <para>策划编辑：阈值 + Action + VFX + Camera 全数据驱动。</para>
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Jump/Landing Tier Profile", fileName = "LandingTierProfile_")]
public sealed class LandingTierProfileSO : ScriptableObject
{
    [System.Serializable]
    public struct TierEntry
    {
        [Tooltip("此 Tier 的着地 Action（决定持续秒数、可控锁定窗）。")]
        public ActionDataSO LandAction;

        [Tooltip("着地 VFX 资产（烟尘 / 裂纹）。")]
        public ScriptableObject Vfx;

        [Tooltip("镜头震动幅度（度）。")]
        public float CameraShakeDeg;

        [Tooltip("HitStop 时长（秒）。0 = 不触发。")]
        public float HitStopSec;

        [Tooltip("玩家锁控时长（秒）；Action 自身打断窗也会接管。")]
        public float ControlLockSec;

        [Tooltip("着地额外授予的能力 Tag（如 HeavyLanding）。")]
        public GameplayTagMask GrantedTags;
    }

    [Header("高度阈值（米；下界采用）")]
    [Min(0f)] public float SoftThreshold = 1.5f;
    [Min(0f)] public float NormalThreshold = 3f;
    [Min(0f)] public float HeavyThreshold = 5f;
    [Min(0f)] public float RollThreshold = 8f;

    [Header("Roll 摔伤")]
    [Tooltip("Roll Tier 的 HP 损失百分比 (0~1)。蓝羽 Modifier 可免疫。")]
    [Range(0f, 1f)] public float RollHpLossRatio = 0.05f;

    [Header("Tier 表现（按 LandingTier 枚举顺序 6 项）")]
    public TierEntry Light;
    public TierEntry Soft;
    public TierEntry Normal;
    public TierEntry Heavy;
    public TierEntry Roll;
    public TierEntry Pound;

    public ref TierEntry Get(LandingTier tier)
    {
        switch (tier)
        {
            case LandingTier.Light:  return ref Light;
            case LandingTier.Soft:   return ref Soft;
            case LandingTier.Normal: return ref Normal;
            case LandingTier.Heavy:  return ref Heavy;
            case LandingTier.Roll:   return ref Roll;
            case LandingTier.Pound:  return ref Pound;
            default:                 return ref Light;
        }
    }
}

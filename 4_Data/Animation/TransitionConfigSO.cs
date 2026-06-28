using UnityEngine;

/// <summary>
/// 178.1 — 全局 fallback 配置（兜底 BlendDuration / Mode）。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Animation/Transition Config", fileName = "TransitionConfig_")]
public sealed class TransitionConfigSO : ScriptableObject
{
    [Header("Default Fallback（四层降级最末层）")]
    [Range(0f, 0.5f)] public float DefaultBlendDuration = 0.10f;
    public TransitionMode DefaultMode = TransitionMode.CrossFade;

    [Header("PhaseMatch")]
    [Tooltip("PhaseMatch 找不到节奏时是否降级到 CrossFade（推荐勾选）。")]
    public bool GracefulPhaseMatchFallback = true;

    [Header("烘焙阈值（W5 离线 PoseCost）")]
    [Tooltip("仅烘焙 PoseCost > 此阈值的 Clip 对，避免稀疏 DB 爆炸。")]
    [Range(0f, 1f)] public float PoseCostBakeThreshold = 0.30f;

    [Header("推荐 Blend 公式")]
    [Tooltip("PoseCost=0 时的推荐 Blend（已对齐，瞬切几乎无视觉差）。")]
    [Range(0f, 0.1f)] public float MinRecommendedBlend = 0.03f;

    [Tooltip("PoseCost=1 时的推荐 Blend（完全错位）。")]
    [Range(0f, 0.5f)] public float MaxRecommendedBlend = 0.20f;
}

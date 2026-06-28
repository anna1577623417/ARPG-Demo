using UnityEngine;

/// <summary>
/// 178.1 — ActionDataSO 的 Transition 扩展 partial。
/// <para>新增字段：Tag（必填，默认 Locomotion 安全值）+ OverrideIncomingTransition 一组。</para>
/// <para>既有 crossFadeDuration 字段保留作为四层降级的最末 fallback；W7 Library Audit 推荐迁移到 Tag/Override。</para>
/// </summary>
public partial class ActionDataSO
{
    [Header("Transition (178.1)")]
    [Tooltip("过渡分类 Tag —— 决定走 TagMatrix 行/列；默认 Locomotion 是安全值。")]
    public TransitionTag TransitionTag = TransitionTag.Locomotion;

    [Header("Transition Override（仅特殊 Action 需要覆盖矩阵时填）")]
    [Tooltip("进入此 Action 时强制使用以下 Override 值（忽略 Tag 矩阵）。")]
    public bool OverrideIncomingTransition;

    [Range(0f, 0.5f)] public float OverrideBlendDuration = 0.10f;
    public TransitionMode OverrideTransitionMode = TransitionMode.CrossFade;
    public AnimationCurve OverrideEaseCurve;

    [Tooltip("Override 备注：为什么需要覆盖矩阵？（如 ExecutionStart=凝重感 0.25s）")]
    public string OverrideDesignerNote;
}

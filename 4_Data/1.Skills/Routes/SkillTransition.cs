using System;
using UnityEngine;

/// <summary>
/// Stage → Stage / Stage → Route 的衔接定义（入口语义层）。
///
/// ═══ 语义 ═══
///   · 一个 Stage 可定义多条 Transition，运行时按数组顺序求值，**第一条** Trigger+Conditions 全过的中标。
///   · 同时配置 NextStage 与 NextRoute：优先 NextStage（同 Route 内推进）；若 NextStage 为空则进入 NextRoute（跨 Route 派生）。
///   · OpenTime/CloseTime 以本 Stage 的归一化时间 [0,1] 为基准；Auto 触发仅在 [OpenTime, CloseTime] 区间内开。
/// </summary>
[Serializable]
public struct SkillTransition
{
    [SerializeField, Tooltip("触发类型（Auto / OnInput / OnHit / OnTimer / OnRelease）。")]
    private StageTransitionTrigger trigger;

    [SerializeField, Range(0f, 1f), Tooltip("时间窗起点（归一化 0~1）。Auto 触发等价于到达此点即触发。")]
    private float openTime;

    [SerializeField, Range(0f, 1f), Tooltip("时间窗终点（归一化 0~1）。超过此点 Transition 失效。")]
    private float closeTime;

    [SerializeField, Tooltip("衔接到同 Route 内的下一个 Stage（与 nextRoute 二选一，优先此项）。")]
    private SkillStageDefinition nextStage;

    [SerializeField, Tooltip("衔接到另一条 Route（跨 Route 派生）。")]
    private SkillRouteDefinition nextRoute;

    [SerializeField, Tooltip("AND 组合的条件数组；ConditionEvaluator 任一不过即整条 Transition 不开。")]
    private SkillTransitionCondition[] conditions;

    [SerializeField, Tooltip("此 Transition 自身的标签；可用于 Stage 间打断闸门复用。")]
    private ulong selfTags;

    public StageTransitionTrigger Trigger => trigger;
    public float OpenTime => openTime;
    public float CloseTime => closeTime;
    public SkillStageDefinition NextStage => nextStage;
    public SkillRouteDefinition NextRoute => nextRoute;
    public SkillTransitionCondition[] Conditions => conditions;
    public ulong SelfTags => selfTags;

    public bool IsTimeInWindow(float normalizedTime)
    {
        var t = Mathf.Clamp01(normalizedTime);
        return t >= openTime && t <= closeTime;
    }
}

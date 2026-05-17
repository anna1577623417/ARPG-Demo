using System;
using UnityEngine;

/// <summary>
/// Stage 衔接条件（叶子）。
///
/// ═══ 设计要点 ═══
///   · 单一职责：一条 Condition 只判断「一件事」（一次命中 / 一种目标 / 一组标签）。
///   · 多条 Condition 由 SkillTransition 以 AND 组合 — 任一不满足即整条 Transition 不开。
///   · 无副作用：判断不修改任何运行时状态；副作用在 RouteRuntime 推进 Stage 时统一执行。
/// </summary>
[Serializable]
public struct SkillTransitionCondition
{
    [SerializeField, Tooltip("条件类型（Hit / TargetRule / Tag / Input / Time）。")]
    private SkillTransitionConditionKind kind;

    [SerializeField, Tooltip("OnHit：需要至少几次命中（默认 1）。")]
    private int requiredHitCount;

    [SerializeField, Tooltip("OnHit：命中目标过滤（与 requiredHitCount 联合）。")]
    private TransitionTargetRule targetRule;

    [SerializeField, Tooltip("OnTag：需要 Player 当前 State 轨包含此 ulong 位（全集 AND）。")]
    private ulong requiredStateTags;

    [SerializeField, Tooltip("OnTag：禁止 Player 当前 State 轨包含此 ulong 位（任一命中即拒）。")]
    private ulong forbiddenStateTags;

    [SerializeField, Tooltip("OnTag：需要 Mechanic 轨包含此 ulong 位（拉克丝 E 锚点落地等）。")]
    private ulong requiredMechanicTags;

    [SerializeField, Tooltip("OnTag：禁止 Mechanic 轨包含此 ulong 位。")]
    private ulong forbiddenMechanicTags;

    [SerializeField, Tooltip("OnInput：玩家是否需要再次输入触发槽位（与 expectedSlot 联合）。")]
    private bool requireInputAgain;

    [SerializeField, Tooltip("OnInput：期望的入口槽位。")]
    private SkillEntrySlot expectedSlot;

    [SerializeField, Min(0f), Tooltip("OnTime：相对 Stage 起点的最小经过秒数。")]
    private float minElapsedSeconds;

    public SkillTransitionConditionKind Kind => kind;
    public int RequiredHitCount => Mathf.Max(1, requiredHitCount);
    public TransitionTargetRule TargetRule => targetRule;
    public ulong RequiredStateTags => requiredStateTags;
    public ulong ForbiddenStateTags => forbiddenStateTags;
    public ulong RequiredMechanicTags => requiredMechanicTags;
    public ulong ForbiddenMechanicTags => forbiddenMechanicTags;
    public bool RequireInputAgain => requireInputAgain;
    public SkillEntrySlot ExpectedSlot => expectedSlot;
    public float MinElapsedSeconds => minElapsedSeconds;

    public static SkillTransitionCondition OnHit(int count = 1, TransitionTargetRule rule = TransitionTargetRule.Any) =>
        new SkillTransitionCondition { kind = SkillTransitionConditionKind.OnHit, requiredHitCount = count, targetRule = rule };

    public static SkillTransitionCondition OnInput(SkillEntrySlot slot) =>
        new SkillTransitionCondition { kind = SkillTransitionConditionKind.OnInput, requireInputAgain = true, expectedSlot = slot };

    public static SkillTransitionCondition OnTime(float seconds) =>
        new SkillTransitionCondition { kind = SkillTransitionConditionKind.OnTime, minElapsedSeconds = seconds };

    public static SkillTransitionCondition OnTag(ulong required, ulong forbidden) =>
        new SkillTransitionCondition { kind = SkillTransitionConditionKind.OnTag, requiredStateTags = required, forbiddenStateTags = forbidden };

    public static SkillTransitionCondition OnMechanicTag(ulong required, ulong forbidden = 0) =>
        new SkillTransitionCondition
        {
            kind = SkillTransitionConditionKind.OnTag,
            requiredMechanicTags = required,
            forbiddenMechanicTags = forbidden,
        };
}

/// <summary>
/// Condition 种类标识 — 用于 ConditionEvaluator 分支调度。
/// </summary>
public enum SkillTransitionConditionKind : byte
{
    Always   = 0,   // 永真（仅用于占位调试）
    OnHit    = 1,
    OnInput  = 2,
    OnTime   = 3,
    OnTag    = 4,
}

using System;
using UnityEngine;

/// <summary>
/// 蓄力路由 — 支持单 Clip / 多 Clip 两种编排模式。
///
/// ═══ 单 Clip（SingleClip）═══
///   仅 Stages[0] 作为整条蓄力的动作时间线（前摇 → HoldAnchor 凝滞 → 松手收尾均在同一 Clip 上，
///   由 SingleClipFreezeAtNormalized + ChargeRouteRuntime 冻结/解冻控制）。
///   若 Stages 多于 1 段：Stages[1] 及之后只作后摇/收招等衔接段，须在本段 Stage 资产配置
///   Transition → Auto（openTime≈1）指向下一段，勿让后续 Stage 单独承担蓄力时间轴。
///   适合：动画美术资源紧张、一条 Clip 内完成蓄力表现的简单蓄力。
///
/// ═══ 多 Clip（MultiClip）═══
///   Startup / HoldLoop / Release / Cancel 四个独立 Stage，由 ChargeRouteRuntime 推进。
///   · Startup    — 起手动画（HoldSeconds < tapThreshold 时松手则取消该 Stage）。
///   · HoldLoop   — 循环动画（运行时 MotionPlaybackContext.LoopWindow.Active = true）。
///   · Release    — 释放动画（玩家松手 / HasReachedFull 后切到此 Stage）。
///   · Cancel     — 取消动画（maxHoldTime 超时且 expiryPolicy = Cancel）。
///   注：MultiClip 模式下 base.Stages 字段不使用（由本类四个 Stage 字段独占）。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/SkillRoute/Route/Charge Route", fileName = "Route_Charge_")]
public sealed class ChargeRouteDefinition : SkillRouteDefinition
{
    [Header("Charge Mode")]
    [SerializeField, Tooltip(
        "单 Clip / 多 Clip 编排模式。\n\n" +
        "【SingleClip】仅 Stages[0] 作为动作时间线；凝滞/解冻见下方 Single-Clip Animation Lock-in。\n" +
        "若 Stages 配了多段：Stages[1+] 应在对应 Stage 资产里设 Transition → Auto 衔接（常用于收招），" +
        "勿用后续 Stage 替代 Stages[0] 的蓄力时间轴。\n\n" +
        "【MultiClip】不用 Stages[]，改配下方 Startup / HoldLoop / Release / Cancel 四段。")]
    private ChargePresentationMode presentationMode = ChargePresentationMode.SingleClip;

    [Header("Time anchors (seconds)")]
    [SerializeField, Min(0f), Tooltip("Tap 阈值：HoldSeconds < 本值视作轻点 → 回退到 tapFallbackRoute。")]
    private float tapThreshold = 0.18f;

    [SerializeField, Min(0f), Tooltip("满蓄时长：到达此值进入 HoldLoop / 压速；并触发完蓄伤害倍率。")]
    private float chargeFullTime = 1.0f;

    [SerializeField, Min(0f), Tooltip("极限保持时长（>0 时启用）；到达后按 expiryPolicy 处理。")]
    private float maxHoldTime = 3.0f;

    [SerializeField, Tooltip("达到 maxHoldTime 后的处理策略。")]
    private ChargeExpiryPolicy expiryPolicy = ChargeExpiryPolicy.ForceRelease;

    [Header("Single-Clip Animation Lock-in")]
    [SerializeField, Range(0f, 1f),
     Tooltip("仅 SingleClip 模式生效：进入凝滞点后 Animator 速率倍率（0=完全暂停, 0.05≈抖循环）。")]
    private float singleClipHoldAnimSpeedAtFull = 0.05f;

    [SerializeField, Range(0f, 1f),
     Tooltip("仅 SingleClip 模式生效：Stage 归一化时间到此值且玩家仍按住时，进入 凝滞点 ——\n" +
             "时钟冻结（Stage.Elapsed 停止推进，Action 不再前进）+ 动画压速；\n" +
             "玩家松手即解冻继续播放剩余部分（释放挥砍段）。\n" +
             "推荐 0.5~0.7（前摇 → 凝滞 → 收尾打击）。设为 0 或 1 视作 无凝滞点，保持旧线性行为。")]
    private float singleClipFreezeAtNormalized = 0.6f;

    [Header("Multi-Clip Stages")]
    [SerializeField, Tooltip("MultiClip 模式：起手 Stage（Press → 进入此 Stage）。")]
    private SkillStageDefinition startupStage;

    [SerializeField, Tooltip("MultiClip 模式：循环 Stage（满蓄后切入；LoopWindow.Active=true）。")]
    private SkillStageDefinition holdLoopStage;

    [SerializeField, Range(0f, 1f), Tooltip("MultiClip 模式：HoldLoop 的循环区间起点（归一化）。")]
    private float holdLoopRangeStart = 0.2f;

    [SerializeField, Range(0f, 1f), Tooltip("MultiClip 模式：HoldLoop 的循环区间终点（归一化）。")]
    private float holdLoopRangeEnd = 0.8f;

    [SerializeField, Tooltip("MultiClip 模式：释放 Stage（松手 / 满蓄强制释放 时切入）。")]
    private SkillStageDefinition releaseStage;

    [SerializeField, Tooltip("MultiClip 模式：取消 Stage（maxHoldTime 超时且 expiryPolicy=Cancel 时切入）。")]
    private SkillStageDefinition cancelStage;

    [Header("Damage Curve (continuous)")]
    [SerializeField, Tooltip("连续伤害倍率曲线 — X = ChargeProgress01 (HoldSeconds/chargeFullTime), Y = 倍率。空 = 恒 1。")]
    private AnimationCurve damageMultiplierByProgress;

    [Header("Cancel Refund")]
    [SerializeField, Tooltip("蓄力被取消（超时 / 外部打断 / 死亡）时 CD 返还模式。")]
    private ChargeCancelRefundMode cancelRefundMode = ChargeCancelRefundMode.ReturnPercent;

    [SerializeField, Min(0f),
     Tooltip("Refund 数值：Percent 模式取 [0,1]；Fixed 模式取秒数。")]
    private float cancelRefundValue = 1f;

    [SerializeField, Tooltip("取消时是否退还本次已扣的资源（HP/Stamina/MP/Energy）。")]
    private bool refundResourceOnCancel = true;

    [Header("Tap Fallback")]
    [SerializeField, Tooltip("启用 Tap 回退：HoldSeconds < tapThreshold 时松手回退到本 Route。")]
    private bool enableTapFallback = true;

    [SerializeField, Tooltip("Tap 回退到哪条 Route（通常是同 Entry 的 NormalRoute）。")]
    private SkillRouteDefinition tapFallbackRoute;

    // ── 公有只读暴露 ──
    public ChargePresentationMode PresentationMode => presentationMode;
    public float TapThreshold => tapThreshold;
    public float ChargeFullTime => chargeFullTime;
    public float MaxHoldTime => maxHoldTime;
    public ChargeExpiryPolicy ExpiryPolicy => expiryPolicy;
    public float SingleClipHoldAnimSpeedAtFull => singleClipHoldAnimSpeedAtFull;
    public float SingleClipFreezeAtNormalized => singleClipFreezeAtNormalized;
    public SkillStageDefinition StartupStage => startupStage;
    public SkillStageDefinition HoldLoopStage => holdLoopStage;
    public float HoldLoopRangeStart => holdLoopRangeStart;
    public float HoldLoopRangeEnd => holdLoopRangeEnd;
    public SkillStageDefinition ReleaseStage => releaseStage;
    public SkillStageDefinition CancelStage => cancelStage;
    public AnimationCurve DamageMultiplierByProgress => damageMultiplierByProgress;
    public ChargeCancelRefundMode CancelRefundMode => cancelRefundMode;
    public float CancelRefundValue => cancelRefundValue;
    public bool RefundResourceOnCancel => refundResourceOnCancel;
    public bool EnableTapFallback => enableTapFallback;
    public SkillRouteDefinition TapFallbackRoute => tapFallbackRoute;

    public override RouteKind Kind => RouteKind.Charge;

    /// <summary>采样进度 → 倍率（曲线缺失返回 1）。</summary>
    public float SampleDamageMultiplier(float progress01)
    {
        if (damageMultiplierByProgress == null || damageMultiplierByProgress.length == 0)
        {
            return 1f;
        }

        return Mathf.Max(0f, damageMultiplierByProgress.Evaluate(Mathf.Clamp01(progress01)));
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // 1) maxHoldTime ≥ chargeFullTime
        if (maxHoldTime > 0f && maxHoldTime < chargeFullTime)
        {
            maxHoldTime = chargeFullTime;
        }

        // 2) HoldLoop 区间合法
        if (holdLoopRangeEnd < holdLoopRangeStart)
        {
            holdLoopRangeEnd = holdLoopRangeStart;
        }

        // 3) SingleClip 凝滞点合理范围（防止配 0 或 1 → 全程冻结 / 永不冻结）
        if (presentationMode == ChargePresentationMode.SingleClip
            && (singleClipFreezeAtNormalized <= 0.01f || singleClipFreezeAtNormalized >= 0.99f))
        {
            Debug.LogWarning(
                $"[ChargeRoute] '{name}' SingleClipFreezeAtNormalized={singleClipFreezeAtNormalized:F2} 处于边界," +
                " 建议 0.15–0.30（前摇与凝滞分界）；0/1 视作 无凝滞点 =纯线性播放。",
                this);
        }

        // 4) TapThreshold 必须 > 0（否则任何按压都判 Tap，永不进 Charge）
        if (tapThreshold <= 0.0001f)
        {
            Debug.LogWarning(
                $"[ChargeRoute] '{name}' TapThreshold=0 → 永远走不到 Charge 分支！" +
                " 必须 >0 才能让 InputSemanticResolver 在 hold ≥ 此值时派发 Charge intent。",
                this);
        }

        // 5) FullChargeTime ≤ MaxHoldTime（语义合理性）
        if (chargeFullTime > maxHoldTime && maxHoldTime > 0f)
        {
            Debug.LogWarning(
                $"[ChargeRoute] '{name}' ChargeFullTime ({chargeFullTime:F2}s) > MaxHoldTime ({maxHoldTime:F2}s)" +
                " — Damage 倍率永远达不到 1.0；建议 FullChargeTime ≤ MaxHoldTime。",
                this);
        }

        // 6) MultiClip 模式必须配齐 Startup + HoldLoop + Release（Cancel 可选）
        if (presentationMode == ChargePresentationMode.MultiClip)
        {
            if (startupStage == null || holdLoopStage == null || releaseStage == null)
            {
                Debug.LogWarning(
                    $"[ChargeRoute] '{name}' MultiClip 模式需要 Startup + HoldLoop + Release 三个 Stage 全部配置。",
                    this);
            }
        }

        // 7) SingleClip 模式必须有 Stages[0]
        if (presentationMode == ChargePresentationMode.SingleClip)
        {
            var first = FirstStage();
            if (first == null || first.Action == null)
            {
                Debug.LogWarning(
                    $"[ChargeRoute] '{name}' SingleClip 模式必须配置 Stages[0] 且 Stages[0].Action 非空（含 MotionProfile）。",
                    this);
            }

        }
    }
#endif
}

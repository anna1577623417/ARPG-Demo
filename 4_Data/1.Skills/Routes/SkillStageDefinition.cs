using UnityEngine;

/// <summary>
/// 技能阶段定义（入口语义层最小单元）。
///
/// ═══ 与动作语义层的唯一握手 ═══
///   · <see cref="Action"/> 字段引用一份独立的 ActionDataSO；该 ActionData 内部对本 Stage 一无所知。
///   · 这是整套架构的"两条管线交汇点"——SkillRoute/Stage 拥有按键意图、CD、Cost、Transition；
///     ActionData 只拥有 Clip / Window / MotionProfile。
///
/// ═══ 「后摇阶段」的等价表达 ═══
///   把后摇做成本 Stage 的 transitions[0] = { trigger=Auto, openTime=1, closeTime=1, nextStage=RecoveryStage }，
///   RecoveryStage 自身配 Action（仅播收招动画），即可在 RouteRuntime 层自动衔接，无需 ActionData.Duration 留长尾。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/SkillRoute/Stage/Stage Definition", fileName = "Stage_")]
public class SkillStageDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField, Tooltip("调试用稳定标识；不参与运行时分发。")]
    private string stageId;

    [Header("Action (动作语义握手点)")]
    [SerializeField, Tooltip("本 Stage 播放的 ActionData — Clip / Windows / MotionProfile 来源。")]
    private ActionDataSO action;

    [Header("HUD (optional)")]
    [SerializeField, Tooltip("本段在技能栏显示的图标；空则继承 Route.Icon。")]
    private Sprite hudIcon;

    [SerializeField, Tooltip("211.3 — 本段 HUD 显示名；空则继承 Route/Group。")]
    string hudDisplayName;

    [SerializeField, Tooltip("211.3 — 本段 HUD 角标文本；空则由 Badge 段号填充。")]
    string hudBadgeText;

    [Header("Stage-Local Cost (独立于 Route Cost)")]
    [SerializeField, Tooltip("进入本 Stage 时额外扣的资源（与 Route.costs 叠加，可为空）。")]
    private SkillCostEntry[] additionalCosts;

    [Header("Stage-Local Cooldown")]
    [SerializeField, Min(0f), Tooltip("本 Stage 独立 CD 写入（与 Route CD 互不冲突；用于多段技能的中间段独立 CD 控制）。")]
    private float stageLocalCooldownSeconds;

    [Header("Transitions")]
    [SerializeField, Tooltip("Stage → Stage / Stage → Route 的衔接表；按数组顺序，第一条通过的为准。")]
    private SkillTransition[] transitions;

    [Header("Hit Detection (复用 ActionWindow 命中事件即可)")]
    [SerializeField, Tooltip("可选：本 Stage 命中目标的过滤器（影响 OnHit Condition 计数与 DamagePipeline 过滤）。")]
    private TargetFilterSO targetFilter;

    [SerializeField, Tooltip("可选：本 Stage 命中检测形状（HitFrame 触发时使用）。")]
    private HitShapeSO hitShape;

    [Header("Damage (per-stage override; null 时继承 Route.damage)")]
    [SerializeField, Tooltip("是否覆盖 Route.damage；勾选后本 Stage 用 stageDamage。")]
    private bool overrideRouteDamage;

    [SerializeField, Tooltip("Stage 级伤害表（仅在 overrideRouteDamage=true 时生效）。")]
    private DamageSheet stageDamage;

    // ── 公有只读暴露 ──
    public string StageId => stageId;
    public ActionDataSO Action => action;
    public Sprite HudIcon => hudIcon;
    public string HudDisplayName => hudDisplayName ?? string.Empty;
    public string HudBadgeText => hudBadgeText ?? string.Empty;
    public SkillCostEntry[] AdditionalCosts => additionalCosts;
    public float StageLocalCooldownSeconds => stageLocalCooldownSeconds;
    public SkillTransition[] Transitions => transitions;
    public TargetFilterSO TargetFilter => targetFilter;
    public HitShapeSO HitShape => hitShape;
    public bool OverrideRouteDamage => overrideRouteDamage;
    public DamageSheet StageDamage => stageDamage;

    /// <summary>取本 Stage 时长（秒）：直接取 Action 的逻辑时长。</summary>
    public float ResolveDurationSeconds()
    {
        return action != null ? action.ResolveLogicalDurationSeconds() : 0f;
    }
}

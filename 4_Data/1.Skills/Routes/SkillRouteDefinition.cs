using UnityEngine;

/// <summary>
/// 技能路由定义（抽象基类） — 入口语义层的「最小技能单位」。
///
/// ═══ 设计核心 ═══
///   · 一个 SkillEntry 可挂多条 Route（Normal / Combo / Charge / MultiStage / Derivative），
///     由 RouteResolver 按 RoutePriority + InputContext 选出本次释放谁。
///   · Route 直接持有 Icon / Cooldown / Cost / Damage —— 不再通过 SkillData 中转。
///   · Route 不直接持有 ActionData —— Action 全部下沉到 Stages[]。
/// </summary>
public abstract class SkillRouteDefinition : ScriptableObject, ISkillUnit
{
    [Header("Identity")]
    [SerializeField, Tooltip("调试稳定 ID（不参与运行时分发，仅用于 Recipe 反查 / 日志）。")]
    private string routeId;

    [SerializeField, Tooltip("HUD 图标 — 由 Presenter 经 IRouteRuntimeHandle.Icon 读取。")]
    private Sprite icon;

    [SerializeField, Tooltip("HUD 显示名（本地化前的原文）。")]
    private string displayName;

    [SerializeField, Tooltip("是否在 HUD 显示该 Route。默认 true；Derivative 派生招若默认隐藏可置 false。")]
    private bool showOnHud = true;

    [Header("Routing")]
    [SerializeField, Tooltip("Resolver 选择优先级。一个 Entry 内多条 Route 时按本字段从小到大求值。")]
    private RoutePriority priority = RoutePriority.Normal;

    [Header("Cooldown")]
    [SerializeField, Tooltip("CD 启动策略 — 单 Route 用 OnRouteEnd / OnFirstStageEnd；Combo 容器推荐 On Last SubRoute End。")]
    protected RouteCooldownPolicy cooldownPolicy = RouteCooldownPolicy.OnRouteEnd;

    [SerializeField, Min(0f), Tooltip("基础冷却时长（秒）；最终 CD 受 IStatSet.CooldownReduction 调制。")]
    private float baseCooldownSeconds;

    [SerializeField, Tooltip("冷却分组键（非空时本 Route 与同 Group 内所有 Route 共享 CD）。")]
    private string cooldownGroup;

    [SerializeField, Tooltip("是否绕过全局公共 CD（GCD）。位移类常用。")]
    private bool ignoreGlobalCooldown;

    [Header("Cost")]
    [SerializeField, Tooltip("Route 级资源消耗表（与 Stage.additionalCosts 叠加）。")]
    private SkillCostEntry[] costs;

    [SerializeField, Tooltip(
        "资源消耗时机。\n" +
        "· OnRouteStart / On First SubRoute Start — 进入时扣费。\n" +
        "· OnFirstStageStart — 首段 Stage 开始时。\n" +
        "· OnLastStageEnd / On Last SubRoute End — 末段完成时。\n" +
        "· OnRouteEnd — Route 退出时。\n" +
        "Combo 容器：On First SubRoute Start + On Last SubRoute End；SubRoute 勿重复扣费。")]
    protected RouteResourceConsumePolicy resourceConsumePolicy = RouteResourceConsumePolicy.OnRouteStart;

    [Header("Damage")]
    [SerializeField, Tooltip("Route 级伤害表 — Stage 若 overrideRouteDamage=false 则继承本字段。")]
    private DamageSheet damage = DamageSheet.None;

    [Header("Stages")]
    [SerializeField, Tooltip("Stage 数组；至少一条。多 Stage 由 Transition 推进。")]
    private SkillStageDefinition[] stages;

    [Header("Tags (entrance + while-active)")]
    [SerializeField, Tooltip("进入 Route 时给 Player 标签写入的 State 位（5 轨之 State 轨）。")]
    private ulong grantedStateTags;

    [SerializeField, Tooltip("进入 Route 时给 Player 标签写入的 Ability 位。")]
    private ulong grantedAbilityTags;

    [Header("Group Membership (Ver4.3.7+)")]
    [SerializeField, Tooltip("所属技能组；空 = 独立技能单位。")]
    SkillGroupDefinition ownerGroup;

    [SerializeField, Tooltip("为 true 时使用本 Route 的 CD，不读 Group。")]
    bool overrideCooldown;

    [SerializeField, Tooltip("为 true 时使用本 Route 的 Icon。")]
    bool overrideIcon;

    [SerializeField, Tooltip("为 true 时使用本 Route 的 Cost。")]
    bool overrideCost;

    // ── 公有只读暴露 ──
    public string RouteId => routeId;
    public Sprite Icon => icon;
    public string DisplayName => displayName;
    public bool ShowOnHud => showOnHud;
    public RoutePriority Priority => priority;
    public RouteCooldownPolicy CooldownPolicy => cooldownPolicy;
    public float BaseCooldownSeconds => baseCooldownSeconds;
    public string CooldownGroup => cooldownGroup;
    public bool IgnoreGlobalCooldown => ignoreGlobalCooldown;
    public SkillCostEntry[] Costs => costs;
    public RouteResourceConsumePolicy ResourceConsumePolicy => resourceConsumePolicy;
    public DamageSheet Damage => damage;
    public SkillStageDefinition[] Stages => stages;
    public ulong GrantedStateTags => grantedStateTags;
    public ulong GrantedAbilityTags => grantedAbilityTags;
    public SkillGroupDefinition OwnerGroup => ownerGroup;
    public bool OverrideGroupCooldown => overrideCooldown;
    public bool OverrideGroupIcon => overrideIcon;
    public bool OverrideGroupCost => overrideCost;

    string ISkillUnit.DisplayName => GetEffectiveDisplayName();
    Sprite ISkillUnit.Icon => GetEffectiveIcon();
    float ISkillUnit.CooldownSeconds => GetEffectiveCooldownSeconds(null);
    SkillCostEntry[] ISkillUnit.Costs => GetEffectiveCosts();
    ulong ISkillUnit.RequiredAbilityTags => GetEffectiveRequiredAbilityTags();

    public string GetEffectiveDisplayName()
    {
        if (!string.IsNullOrEmpty(displayName))
        {
            return displayName;
        }

        if (ownerGroup != null)
        {
            return ownerGroup.DisplayName;
        }

        return name;
    }

    public Sprite GetEffectiveIcon()
    {
        if (overrideIcon || icon != null)
        {
            return icon;
        }

        return ownerGroup != null ? ownerGroup.Icon : icon;
    }

    public float GetEffectiveCooldownSeconds(IStatSet stats)
    {
        var raw = (ownerGroup != null && !overrideCooldown)
            ? ownerGroup.CooldownSeconds
            : baseCooldownSeconds;
        if (stats == null)
        {
            return raw;
        }

        var cdr = Mathf.Clamp(stats.Get(StatType.CooldownReduction), 0f, 0.4f);
        return Mathf.Max(0f, raw * (1f - cdr));
    }

    public SkillCostEntry[] GetEffectiveCosts()
    {
        if (overrideCost || costs != null && costs.Length > 0)
        {
            return costs;
        }

        return ownerGroup != null ? ownerGroup.Costs : costs;
    }

    public ulong GetEffectiveRequiredAbilityTags()
    {
        var routeTags = grantedAbilityTags;
        if (ownerGroup == null)
        {
            return routeTags;
        }

        return routeTags | ownerGroup.RequiredAbilityTags;
    }

    /// <summary>子类自报"我是哪种 Route"，用于 RouteResolver 类型分支（避免反射）。</summary>
    public abstract RouteKind Kind { get; }

    public SkillStageDefinition FirstStage()
    {
        return stages != null && stages.Length > 0 ? stages[0] : null;
    }

#if UNITY_EDITOR
    internal void EditorAssignOwnerGroup(SkillGroupDefinition group)
    {
        ownerGroup = group;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}

/// <summary>
/// Route 子类标识 — 用于 RouteResolver 类型分支。
/// </summary>
public enum RouteKind : byte
{
    Normal     = 0,
    Combo      = 1,
    Charge     = 2,
    MultiStage = 3,
    Derivative = 4,
    Directional= 5,   // DirectionalRouteSet（聚合多个子 Route 按方向选）
}

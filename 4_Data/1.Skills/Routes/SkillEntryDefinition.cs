using UnityEngine;

/// <summary>
/// 技能入口定义 — 一个槽位绑定一个 Entry，Entry 内部聚合多条 Route。
///
/// ═══ 数据模型 ═══
///   SkillEntryDefinition (槽位入口，1 个)
///     ├─ PrimaryUnit (SkillGroup / Route) — 四向在 Group 内配置
///     ├─ NormalRoute      (可空，非 Directional 回退)
///     ├─ ComboRoute       (可空)
///     ├─ ChargeRoute      (可空)
///     ├─ MultiStageRoute  (可空)
///     └─ Derivatives[]    (派生招，可空)
///
/// ═══ Resolver 选择优先级 ═══
///   按 RoutePriority 从小到大求值：Charge → Combo → PrimaryGroup(四向) → Derivative → MultiStage → Normal。
///   首条命中即出（短路）。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/SkillRoute/Entry/Skill Entry Definition", fileName = "Entry_")]
public class SkillEntryDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField, Tooltip("入口绑定的槽位 — 与 InputReader 物理键映射。")]
    private SkillEntrySlot slot;

    [SerializeField, Tooltip("HUD 显示名（本地化前的原文）。")]
    private string displayName;

    [SerializeField, Tooltip("HUD 备用图标（当无 Route 激活时显示）。")]
    private Sprite fallbackIcon;

    [Header("Routes (从优先级高到低)")]
    [SerializeField, Tooltip("蓄力路由（最高优先级；持续按住时启用）。")]
    private ChargeRouteDefinition chargeRoute;

    [SerializeField, Tooltip("连段路由（短按多次时启用）。")]
    private ComboRouteDefinition comboRoute;

    [SerializeField, Tooltip(
        "Combo2 独立连段容器（如 A2→B2→C2），可与 Combo1 共用动作但须用不同 NormalRoute SO。\n" +
        "Combo1 完整打完 B1 后、窗内且 Combo2 CD 好 → 接续整段 Combo2。\n" +
        "虚拟链：A1-B1-A2-B2-C2。CD 在 Combo2 末段结束结算。")]
    private ComboRouteDefinition extendedComboRoute;

    [SerializeField, Tooltip("空中连段（IsAirborne 时替代 comboRoute）。")]
    private ComboRouteDefinition airComboRoute;

    [SerializeField, Tooltip("多段路由（同 Route 内 Stage 推进型，如盲僧 Q1→Q2）。")]
    private MultiStageRouteDefinition multiStageRoute;

    [SerializeField, Tooltip("普通路由（默认 fallback）。一般每个 Entry 至少配此项。")]
    private NormalRouteDefinition normalRoute;

    [Header("Combo1 末段(B1) → Combo2 起手(A2) 双链衔接")]
    [SerializeField, Min(0f), Tooltip(
        "B1 自然结束后，允许多久内按 LM 接续 Combo2（秒）。\n" +
        "0 = 回落 Combo Route 的 Combo Session Reset Time。\n" +
        "与 Combo1 内 A1→B1 的 Session 窗可分开调。")]
    private float extendedHandoffWindowSeconds;

    [SerializeField, Min(0f), Tooltip("B1 结束后，最早何时可接 A2（秒）；0 = 不限制。")]
    private float extendedHandoffMinGap;

    [SerializeField, Min(0f), Tooltip(
        "B1 结束后，最晚何时必须按下才能接 A2（秒）；0 = 与【衔接总窗口】相同。\n" +
        "A2→B2、B2→C2 使用 Combo2 的 Combo Transitions。")]
    private float extendedHandoffMaxGap;

    [Header("Derivative Routes (派生招池)")]
    [SerializeField, Tooltip("派生招池 — 当父 Route 处于 Active 时由 RouteResolver 动态接入。")]
    private DerivativeRouteDefinition[] derivativeRoutes;

    [Header("Primary Skill Unit (Ver4.3.7+)")]
    [SerializeField, Tooltip("【主单位】SkillRouteDefinition 或 SkillGroupDefinition；配置后优先于仅 CombatGraph 的裸 Route 列表。")]
    private UnityEngine.Object primaryUnit;

    // ── 公有只读暴露 ──
    public SkillEntrySlot Slot => slot;
    public string DisplayName => displayName;
    public Sprite FallbackIcon => fallbackIcon;
    public ChargeRouteDefinition ChargeRoute => chargeRoute;
    public ComboRouteDefinition ComboRoute => comboRoute;
    public ComboRouteDefinition ExtendedComboRoute => extendedComboRoute;
    public ComboRouteDefinition AirComboRoute => airComboRoute;
    public MultiStageRouteDefinition MultiStageRoute => multiStageRoute;
    public NormalRouteDefinition NormalRoute => normalRoute;
    public float ExtendedHandoffWindowSeconds => extendedHandoffWindowSeconds;
    public float ExtendedHandoffMinGap => extendedHandoffMinGap;
    public float ExtendedHandoffMaxGap => extendedHandoffMaxGap;
    public DerivativeRouteDefinition[] DerivativeRoutes => derivativeRoutes;
    public ISkillUnit PrimaryUnit => primaryUnit as ISkillUnit;
    public SkillGroupDefinition PrimaryGroup => primaryUnit as SkillGroupDefinition;
    public SkillRouteDefinition PrimaryRoute => primaryUnit as SkillRouteDefinition;

    /// <summary>注册 Entry 内所有 Route（含 Group 成员）到 SkillEntryService。</summary>
    public void CollectAllRoutes(System.Collections.Generic.List<SkillRouteDefinition> buffer)
    {
        if (buffer == null)
        {
            return;
        }

        if (PrimaryGroup != null)
        {
            AddIfNotNull(buffer, PrimaryGroup.Forward);
            AddIfNotNull(buffer, PrimaryGroup.Backward);
            AddIfNotNull(buffer, PrimaryGroup.Left);
            AddIfNotNull(buffer, PrimaryGroup.Right);

            var routes = PrimaryGroup.Routes;
            if (routes != null)
            {
                for (var i = 0; i < routes.Count; i++)
                {
                    if (routes[i] != null)
                    {
                        buffer.Add(routes[i]);
                    }
                }
            }

            if (PrimaryGroup.FallbackRoute != null)
            {
                buffer.Add(PrimaryGroup.FallbackRoute);
            }
        }
        else if (PrimaryRoute != null)
        {
            buffer.Add(PrimaryRoute);
        }

        if (chargeRoute != null) buffer.Add(chargeRoute);
        if (comboRoute != null) buffer.Add(comboRoute);
        if (extendedComboRoute != null) buffer.Add(extendedComboRoute);
        if (airComboRoute != null) buffer.Add(airComboRoute);
        if (multiStageRoute != null) buffer.Add(multiStageRoute);
        if (normalRoute != null) buffer.Add(normalRoute);
        if (derivativeRoutes != null)
        {
            for (var i = 0; i < derivativeRoutes.Length; i++)
            {
                if (derivativeRoutes[i] != null)
                {
                    buffer.Add(derivativeRoutes[i]);
                }
            }
        }
    }

    static void AddIfNotNull(System.Collections.Generic.List<SkillRouteDefinition> buffer, SkillRouteDefinition route)
    {
        if (route == null)
        {
            return;
        }

        for (var i = 0; i < buffer.Count; i++)
        {
            if (buffer[i] == route)
            {
                return;
            }
        }

        buffer.Add(route);
    }

    /// <summary>B1 后等候 Combo2 的总窗口（秒）。0 = Combo1.ComboSessionResetTime。</summary>
    public float GetExtendedHandoffWindowSeconds(ComboRouteDefinition primary)
    {
        if (extendedHandoffWindowSeconds > 0.0001f)
        {
            return extendedHandoffWindowSeconds;
        }

        return primary != null ? primary.ComboSessionResetTime : 0f;
    }

    /// <summary>B1→A2 按键的最晚间隔；0 = 与总窗口相同。</summary>
    public float GetExtendedHandoffMaxGapEffective(ComboRouteDefinition primary)
    {
        if (extendedHandoffMaxGap > 0.0001f)
        {
            return extendedHandoffMaxGap;
        }

        return GetExtendedHandoffWindowSeconds(primary);
    }

    /// <summary>Combo1 末段 → Combo2 的衔接间隔是否合法（gap 自 Combo1 最后一段自然结束起算）。</summary>
    public bool IsExtendedHandoffGapValid(float gapSeconds, ComboRouteDefinition primary, out string rejectReason)
    {
        rejectReason = null;
        if (extendedComboRoute == null || primary == null)
        {
            return false;
        }

        if (extendedHandoffMinGap > 0.0001f && gapSeconds < extendedHandoffMinGap)
        {
            rejectReason =
                $"handoff gap {gapSeconds:F2}s < min {extendedHandoffMinGap:F2}s (B1→A2)";
            return false;
        }

        var maxEffective = GetExtendedHandoffMaxGapEffective(primary);
        if (maxEffective > 0.0001f && gapSeconds > maxEffective)
        {
            rejectReason =
                $"handoff gap {gapSeconds:F2}s > max {maxEffective:F2}s (B1→A2)";
            return false;
        }

        return true;
    }

    /// <summary>聚合所有 Route 给 HUD Presenter 用（不含派生招池）。</summary>
    /// <remarks>0-GC 调用方需提供 buffer，长度 ≥ 5。返回写入的数量。</remarks>
    public int CollectVisibleRoutes(SkillRouteDefinition[] buffer)
    {
        var n = 0;
        if (buffer == null || buffer.Length == 0)
        {
            return 0;
        }

        if (chargeRoute     != null && chargeRoute.ShowOnHud     && n < buffer.Length) buffer[n++] = chargeRoute;
        if (comboRoute      != null && comboRoute.ShowOnHud      && n < buffer.Length) buffer[n++] = comboRoute;
        if (extendedComboRoute != null && extendedComboRoute.ShowOnHud && n < buffer.Length) buffer[n++] = extendedComboRoute;
        if (airComboRoute   != null && airComboRoute.ShowOnHud   && n < buffer.Length) buffer[n++] = airComboRoute;
        if (multiStageRoute != null && multiStageRoute.ShowOnHud && n < buffer.Length) buffer[n++] = multiStageRoute;
        if (normalRoute     != null && normalRoute.ShowOnHud     && n < buffer.Length) buffer[n++] = normalRoute;

        return n;
    }
}

using UnityEngine;

/// <summary>
/// 技能入口定义 — 一个槽位绑定一个 Entry，Entry 内部聚合多条 Route。
///
/// ═══ 数据模型 ═══
///   SkillEntryDefinition (槽位入口，1 个)
///     ├─ NormalRoute      (可空)
///     ├─ ComboRoute       (可空)
///     ├─ ChargeRoute      (可空)
///     ├─ MultiStageRoute  (可空)
///     ├─ DirectionalSet   (可空)
///     └─ Derivatives[]    (派生招，可空)
///
/// ═══ Resolver 选择优先级 ═══
///   按 RoutePriority 从小到大求值：Charge → Combo → Directional → Derivative → MultiStage → Normal。
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

    [SerializeField, Tooltip("方向化路由集（WASD modifier + Trigger 时启用）。")]
    private DirectionalRouteSet directionalRoute;

    [SerializeField, Tooltip("多段路由（同 Route 内 Stage 推进型，如盲僧 Q1→Q2）。")]
    private MultiStageRouteDefinition multiStageRoute;

    [SerializeField, Tooltip("普通路由（默认 fallback）。一般每个 Entry 至少配此项。")]
    private NormalRouteDefinition normalRoute;

    [Header("Derivative Routes (派生招池)")]
    [SerializeField, Tooltip("派生招池 — 当父 Route 处于 Active 时由 RouteResolver 动态接入。")]
    private DerivativeRouteDefinition[] derivativeRoutes;

    // ── 公有只读暴露 ──
    public SkillEntrySlot Slot => slot;
    public string DisplayName => displayName;
    public Sprite FallbackIcon => fallbackIcon;
    public ChargeRouteDefinition ChargeRoute => chargeRoute;
    public ComboRouteDefinition ComboRoute => comboRoute;
    public DirectionalRouteSet DirectionalRoute => directionalRoute;
    public MultiStageRouteDefinition MultiStageRoute => multiStageRoute;
    public NormalRouteDefinition NormalRoute => normalRoute;
    public DerivativeRouteDefinition[] DerivativeRoutes => derivativeRoutes;

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
        if (directionalRoute!= null && directionalRoute.ShowOnHud&& n < buffer.Length) buffer[n++] = directionalRoute;
        if (multiStageRoute != null && multiStageRoute.ShowOnHud && n < buffer.Length) buffer[n++] = multiStageRoute;
        if (normalRoute     != null && normalRoute.ShowOnHud     && n < buffer.Length) buffer[n++] = normalRoute;

        return n;
    }
}

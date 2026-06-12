using UnityEngine;

/// <summary>
/// 派生路由 — 从父 Route 解锁出来的衍生招。
///
/// 范式：闪避后 0.3s 内按 LMB → 闪避反击；处决（处决目标 HP 阈值后 LMB 触发）。
/// 派生不污染 Entry 主路由列表，但会被 RouteResolver 在 ActiveRoute 期间动态接入。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/SkillRoute/Route/Derivative Route", fileName = "Route_Derivative_")]
public sealed class DerivativeRouteDefinition : SkillRouteDefinition
{
    [Header("Derivation")]
    [SerializeField, Tooltip("父 Route（运行时只在此 Route 处于 Active 时本派生可被触发）。")]
    private SkillRouteDefinition parentRoute;

    [SerializeField, Tooltip("解锁需要的全部条件（AND 组合）。")]
    private SkillTransitionCondition[] unlockConditions;

    [SerializeField, Tooltip("派生触发槽位（玩家需在解锁窗内按此槽）。")]
    private SkillEntrySlot triggerSlot = SkillEntrySlot.LM;

    [SerializeField, Min(0f), Tooltip("解锁窗时长（秒，自满足条件起算）。0 = 持续到父 Route 退出。")]
    private float unlockWindowSeconds = 0.4f;

    [SerializeField, Tooltip("CD / 资源继承策略（标志位）。")]
    private DerivativeFlags flags = DerivativeFlags.None;

    public SkillRouteDefinition ParentRoute => parentRoute;
    public SkillTransitionCondition[] UnlockConditions => unlockConditions;
    public SkillEntrySlot TriggerSlot => triggerSlot;
    public float UnlockWindowSeconds => unlockWindowSeconds;
    public DerivativeFlags Flags => flags;

    public override RouteKind Kind => RouteKind.Derivative;

    public override RouteGraphType GraphType => RouteGraphType.Derived;
}

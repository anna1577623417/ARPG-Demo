using UnityEngine;

/// <summary>
/// 普通路由 — 最简单的「按下即释放」单段或多段技能。
///
/// 适用：普攻 1 段、瞬发主动技、无 Combo / 蓄力 / 派生 的招式。
/// Stages 多于 1 段时由 Transition.Auto 自动衔接 — 可表达"出招 + 后摇"。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/SkillRoute/Route/Normal Route", fileName = "Route_Normal_")]
public sealed class NormalRouteDefinition : SkillRouteDefinition
{
    [Header("Graph (153.2)")]
    [SerializeField,
     Tooltip("单动作模式：开启时 Stage 数组锁定为 1，Graph Flow 边仅使用 Stage[0]。")]
    bool singleAction = true;

    public bool SingleActionMode => singleAction;

    public override RouteKind Kind => RouteKind.Normal;

    public override RouteGraphType GraphType => RouteGraphType.SingleAction;

    /// <summary>Graph Validator：Flow 边引用时须满足单 Stage。</summary>
    public bool IsSingleStageForGraph =>
        singleAction || StageCount <= 1;
}

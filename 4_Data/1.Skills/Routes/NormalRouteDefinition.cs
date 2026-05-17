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
    public override RouteKind Kind => RouteKind.Normal;
}

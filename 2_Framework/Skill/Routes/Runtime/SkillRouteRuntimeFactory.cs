using UnityEngine;

/// <summary>
/// RouteRuntime 工厂 — 按 Definition.Kind 创建匹配子类。/// 集中此处便于以后引入对象池（每个 Kind 一个池）。
/// </summary>
public static class SkillRouteRuntimeFactory
{
    public static SkillRouteRuntime Create(SkillRouteDefinition def)
    {
        if (def == null)
        {
            return null;
        }

        SkillRouteRuntime rt;
        switch (def.Kind)
        {
            case RouteKind.Normal:      rt = new NormalRouteRuntime();      break;
            case RouteKind.Combo:       rt = new ComboRouteRuntime();       break;
            case RouteKind.Charge:      rt = new ChargeRouteRuntime();      break;
            case RouteKind.MultiStage:  rt = new MultiStageRouteRuntime();  break;
            case RouteKind.Derivative:  rt = new DerivativeRouteRuntime();  break;
            case RouteKind.Directional:
                Debug.LogWarning(
                    $"[SkillRoute] RouteKind.Directional 已废弃（{def.name}），请迁移为 SkillGroup；按 Normal 注册。");
                rt = new NormalRouteRuntime();
                break;
            default:                    rt = new NormalRouteRuntime();      break;
        }

        rt.Bind(def);
        return rt;
    }
}

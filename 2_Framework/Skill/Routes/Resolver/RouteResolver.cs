using UnityEngine;

/// <summary>
/// [DEPRECATED Ver4.3.6+] 路由解析器 — 已被 <see cref="SkillEntryService.TryResolveForIntent"/> 内联优先级链替代。
///
/// 当前优先级链直接写在 SkillEntryService 中（按 hold 时长 + CanCast fallback），不再调本类。
/// 本类保留仅供未来"按 SkillEntryDefinition.RoutePriority 动态排序"等扩展实现，**不要在新代码里调用**。
/// 派生招判定 (TryResolveDerivative) 仍由 SkillEntryService 内部直接处理。
/// </summary>
public static class RouteResolver
{
    public struct ResolveResult
    {
        public SkillRouteDefinition Chosen;
        public DirectionalRouteType DirectionUsed;
        public RouteKind ChosenKind;
        public string DebugReason;
    }

    public static ResolveResult Resolve(
        SkillEntryDefinition entry,
        in InputSnapshot input,
        ComboRouteRuntime comboState,
        float now)
    {
        var result = default(ResolveResult);
        if (entry == null)
        {
            result.DebugReason = "entry=null";
            return result;
        }

        // 1) Charge — 仅 Press 沿（hold≈0 且 PressedEdge=true）才进 Startup；
        //    Release 沿（hold>0）若未越过 TapThreshold，按 Tap fallback 走 Combo/Normal。
        if (entry.ChargeRoute != null)
        {
            var ch = entry.ChargeRoute;
            var hold = input.TriggerHoldSeconds;
            var isPressEdge = input.TriggerPressedEdge && !input.TriggerReleasedEdge && hold <= 0.0001f;
            var heldEnough = hold >= ch.TapThreshold;

            if (isPressEdge)
            {
                result.Chosen = ch;
                result.ChosenKind = RouteKind.Charge;
                result.DebugReason = $"charge PRESS edge (hold={hold:F3})";
                return result;
            }

            if (heldEnough)
            {
                result.Chosen = ch;
                result.ChosenKind = RouteKind.Charge;
                result.DebugReason = $"charge RELEASE heldEnough (hold={hold:F3} ≥ tapThr={ch.TapThreshold:F2})";
                return result;
            }

            // Tap 回退路径：落到 Combo / Normal（继续往下走）。
        }

        // 2) Combo — 窗内才选
        if (entry.ComboRoute != null && comboState != null && comboState.IsComboWindowOpen(now))
        {
            result.Chosen = entry.ComboRoute;
            result.ChosenKind = RouteKind.Combo;
            result.DebugReason = "combo window open";
            return result;
        }

        // 3) Directional — 136.1 L7 已移除；四向由 SkillGroupDefinition + SkillEntryService 承担。

        // 4) MultiStage — 显式存在则用
        if (entry.MultiStageRoute != null)
        {
            result.Chosen = entry.MultiStageRoute;
            result.ChosenKind = RouteKind.MultiStage;
            result.DebugReason = "multi-stage";
            return result;
        }

        // 5) Normal — 兜底
        if (entry.NormalRoute != null)
        {
            result.Chosen = entry.NormalRoute;
            result.ChosenKind = RouteKind.Normal;
            result.DebugReason = "normal";
            return result;
        }

        result.DebugReason = "no route";
        return result;
    }

    /// <summary>包装：Resolve + 写入 Debug log（由 SkillEntryService 调）。</summary>
    public static ResolveResult ResolveWithDebug(
        Player owner,
        SkillEntryDefinition entry,
        in InputSnapshot input,
        ComboRouteRuntime comboState,
        float now)
    {
        var r = Resolve(entry, in input, comboState, now);
        return r;
    }

    /// <summary>派生招检查 — 父 Route Active 期间，由 SkillEntryService 在收到再次按键时调用。</summary>
    public static DerivativeRouteDefinition TryResolveDerivative(
        SkillEntryDefinition entry,
        SkillRouteRuntime activeParentRuntime,
        in SkillRouteContext ctx)
    {
        if (entry?.DerivativeRoutes == null || activeParentRuntime == null) return null;

        for (var i = 0; i < entry.DerivativeRoutes.Length; i++)
        {
            var d = entry.DerivativeRoutes[i];
            if (d == null) continue;
            if (d.ParentRoute != activeParentRuntime.Definition) continue;
            if (d.TriggerSlot != ctx.Input.TriggerSlot) continue;
            if (!ConditionEvaluator.EvaluateAll(d.UnlockConditions, in ctx, activeParentRuntime.Stage?.Elapsed ?? 0f))
            {
                continue;
            }
            return d;
        }

        return null;
    }
}

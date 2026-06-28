/// <summary>
/// 184.4 — Motion Grammar 决议：Transition 互斥 + Tap Facing 三分支。
/// </summary>
public static class MotionGrammar
{
    public static readonly MotionGrammarRule StartPrototype = new MotionGrammarRule
    {
        OwnsPresentation = true,
        ConsumesDirectionChange = true,
        ConsumesMomentumChange = true,
        BlocksOtherTransitions = true,
    };

    public static readonly MotionGrammarRule EndPrototype = new MotionGrammarRule
    {
        OwnsPresentation = true,
        ConsumesDirectionChange = true,
        ConsumesMomentumChange = true,
        BlocksOtherTransitions = true,
    };

    public static readonly MotionGrammarRule TurnPrototype = new MotionGrammarRule
    {
        OwnsPresentation = true,
        ConsumesDirectionChange = true,
        ConsumesMomentumChange = false,
        BlocksOtherTransitions = true,
    };

    public static readonly MotionGrammarRule PivotPrototype = new MotionGrammarRule
    {
        OwnsPresentation = true,
        ConsumesDirectionChange = true,
        ConsumesMomentumChange = true,
        BlocksOtherTransitions = true,
    };

    public static MotionGrammarRule ResolveGrammar(ActionDataSO action)
    {
        if (action == null)
        {
            return default;
        }

        if (action.OverrideGrammar)
        {
            return action.GrammarOverride;
        }

        return action.TransitionType switch
        {
            TransitionType.Start => StartPrototype,
            TransitionType.End => EndPrototype,
            TransitionType.Turn => TurnPrototype,
            TransitionType.Pivot => PivotPrototype,
            _ => default,
        };
    }

    /// <summary>蓝图 §8.1 别名。</summary>
    public static MotionGrammarRule Resolve(ActionDataSO action) => ResolveGrammar(action);

    public static string GetDocumentation(TransitionType type) => type switch
    {
        TransitionType.Start =>
            "起步负责解释起步。消费方向 + 动量。阻止 Turn / Pivot。\n" +
            "Tap Facing 期间 → Ignore（起步方向已锁定）。",
        TransitionType.End =>
            "急停负责解释停止与方向变化，不再追加 Turn 补偿。\n" +
            "Tap Facing 期间 → CachePendingFacing → End 结束时直接修正 LogicForward。",
        TransitionType.Turn =>
            "静止转向。消费方向，不消费动量。阻止 Pivot。\n" +
            "Turn 期间再来 Tap Facing → 替换为新 Turn。",
        TransitionType.Pivot =>
            "运动重定向。消费方向 + 动量。阻止 Turn。\n" +
            "Pivot 期间 Tap Facing → CachePendingFacing，Pivot 结束后应用。",
        _ => "非 Transition Action；不参与 Motion Grammar 决议。",
    };

    public static TapFacingDecision DecideTapFacing(ActionDataSO currentTransition)
    {
        if (currentTransition == null || currentTransition.TransitionType == TransitionType.None)
        {
            return TapFacingDecision.PlayTurnImmediately;
        }

        var grammar = ResolveGrammar(currentTransition);
        if (grammar.ConsumesDirectionChange)
        {
            if (currentTransition.TransitionType == TransitionType.Start)
            {
                return TapFacingDecision.Ignore;
            }

            if (currentTransition.TransitionType == TransitionType.Turn)
            {
                return TapFacingDecision.PlayTurnImmediately;
            }

            return TapFacingDecision.CachePendingFacing;
        }

        return TapFacingDecision.PlayTurnImmediately;
    }
}

public enum TapFacingDecision : byte
{
    PlayTurnImmediately,
    CachePendingFacing,
    Ignore,
}

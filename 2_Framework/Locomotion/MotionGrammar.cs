/// <summary>
/// 234.5 — Motion Grammar 只描述 Transition 所有权；WASD Tap Facing 三分支已退役。
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
            "起步负责表现起步。消费表现方向/动量语义，阻止重复 Turn / Pivot。",
        TransitionType.End =>
            "急停负责表现停止；新的移动输入按显式打断合同处理，不追加 Tap Turn。",
        TransitionType.Turn =>
            "静止转向表现。消费方向，不消费动量；有移动会话时不得触发。",
        TransitionType.Pivot =>
            "运动重定向表现。消费表现方向 + 动量语义；不得改变 FreeLocomotion 世界方向。",
        _ => "非 Transition Action；不参与 Motion Grammar 决议。",
    };
}

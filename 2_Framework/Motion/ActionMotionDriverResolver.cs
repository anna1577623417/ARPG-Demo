/// <summary>Action Motion Driver 的唯一运行时/编辑期解析结果。</summary>
public readonly struct ActionMotionExecutionPlan
{
    public ActionMotionDriverMode RequestedMode { get; }
    public ActionMotionDriverMode EffectiveMode { get; }
    public bool IsValid { get; }
    public bool UsesMotionExecutor { get; }
    public bool UsesClipRootMotion { get; }
    public bool RequiresBaseMotorTick { get; }
    public bool AllowsPlanarIntent { get; }
    public bool MaintainsVerticalPhysics { get; }
    public bool MaintainsGrounding { get; }
    public string ResolutionReason { get; }

    public ActionMotionExecutionPlan(
        ActionMotionDriverMode requestedMode,
        ActionMotionDriverMode effectiveMode,
        bool isValid,
        bool usesMotionExecutor,
        bool usesClipRootMotion,
        bool requiresBaseMotorTick,
        bool allowsPlanarIntent,
        bool maintainsVerticalPhysics,
        bool maintainsGrounding,
        string resolutionReason)
    {
        RequestedMode = requestedMode;
        EffectiveMode = effectiveMode;
        IsValid = isValid;
        UsesMotionExecutor = usesMotionExecutor;
        UsesClipRootMotion = usesClipRootMotion;
        RequiresBaseMotorTick = requiresBaseMotorTick;
        AllowsPlanarIntent = allowsPlanarIntent;
        MaintainsVerticalPhysics = maintainsVerticalPhysics;
        MaintainsGrounding = maintainsGrounding;
        ResolutionReason = resolutionReason ?? string.Empty;
    }

    public override string ToString() =>
        $"requested={RequestedMode} effective={EffectiveMode} valid={IsValid} " +
        $"executor={UsesMotionExecutor} clipRoot={UsesClipRootMotion} baseMotor={RequiresBaseMotorTick} " +
        $"planarIntent={AllowsPlanarIntent} vertical={MaintainsVerticalPhysics} grounding={MaintainsGrounding} " +
        $"reason={ResolutionReason}";
}

/// <summary>
/// 227.4：Runtime、Inspector、Validator 与专项 Log 共用的纯解析器。
/// LegacyAuto 必须严格保留改造前的 RootMotion → MP → 无 Executor 优先级。
/// </summary>
public static class ActionMotionDriverResolver
{
    public static ActionMotionExecutionPlan Resolve(ActionDataSO action)
    {
        if (action == null)
        {
            return Build(
                ActionMotionDriverMode.LegacyAuto,
                ActionMotionDriverMode.LegacyAuto,
                false,
                reason: "Action is null");
        }

        switch (action.MotionDriverMode)
        {
            case ActionMotionDriverMode.InheritStateMotor:
                return Build(
                    action.MotionDriverMode,
                    ActionMotionDriverMode.InheritStateMotor,
                    true,
                    baseMotor: true,
                    planarIntent: true,
                    vertical: true,
                    grounding: true,
                    reason: "Explicit inherited state motor");

            case ActionMotionDriverMode.MotionProfile:
                return action.MotionProfile != null
                    ? Build(
                        action.MotionDriverMode,
                        ActionMotionDriverMode.MotionProfile,
                        true,
                        executor: true,
                        vertical: true,
                        grounding: true,
                        reason: "Explicit MotionProfile executor")
                    : Build(
                        action.MotionDriverMode,
                        ActionMotionDriverMode.MotionProfile,
                        false,
                        reason: "Explicit MotionProfile mode requires a MotionProfile asset");

            case ActionMotionDriverMode.ClipRootMotion:
                return Build(
                    action.MotionDriverMode,
                    ActionMotionDriverMode.ClipRootMotion,
                    true,
                    clipRoot: true,
                    reason: "Explicit clip root motion");

            case ActionMotionDriverMode.Stationary:
                return Build(
                    action.MotionDriverMode,
                    ActionMotionDriverMode.Stationary,
                    true,
                    baseMotor: true,
                    planarIntent: false,
                    vertical: true,
                    grounding: true,
                    reason: "Explicit stationary physics tick");

            case ActionMotionDriverMode.LegacyAuto:
            default:
                return ResolveLegacy(action);
        }
    }

    static ActionMotionExecutionPlan ResolveLegacy(ActionDataSO action)
    {
        if (action.UseClipRootMotion)
        {
            return Build(
                ActionMotionDriverMode.LegacyAuto,
                ActionMotionDriverMode.ClipRootMotion,
                true,
                clipRoot: true,
                reason: "LegacyAuto: UseClipRootMotion=true");
        }

        if (action.MotionProfile != null)
        {
            return Build(
                ActionMotionDriverMode.LegacyAuto,
                ActionMotionDriverMode.MotionProfile,
                true,
                executor: true,
                vertical: true,
                grounding: true,
                reason: "LegacyAuto: MotionProfile assigned");
        }

        return Build(
            ActionMotionDriverMode.LegacyAuto,
            ActionMotionDriverMode.LegacyAuto,
            true,
            reason: "LegacyAuto: legacy presentation-only/no-executor path");
    }

    static ActionMotionExecutionPlan Build(
        ActionMotionDriverMode requested,
        ActionMotionDriverMode effective,
        bool valid,
        bool executor = false,
        bool clipRoot = false,
        bool baseMotor = false,
        bool planarIntent = false,
        bool vertical = false,
        bool grounding = false,
        string reason = "") =>
        new ActionMotionExecutionPlan(
            requested,
            effective,
            valid,
            executor,
            clipRoot,
            baseMotor,
            planarIntent,
            vertical,
            grounding,
            reason);
}

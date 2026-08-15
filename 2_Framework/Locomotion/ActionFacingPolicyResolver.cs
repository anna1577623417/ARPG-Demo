/// <summary>
/// 模块：2_Framework / Locomotion。职责：把 Action 作者 Policy 与可选 Gameplay 上下文收成 EffectiveFacingPolicy。
/// 237.3 LB — 无状态纯函数。不选槽、不写朝向、不读 Group Directional Input Frame。
/// LockOn 矩阵本版未通电：TrackTarget 一律降为 PreserveEntry 并标 TrackTargetOpen。
/// </summary>
public readonly struct FacingPolicyGameplayContext
{
    public readonly bool LockOnWired;
    public readonly bool HasLockOnTarget;

    public FacingPolicyGameplayContext(bool lockOnWired, bool hasLockOnTarget)
    {
        LockOnWired = lockOnWired;
        HasLockOnTarget = hasLockOnTarget;
    }

    public static FacingPolicyGameplayContext Unwired => default;
}

/// <summary>一次 Action Enter 的作者 Policy 与生效 Policy。Resolver 不持有生命周期。</summary>
public readonly struct ActionFacingPolicyResolution
{
    public readonly ActionFacingPolicy ActionPolicy;
    public readonly ActionFacingPolicy EffectivePolicy;
    public readonly bool TrackTargetOpen;

    public ActionFacingPolicyResolution(
        ActionFacingPolicy actionPolicy,
        ActionFacingPolicy effectivePolicy,
        bool trackTargetOpen)
    {
        ActionPolicy = actionPolicy;
        EffectivePolicy = effectivePolicy;
        TrackTargetOpen = trackTargetOpen;
    }
}

public static class ActionFacingPolicyResolver
{
    /// <summary>
    /// 输入：Action 上的 FacingPolicy，以及可选 LockOn 上下文。
    /// 输出：Authority 实际执行的 EffectivePolicy。不得按 Route Slot 推脸。
    /// </summary>
    public static ActionFacingPolicyResolution Resolve(
        ActionFacingPolicy actionPolicy,
        in FacingPolicyGameplayContext gameplay = default)
    {
        if (actionPolicy == ActionFacingPolicy.TrackTarget)
        {
            _ = gameplay.LockOnWired;
            _ = gameplay.HasLockOnTarget;
            return new ActionFacingPolicyResolution(
                actionPolicy,
                ActionFacingPolicy.PreserveEntryFacing,
                trackTargetOpen: true);
        }

        return new ActionFacingPolicyResolution(
            actionPolicy,
            actionPolicy,
            trackTargetOpen: false);
    }
}

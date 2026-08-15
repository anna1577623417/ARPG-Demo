using UnityEngine;

/// <summary>
/// 209.3 — 八向 Group 选路与 SplitFrame 双轴配置桥接。
/// 237 L3 — 删除 live ChordReframe。Selection Frame = History Down Snapshot 或 Trigger 当前轴。
/// </summary>
internal static class DirectionalRouteMotionBinding
{
    internal static SkillRouteDefinition SelectRouteForChord(
        SkillGroupDefinition group,
        Vector2 moveBuffered,
        Player owner,
        SkillEntrySlot slot,
        SkillContextGroupDefinition contextGroup,
        out DirectionalRouteType resolvedDir,
        out DirectionalContextResult context)
    {
        resolvedDir = DirectionalRouteType.Forward;
        context = DirectionalContextResult.Fail("no_group");
        if (group == null)
        {
            return null;
        }

        var timing = owner != null
            ? owner.ResolveDirectionalTiming(contextGroup)
            : DirectionalTimingProfileSO.Standard;
        var world = owner != null
            ? owner.ResolveCameraRelativeWorldDirection(moveBuffered)
            : new Vector3(moveBuffered.x, 0f, moveBuffered.y);
        var desired = owner != null ? owner.DesiredFacing : Vector3.forward;
        var history = owner != null ? owner.DirectionHistory : null;

        context = DirectionalContextResolver.Resolve(
            InputClock.UnscaledNow,
            history,
            timing,
            group.DirectionalInputFrame,
            moveBuffered,
            world,
            desired);

        if (!context.Success)
        {
            DirectionAuthority237Probe.ObserveCtxFail(owner, slot, group, context.FailReason);
            return null;
        }

        resolvedDir = context.Slot;
        var picked = group.SelectByDirection(resolvedDir);
        var profile = picked?.FirstStage()?.Action?.MotionProfile;
        var effectiveBasis = group.ResolveMotionCurveBasis(profile);
        DodgeChord8Probe.LogSplitFramePick(
            group.DirectionalInputFrame, effectiveBasis, context.Axis, resolvedDir, picked?.name);

        if (owner != null)
        {
            var now = Time.time;
            var holdDur = owner.InputContext.MoveHoldDurationSec(now);
            var chordWin = owner.LocomotionProfile != null && owner.LocomotionProfile.Tuning != null
                ? owner.LocomotionProfile.Tuning.ChordWindowSec
                : 0.12f;
            SkillGroupTurn237Probe.ObservePick(
                owner,
                slot,
                group,
                resolvedDir,
                resolvedDir,
                reframed: false,
                picked,
                isMotionMode: false,
                holdDur,
                chordWin,
                context.Axis);
            DirectionAuthority237Probe.ObserveCtxMatch(
                owner,
                slot,
                group,
                in context,
                picked,
                isMotionMode: false);
        }

        return picked;
    }
}

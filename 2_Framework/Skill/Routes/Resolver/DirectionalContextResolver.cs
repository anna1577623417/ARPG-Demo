using UnityEngine;

/// <summary>237 L3 — Event-Time 方向上下文。RecentChord 用 Down 快照，禁止默认读 live LogicForward。</summary>
public enum DirectionalContextMode : byte
{
    Neutral = 0,
    RecentChord = 1,
    HeldMovement = 2,
    MotionForward = 3
}

/// <summary>Trigger 一次查询的结果。ActionEntry 冻结的是 Slot + Basis，不是 live Transform。</summary>
public readonly struct DirectionalContextResult
{
    public readonly bool Success;
    public readonly DirectionalContextMode Mode;
    public readonly DirectionalRouteType Slot;
    public readonly Vector2 Axis;
    public readonly Vector3 WorldDir;
    public readonly Vector3 BasisFacing;
    public readonly int Token;
    public readonly float AgeSec;
    public readonly bool UsedLiveLogic;
    public readonly string FailReason;

    public DirectionalContextResult(
        bool success,
        DirectionalContextMode mode,
        DirectionalRouteType slot,
        Vector2 axis,
        Vector3 worldDir,
        Vector3 basisFacing,
        int token,
        float ageSec,
        bool usedLiveLogic,
        string failReason)
    {
        Success = success;
        Mode = mode;
        Slot = slot;
        Axis = axis;
        WorldDir = worldDir;
        BasisFacing = basisFacing;
        Token = token;
        AgeSec = ageSec;
        UsedLiveLogic = usedLiveLogic;
        FailReason = failReason;
    }

    public static DirectionalContextResult Fail(string reason) =>
        new DirectionalContextResult(
            false,
            DirectionalContextMode.Neutral,
            DirectionalRouteType.Forward,
            Vector2.zero,
            Vector3.zero,
            Vector3.forward,
            0,
            -1f,
            false,
            reason ?? "unknown");
}

/// <summary>
/// 237 L3 — 冻结到 Route/Action 进入的方向槽。同一 Intent 的 Peek 与 Action 内再解析复用，禁止连续 PICK。
/// </summary>
public readonly struct DirectionalActionEntry
{
    public readonly bool IsValid;
    public readonly bool BoundToAction;
    public readonly SkillEntrySlot EntrySlot;
    public readonly SkillGroupDefinition Group;
    public readonly SkillRouteDefinition Route;
    public readonly DirectionalRouteType Slot;
    public readonly Vector3 BasisFacing;
    public readonly Vector3 WorldDir;
    public readonly int Token;
    public readonly float IntentTimeStamp;
    public readonly DirectionalContextMode Mode;

    public DirectionalActionEntry(
        bool isValid,
        bool boundToAction,
        SkillEntrySlot entrySlot,
        SkillGroupDefinition group,
        SkillRouteDefinition route,
        DirectionalRouteType slot,
        Vector3 basisFacing,
        Vector3 worldDir,
        int token,
        float intentTimeStamp,
        DirectionalContextMode mode)
    {
        IsValid = isValid;
        BoundToAction = boundToAction;
        EntrySlot = entrySlot;
        Group = group;
        Route = route;
        Slot = slot;
        BasisFacing = basisFacing;
        WorldDir = worldDir;
        Token = token;
        IntentTimeStamp = intentTimeStamp;
        Mode = mode;
    }

    public static DirectionalActionEntry Capture(
        SkillEntrySlot entrySlot,
        SkillGroupDefinition group,
        SkillRouteDefinition route,
        in DirectionalContextResult context,
        float intentTimeStamp)
    {
        return new DirectionalActionEntry(
            true,
            false,
            entrySlot,
            group,
            route,
            context.Slot,
            context.BasisFacing,
            context.WorldDir,
            context.Token,
            intentTimeStamp,
            context.Mode);
    }

    public DirectionalActionEntry BindToAction() =>
        new DirectionalActionEntry(
            IsValid,
            true,
            EntrySlot,
            Group,
            Route,
            Slot,
            BasisFacing,
            WorldDir,
            Token,
            IntentTimeStamp,
            Mode);

    public bool ShouldReuse(
        SkillEntrySlot entrySlot,
        SkillGroupDefinition group,
        float intentTimeStamp,
        SkillRouteRuntime activeRuntime)
    {
        if (!IsValid || entrySlot != EntrySlot || !ReferenceEquals(group, Group))
        {
            return false;
        }

        if (Mathf.Abs(intentTimeStamp - IntentTimeStamp) <= 0.0001f)
        {
            return true;
        }

        return BoundToAction
               && activeRuntime != null
               && ReferenceEquals(activeRuntime.Definition, Route);
    }
}

/// <summary>
/// 237 L3 — Skill Trigger 的 Event-Time Query。Selection Frame 来自 History Down 或 Trigger 当前轴，不读 owner.LogicForward。
/// </summary>
public static class DirectionalContextResolver
{
    const float AxisDeadzoneSq = 0.0001f;

    public static DirectionalContextResult Resolve(
        float triggerUnscaledTime,
        DirectionInputHistory history,
        in DirectionalTimingSnapshot timing,
        DirectionalInputFrame inputFrame,
        Vector2 currentAxis,
        Vector3 currentWorldDir,
        Vector3 currentDesiredFacing)
    {
        var pre = Mathf.Max(0f, timing.PreTriggerWindowSec);
        var hasCurrent = currentAxis.sqrMagnitude > AxisDeadzoneSq;

        if (history != null && history.TryGetLatestDown(out var edge) && edge.Token > 0)
        {
            var age = triggerUnscaledTime - edge.UnscaledTime;
            if (age < 0f)
            {
                age = 0f;
            }

            if (age <= pre)
            {
                var slot = DirectionalFrameResolver.ResolveInputChord(
                    inputFrame,
                    edge.Raw,
                    edge.WorldDir,
                    edge.BasisFacing);
                return new DirectionalContextResult(
                    true,
                    DirectionalContextMode.RecentChord,
                    slot,
                    edge.Raw,
                    edge.WorldDir,
                    edge.BasisFacing,
                    edge.Token,
                    age,
                    usedLiveLogic: false,
                    failReason: null);
            }

            if (hasCurrent)
            {
                var slot = DirectionalFrameResolver.ResolveInputChord(
                    inputFrame,
                    currentAxis,
                    currentWorldDir,
                    currentDesiredFacing);
                return new DirectionalContextResult(
                    true,
                    DirectionalContextMode.HeldMovement,
                    slot,
                    currentAxis,
                    currentWorldDir,
                    currentDesiredFacing,
                    edge.Token,
                    age,
                    usedLiveLogic: false,
                    failReason: null);
            }

            return DirectionalContextResult.Fail("no_snapshot");
        }

        if (hasCurrent)
        {
            var slot = DirectionalFrameResolver.ResolveInputChord(
                inputFrame,
                currentAxis,
                currentWorldDir,
                currentDesiredFacing);
            return new DirectionalContextResult(
                true,
                DirectionalContextMode.HeldMovement,
                slot,
                currentAxis,
                currentWorldDir,
                currentDesiredFacing,
                0,
                -1f,
                usedLiveLogic: false,
                failReason: null);
        }

        return DirectionalContextResult.Fail("no_snapshot");
    }
}

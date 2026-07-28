using UnityEngine;

/// <summary>
/// 220.6.1 C2：只读受击方 ReactionProfile/Set，产出一次性的 ReactionPlan。
/// 不入队、不改状态、不调用 Animator 或 Motor。
/// </summary>
public static class ReactionResolver
{
    public static bool TryResolveRoute(
        Entity target,
        string routeId,
        out ReactionSetSO.Entry routeEntry)
    {
        routeEntry = default;
        if (target is not IReactionProfileOwner profileOwner
            || profileOwner.ReactionProfile == null
            || profileOwner.ReactionProfile.ReactionSet == null)
        {
            return false;
        }

        return TryFindRoute(
            profileOwner.ReactionProfile.ReactionSet,
            routeId,
            out routeEntry);
    }

    public static bool TryResolve(
        Entity target,
        in HitReaction hitReaction,
        in ImpulseRequest impulse,
        ulong sourceEventId,
        float now,
        out ReactionResolveResult result)
    {
        result = default;
        if (target == null)
        {
            result = ReactionResolveResult.Failed(false, "missing-target");
            return false;
        }

        if (target.IsDead)
        {
            result = ReactionResolveResult.Failed(true, "target-dead");
            return false;
        }

        if (target.UnitKind is UnitKind.Structure or UnitKind.Ward)
        {
            result = ReactionResolveResult.Failed(true, "structure-ignore");
            return false;
        }

        if (target is not IReactionProfileOwner profileOwner
            || profileOwner.ReactionProfile == null)
        {
            result = ReactionResolveResult.Failed(false, "no-profile");
            return false;
        }

        var profile = profileOwner.ReactionProfile;
        if (!TryFindProfileEntry(
                target,
                profile,
                in impulse,
                out var profileEntry,
                out var hitDirection))
        {
            result = ReactionResolveResult.Failed(true, "no-profile-entry");
            return false;
        }

        if (profile.ReactionSet == null)
        {
            result = ReactionResolveResult.Failed(true, "missing-reaction-set");
            return false;
        }

        if (!TryFindRoute(profile.ReactionSet, profileEntry.ReactionRouteId, out var routeEntry))
        {
            result = ReactionResolveResult.Failed(true, "missing-reaction-route");
            return false;
        }

        if (profileEntry.ApplyImpulseMotor
            && routeEntry.MotionAuthority == ReactionMotionAuthority.ActionMotion)
        {
            result = ReactionResolveResult.Failed(true, "motion-authority-conflict");
            return false;
        }

        var interrupt = profileEntry.CanInterruptAction
            ? routeEntry.InterruptDisposition
            : ReactionInterruptDisposition.Ignore;
        var superArmor = target is ITagOwner tagOwner
                         && tagOwner.Tags.HasAny(
                             TagCategory.Status,
                             (ulong)StatusTag.SuperArmor);
        var enqueueHitReact = profileEntry.EnqueueHitReact;
        if (superArmor)
        {
            switch (profileEntry.SuperArmorDisposition)
            {
                case ReactionSuperArmorDisposition.KeepImpulseOnly:
                    enqueueHitReact = false;
                    interrupt = ReactionInterruptDisposition.Ignore;
                    break;
                case ReactionSuperArmorDisposition.QueueAfterAction:
                    if (enqueueHitReact)
                    {
                        interrupt = ReactionInterruptDisposition.QueueAfterAction;
                    }
                    break;
            }
        }

        var plan = new ReactionPlan(
            sourceEventId,
            routeEntry.RouteId,
            interrupt,
            routeEntry.MotionAuthority,
            profileEntry.ApplyImpulseMotor,
            enqueueHitReact,
            superArmor,
            hitDirection,
            now + Mathf.Max(0.01f, profile.HitReactIntentBufferSeconds));

        result = ReactionResolveResult.Resolved(in plan);
        return true;
    }

    static bool TryFindProfileEntry(
        Entity target,
        ReactionProfileSO profile,
        in ImpulseRequest impulse,
        out ReactionProfileSO.Entry selected,
        out ReactionDirection resolvedDirection)
    {
        selected = default;
        resolvedDirection = ReactionDirection.Any;
        var found = false;
        if (profile.Entries == null)
        {
            return false;
        }

        resolvedDirection = ResolveHitDirection(target, in impulse);
        for (var i = 0; i < profile.Entries.Length; i++)
        {
            var candidate = profile.Entries[i];
            if (candidate.ImpulseKind != impulse.Kind
                || impulse.Force + 0.0001f < candidate.MinimumForce
                || !MatchesDirection(candidate.HitDirection, resolvedDirection))
            {
                continue;
            }

            if (!found
                || candidate.Priority > selected.Priority
                || (candidate.Priority == selected.Priority
                    && candidate.MinimumForce > selected.MinimumForce))
            {
                selected = candidate;
                found = true;
            }
        }

        return found;
    }

    static ReactionDirection ResolveHitDirection(Entity target, in ImpulseRequest impulse)
    {
        if (impulse.LaunchUpSpeed > 0.01f)
        {
            return ReactionDirection.Up;
        }

        if (impulse.Source == null || impulse.Source.Transform == null || target.transform == null)
        {
            return ReactionDirection.Any;
        }

        var localSourcePosition = target.transform.InverseTransformPoint(impulse.Source.Transform.position);
        localSourcePosition.y = 0f;
        if (localSourcePosition.sqrMagnitude < 0.0001f)
        {
            return ReactionDirection.Any;
        }

        if (Mathf.Abs(localSourcePosition.z) >= Mathf.Abs(localSourcePosition.x))
        {
            return localSourcePosition.z >= 0f
                ? ReactionDirection.Front
                : ReactionDirection.Back;
        }

        return localSourcePosition.x >= 0f
            ? ReactionDirection.Right
            : ReactionDirection.Left;
    }

    static bool MatchesDirection(ReactionDirection configured, ReactionDirection resolved)
    {
        return configured == ReactionDirection.Any
            || resolved == ReactionDirection.Any
            || configured == resolved;
    }

    static bool TryFindRoute(
        ReactionSetSO set,
        string routeId,
        out ReactionSetSO.Entry selected)
    {
        selected = default;
        if (string.IsNullOrWhiteSpace(routeId))
        {
            return false;
        }

        if (set.Entries == null)
        {
            return false;
        }

        for (var i = 0; i < set.Entries.Length; i++)
        {
            var candidate = set.Entries[i];
            if (string.Equals(candidate.RouteId, routeId, System.StringComparison.Ordinal))
            {
                selected = candidate;
                return true;
            }
        }

        return false;
    }
}

/// <summary>一次命中解析后的不可变受击计划；C2 只携带数据，不执行副作用。</summary>
public readonly struct ReactionPlan
{
    public readonly ulong SourceEventId;
    public readonly string RouteId;
    public readonly ReactionInterruptDisposition InterruptDisposition;
    public readonly ReactionMotionAuthority MotionAuthority;
    public readonly bool ApplyImpulseMotor;
    public readonly bool EnqueueHitReact;
    public readonly bool SuperArmorApplied;
    public readonly ReactionDirection HitDirection;
    public readonly float ExpiresAt;

    public ReactionPlan(
        ulong sourceEventId,
        string routeId,
        ReactionInterruptDisposition interruptDisposition,
        ReactionMotionAuthority motionAuthority,
        bool applyImpulseMotor,
        bool enqueueHitReact,
        bool superArmorApplied,
        ReactionDirection hitDirection,
        float expiresAt)
    {
        SourceEventId = sourceEventId;
        RouteId = routeId;
        InterruptDisposition = interruptDisposition;
        MotionAuthority = motionAuthority;
        ApplyImpulseMotor = applyImpulseMotor;
        EnqueueHitReact = enqueueHitReact;
        SuperArmorApplied = superArmorApplied;
        HitDirection = hitDirection;
        ExpiresAt = expiresAt;
    }
}

/// <summary>ReactionResolver 的纯数据结果，区分无 Profile 与规则失败。</summary>
public readonly struct ReactionResolveResult
{
    public readonly bool HasProfile;
    public readonly bool IsResolved;
    public readonly string Reason;
    public readonly ReactionPlan Plan;

    ReactionResolveResult(bool hasProfile, bool isResolved, string reason, in ReactionPlan plan)
    {
        HasProfile = hasProfile;
        IsResolved = isResolved;
        Reason = reason;
        Plan = plan;
    }

    public static ReactionResolveResult Failed(bool hasProfile, string reason)
        => new ReactionResolveResult(hasProfile, false, reason, default);

    public static ReactionResolveResult Resolved(in ReactionPlan plan)
        => new ReactionResolveResult(true, true, "resolved", in plan);
}

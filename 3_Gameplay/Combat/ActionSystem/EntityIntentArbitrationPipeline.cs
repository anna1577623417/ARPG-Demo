using UnityEngine;

/// <summary>
/// 220.5 B5.10：实体共用的技能意图仲裁管线。
/// <para>Evaluate 只读取 Intent 与 SkillEntry，产出可提交的 ArbitrationDecision；Lease、FSM 和 IntentBuffer 的修改由实体 StateManager 的 Commit 阶段完成。</para>
/// <para>Player 的输入快照由 PlayerStateManager 提供；Recovery、Graph 调试与旧 PendingAction 提交仍保留在 Player 侧。</para>
/// </summary>
public static class EntityIntentArbitrationPipeline
{
    public static ArbitrationDecision EvaluateCombatIntent(
        ISkillHost host,
        SkillEntryService entries,
        in GameplayIntent intent,
        float now)
    {
        if (host == null || entries == null)
        {
            return new ArbitrationDecision(
                route: null,
                firstAction: null,
                discardIntent: false,
                reason: "missing-host-or-entries");
        }

        var input = BuildDefaultInputSnapshot(in intent);
        return EvaluateCombatIntent(host, entries, in intent, in input, now);
    }

    public static ArbitrationDecision EvaluateCombatIntent(
        ISkillHost host,
        SkillEntryService entries,
        in GameplayIntent intent,
        in InputSnapshot input,
        float now)
    {
        if (host == null || entries == null)
        {
            return new ArbitrationDecision(
                route: null,
                firstAction: null,
                discardIntent: false,
                reason: "missing-host-or-entries");
        }

        var route = entries.TryResolveForIntent(in intent, in input, now, out var discardIntent);
        if (route == null)
        {
            return new ArbitrationDecision(
                route: null,
                firstAction: null,
                discardIntent: discardIntent,
                reason: discardIntent ? "no-route-discard" : "no-route");
        }

        var firstStage = entries.ResolveStartStage(route, now);
        return new ArbitrationDecision(
            route,
            firstStage?.Action,
            discardIntent: false,
            reason: firstStage?.Action != null ? "route-ready" : "route-without-action");
    }

    public static bool TryPrepareCombatCommit(
        ISkillHost skillHost,
        IActionIntentCommitter committer,
        in GameplayIntent intent,
        in ArbitrationDecision decision,
        out string reason)
    {
        if (skillHost == null || committer == null)
        {
            skillHost?.ClearPendingAction();
            reason = "missing-commit-capability";
            return false;
        }

        if (!decision.IsResolved)
        {
            skillHost.ClearPendingAction();
            reason = "missing-route";
            return false;
        }

        if (!committer.TryCommitActionIntent(in intent, in decision, out reason))
        {
            skillHost.ClearPendingAction();
            return false;
        }

        return true;
    }

    public static bool TryProcessCombatIntent<T>(
        T entity,
        EntityState<T> current,
        IIntentHost intentHost,
        ISkillHost skillHost,
        SkillEntryService entries,
        IActionIntentCommitter committer,
        in GameplayIntent intent,
        in InputSnapshot input,
        in FrameContext frameContext,
        float now,
        out ArbitrationDecision decision,
        out string phase,
        out string reason)
        where T : Entity<T>
    {
        decision = EvaluateCombatIntent(skillHost, entries, in intent, in input, now);
        if (!decision.IsResolved)
        {
            skillHost?.ClearPendingAction();
            phase = "Resolve";
            reason = decision.Reason;
            return false;
        }

        return TryCommitEvaluatedCombatIntent(
            entity,
            current,
            intentHost,
            skillHost,
            entries,
            committer,
            in intent,
            in decision,
            in frameContext,
            out phase,
            out reason);
    }

    public static bool TryCommitEvaluatedCombatIntent<T>(
        T entity,
        EntityState<T> current,
        IIntentHost intentHost,
        ISkillHost skillHost,
        SkillEntryService entries,
        IActionIntentCommitter committer,
        in GameplayIntent intent,
        in ArbitrationDecision decision,
        in FrameContext frameContext,
        out string phase,
        out string reason)
        where T : Entity<T>
    {
        if (entity == null || current == null || intentHost == null || skillHost == null || entries == null)
        {
            skillHost?.ClearPendingAction();
            phase = "Commit";
            reason = "missing-state-or-commit-capability";
            return false;
        }

        if (!TryPrepareCombatCommit(skillHost, committer, in intent, in decision, out reason))
        {
            phase = "Commit";
            return false;
        }

        if (!current.TryConsumeGameplayIntent(entity, in frameContext, in intent))
        {
            skillHost.ClearPendingAction();
            phase = "StateGate";
            reason = current.StateId;
            return false;
        }

        if (GameplayIntent.TryIntentKindToSlot(intent.Kind, out var slot))
        {
            entries.NotifyRouteEntered(decision.Route, slot);
        }

        intentHost.IntentBuffer.Pop();
        phase = "Commit";
        reason = decision.Reason;
        return true;
    }

    public static void Tick<T>(
        IEntityIntentArbitrationPort<T> port,
        float deltaTime,
        float now)
        where T : Entity<T>
    {
        if (port == null || port.Entity == null || port.Current == null || port.IntentHost == null)
        {
            return;
        }

        port.SkillEntries?.TickCooldowns(deltaTime);
        port.IntentHost.FlushExpiredIntents(now);

        var maxConsumptions = Mathf.Max(1, port.MaxIntentConsumptionsPerFrame);
        for (var i = 0; i < maxConsumptions; i++)
        {
            if (!port.IntentHost.IntentBuffer.TryPeek(out var intent))
            {
                break;
            }

            var frameContext = port.BuildFrameContext(deltaTime);
            if (!TransitionResolver.CanOfferIntent(in frameContext, in intent, out var transitionReason))
            {
                port.LogTransitionBlocked(in intent, transitionReason);
                break;
            }

            var lane = ActionIntentRouting.ResolveLane(in intent, pendingAction: null);
            if (lane == ActionIntentCategory.Combat)
            {
                var input = port.BuildInputSnapshot(in intent);
                var decision = EvaluateCombatIntent(
                    port.SkillHost,
                    port.SkillEntries,
                    in intent,
                    in input,
                    now);

                if (!decision.IsResolved)
                {
                    port.SkillHost?.ClearPendingAction();
                    port.LogResolveBlocked(in intent, in decision);
                    if (decision.DiscardIntent)
                    {
                        port.IntentHost.IntentBuffer.Pop();
                        continue;
                    }

                    break;
                }

                if (!port.IsRouteAllowed(decision.Route, out var routeReason))
                {
                    port.SkillHost?.ClearPendingAction();
                    port.LogRouteRejected(in intent, decision.Route, routeReason);
                    port.IntentHost.IntentBuffer.Pop();
                    continue;
                }

                if (!TryCommitEvaluatedCombatIntent(
                    port.Entity,
                    port.Current,
                    port.IntentHost,
                    port.SkillHost,
                    port.SkillEntries,
                    port.ActionCommitter,
                    in intent,
                    in decision,
                    in frameContext,
                    out var phase,
                    out var commitReason))
                {
                    if (phase == "StateGate")
                    {
                        port.LogStateGateBlocked(in intent, decision.Route, commitReason);
                    }
                    else
                    {
                        port.LogCommitBlocked(in intent, decision.Route, commitReason);
                    }

                    break;
                }

                port.LogResolved(in intent, in decision);
                port.LogConsumed(in intent, decision.Route, decision.Reason);
                continue;
            }

            if (!port.Current.TryConsumeGameplayIntent(port.Entity, in frameContext, in intent))
            {
                port.SkillHost?.ClearPendingAction();
                port.LogStateGateBlocked(in intent, null, port.Current.StateId);
                break;
            }

            port.IntentHost.IntentBuffer.Pop();
            port.LogConsumed(in intent, null, "global");
        }
    }

    /// <summary>
    /// 兼容旧 Player 调用方的过渡包装器；Enemy 新路径不再在 Evaluate 阶段 Arm。
    /// </summary>
    public static bool TryResolveCombatIntent(
        ISkillHost host,
        SkillEntryService entries,
        in GameplayIntent intent,
        float now,
        out SkillRouteRuntime route,
        out bool discardIntent)
    {
        var decision = EvaluateCombatIntent(host, entries, in intent, now);
        route = decision.Route;
        discardIntent = decision.DiscardIntent;
        if (route == null)
        {
            host?.ClearPendingAction();
            return false;
        }

        if (decision.FirstAction != null)
        {
            host.ArmPendingAction(intent.Kind, decision.FirstAction);
        }

        return true;
    }

    public static InputSnapshot BuildDefaultInputSnapshot(in GameplayIntent intent)
    {
        var snapshot = new InputSnapshot
        {
            TriggerHoldSeconds = intent.HoldDurationSeconds,
            TriggerPressedEdge = true,
            TriggerReleasedEdge = intent.Semantic == InputSemanticType.Release,
            TriggerHolding = intent.Semantic == InputSemanticType.Charge,
            MoveBuffered = intent.MoveBuffered,
            MoveBufferValid = intent.MoveBufferValid,
        };

        if (GameplayIntent.TryIntentKindToSlot(intent.Kind, out var slot))
        {
            snapshot.TriggerSlot = SkillEntryService.CanonicalEntry(slot);
        }

        return snapshot;
    }
}

public readonly struct ArbitrationDecision
{
    public readonly SkillRouteRuntime Route;
    public readonly ActionDataSO FirstAction;
    public readonly bool DiscardIntent;
    public readonly string Reason;

    public bool IsResolved => Route != null;

    public ArbitrationDecision(
        SkillRouteRuntime route,
        ActionDataSO firstAction,
        bool discardIntent,
        string reason)
    {
        Route = route;
        FirstAction = firstAction;
        DiscardIntent = discardIntent;
        Reason = reason;
    }
}

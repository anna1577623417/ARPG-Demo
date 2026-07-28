using System.Collections.Generic;

using UnityEngine;

[RequireComponent(typeof(Enemy))]
[RequireComponent(typeof(AIController))]
[RequireComponent(typeof(EnemyPerception))]
[AddComponentMenu("GameMain/Enemy/Enemy State Manager")]
public sealed class EnemyStateManager : EntityStateManager<Enemy>, IEntityIntentArbitrationPort<Enemy>
{
    [SerializeField] int maxIntentConsumptionsPerFrame = 1;

    public IIntentHost IntentHost => Entity;
    public ISkillHost SkillHost => Entity;
    public SkillEntryService SkillEntries => Entity?.SkillEntries;
    public IActionIntentCommitter ActionCommitter => Entity;
    public int MaxIntentConsumptionsPerFrame => maxIntentConsumptionsPerFrame;

    protected override List<EntityState<Enemy>> BuildStateList()
    {
        return new List<EntityState<Enemy>>
        {
            new EnemyLocomotionState(),
            new EnemyActionState(),
            new EnemyHitReactState(),
            new EnemyDeadState(),
        };
    }

    protected override void Start()
    {
        var enemy = GetComponent<Enemy>();
        string reason;
        if (enemy == null)
        {
            reason = "missing-enemy";
        }
        else
        {
            enemy.TryValidateRuntime(out reason);
        }

        if (!string.IsNullOrEmpty(reason))
        {
            Debug.LogError($"[Enemy] Runtime event=Ready result=Blocked reason={reason}", this);
            return;
        }

        base.Start();
        Entity?.BindStateManager(this);
        Entity?.MarkRuntimeReady();
        if (EnemyRuntimeDiag.IsEnabled)
        {
            Debug.Log(
                $"[Enemy] Runtime event=Ready result=Accepted entity={Entity?.name ?? "-"} " +
                $"instanceId={Entity?.GetInstanceID() ?? 0}",
                this);
        }
    }

    protected override void OnPreLogicUpdate(float deltaTime)
    {
        if (Entity == null || Current == null)
        {
            return;
        }

        if (Entity.IntentBuffer.TryPeek(out var expiredIntent)
            && expiredIntent.Kind == GameplayIntentKind.HitReact
            && expiredIntent.ExpireTime < Time.time)
        {
            if (EnemyRuntimeDiag.IsEnabled
                || GameMainDebugSettings.ReactionDirection2206Log)
            {
                Debug.Log(
                    $"[Enemy] HitReact phase=Expire result=Discard " +
                    $"route={expiredIntent.ReactionRouteId ?? "-"} " +
                    $"sourceEventId={expiredIntent.ReactionSourceEventId} " +
                    $"reason=buffer-expired log=220.6",
                    Entity);
            }
        }

        Entity.FlushExpiredIntents(Time.time);
        if (TryConsumeHitReactIntent())
        {
            return;
        }

        EntityIntentArbitrationPipeline.Tick(this, deltaTime, Time.time);
    }

    bool TryConsumeHitReactIntent()
    {
        if (!Entity.IntentBuffer.TryPeek(out var intent)
            || intent.Kind != GameplayIntentKind.HitReact)
        {
            return false;
        }

        if (!ReactionResolver.TryResolveRoute(Entity, intent.ReactionRouteId, out var route))
        {
            Entity.IntentBuffer.Pop();
            LogHitReact(intent, "Discard", "missing-reaction-route");
            return true;
        }

        var currentIsAction = IsCurrentOfType<EnemyActionState>();
        if (currentIsAction)
        {
            switch (intent.ReactionInterruptDisposition)
            {
                case ReactionInterruptDisposition.Ignore:
                    Entity.IntentBuffer.Pop();
                    LogHitReact(intent, "Discard", "interrupt-ignore");
                    return true;
                case ReactionInterruptDisposition.QueueAfterAction:
                    LogHitReact(intent, "Retain", "queue-after-action");
                    return true;
            }

            Entity.CancelActiveRoute(ActionCancelReason.HitReact);
        }

        if (route.Action == null)
        {
            Entity.IntentBuffer.Pop();
            LogHitReact(intent, "Discard", "missing-action");
            return true;
        }

        var lease = Entity.CreateActionLease(
            GameplayIntentKind.HitReact,
            route.Action,
            route: null,
            motionProfile: route.MotionProfile);
        if (!Entity.TryArmActionLease(in lease))
        {
            LogHitReact(intent, "Retain", "action-lease-arm-failed");
            return true;
        }

        Entity.IntentBuffer.Pop();
        ForceChange<EnemyHitReactState>();
        LogHitReact(intent, "Consumed", route.RouteId);
        return true;
    }

    void LogHitReact(in GameplayIntent intent, string result, string reason)
    {
        if (!EnemyRuntimeDiag.IsEnabled
            && !GameMainDebugSettings.ReactionDirection2206Log)
        {
            return;
        }

        Debug.Log(
            $"[Enemy] HitReact phase=Commit kind={intent.Kind} result={result} " +
            $"route={intent.ReactionRouteId ?? "-"} sourceEventId={intent.ReactionSourceEventId} " +
            $"reason={reason} state={Current?.StateId ?? "-"} " +
            $"instanceId={Entity?.GetInstanceID() ?? 0} log=220.6",
            Entity);
    }

    public FrameContext BuildFrameContext(float deltaTime) => Entity.BuildFrameContext(deltaTime);

    public InputSnapshot BuildInputSnapshot(in GameplayIntent intent)
        => EntityIntentArbitrationPipeline.BuildDefaultInputSnapshot(in intent);

    public bool IsRouteAllowed(SkillRouteRuntime route, out string reason)
    {
        if (route == null)
        {
            reason = "missing-route";
            return false;
        }

        if (route.Kind != RouteKind.Normal)
        {
            reason = "B3.6 supports NormalRoute only";
            return false;
        }

        reason = null;
        return true;
    }

    public void LogTransitionBlocked(in GameplayIntent intent, string reason)
        => EnemyRuntimeDiag.LogArbitration(Entity, intent.Kind, "Transition", "Blocked", reason: reason);

    public void LogResolveBlocked(in GameplayIntent intent, in ArbitrationDecision decision)
        => EnemyRuntimeDiag.LogArbitration(
            Entity,
            intent.Kind,
            "Resolve",
            decision.DiscardIntent ? "Discard" : "Queued",
            reason: decision.Reason);

    public void LogRouteRejected(in GameplayIntent intent, SkillRouteRuntime route, string reason)
        => EnemyRuntimeDiag.LogArbitration(Entity, intent.Kind, "Resolve", "Discard", route, reason);

    public void LogCommitBlocked(in GameplayIntent intent, SkillRouteRuntime route, string reason)
        => EnemyRuntimeDiag.LogArbitration(Entity, intent.Kind, "Commit", "Blocked", route, reason);

    public void LogStateGateBlocked(in GameplayIntent intent, SkillRouteRuntime route, string reason)
        => EnemyRuntimeDiag.LogArbitration(Entity, intent.Kind, "StateGate", "Blocked", route, reason);

    public void LogResolved(in GameplayIntent intent, in ArbitrationDecision decision)
        => EnemyRuntimeDiag.LogArbitration(Entity, intent.Kind, "Resolve", "Resolved", decision.Route, decision.Reason);

    public void LogConsumed(in GameplayIntent intent, SkillRouteRuntime route, string reason)
        => EnemyRuntimeDiag.LogArbitration(Entity, intent.Kind, "Commit", "Consumed", route, reason);
}

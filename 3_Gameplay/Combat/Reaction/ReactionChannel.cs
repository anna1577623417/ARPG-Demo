using UnityEngine;

/// <summary>
/// 220.6.1 C2：FeedbackRouter 的 Reaction 单通道。
/// 只负责解析结果日志和 HitReact 意图入队，不直接播放动画。
/// </summary>
public static class ReactionChannel
{
    public static bool TryResolve(
        Entity target,
        in HitReaction hitReaction,
        in ImpulseRequest impulse,
        ulong eventId,
        out ReactionResolveResult result)
    {
        var resolved = ReactionResolver.TryResolve(
            target,
            in hitReaction,
            in impulse,
            eventId,
            Time.time,
            out result);

        if (GameMainDebugSettings.ReactionDirection2206Log)
        {
            var targetName = target != null ? target.name : "null";
            if (!result.HasProfile)
            {
                Debug.Log(
                    $"[Feedback] OPEN reaction: no profile eventId={eventId} target={targetName} log=220.6");
            }
            else if (!resolved)
            {
                if (result.Reason == "structure-ignore")
                {
                    Debug.Log(
                        $"[Feedback] Reaction ignore kind={target.UnitKind} " +
                        $"eventId={eventId} target={targetName} log=220.6");
                }
                else
                {
                    Debug.Log(
                        $"[Feedback] Reaction resolve result=Blocked eventId={eventId} " +
                        $"target={targetName} reason={result.Reason} log=220.6");
                }
            }
            else
            {
                Debug.Log(
                    $"[Feedback] Reaction resolve result=Resolved eventId={eventId} " +
                    $"target={targetName} route={result.Plan.RouteId} " +
                    $"direction={result.Plan.HitDirection} " +
                    $"enqueue={result.Plan.EnqueueHitReact} " +
                    $"applyImpulse={result.Plan.ApplyImpulseMotor} " +
                    $"interrupt={result.Plan.InterruptDisposition} " +
                    $"superArmor={result.Plan.SuperArmorApplied} log=220.6");
            }
        }

        return resolved;
    }

    public static bool EnqueueHitReact(
        Entity target,
        in ReactionPlan plan,
        ulong eventId)
    {
        if (target is not IIntentHost host)
        {
            LogEnqueue(eventId, target, "NoIntentHost", "Rejected");
            return false;
        }

        var now = Time.time;
        var bufferSeconds = Mathf.Max(0.01f, plan.ExpiresAt - now);
        var intent = GameplayIntent.ForHitReact(
            plan.RouteId,
            plan.SourceEventId,
            now,
            bufferSeconds,
            plan.InterruptDisposition,
            plan.SuperArmorApplied);
        var result = host.TryEnqueue(in intent);
        LogEnqueue(eventId, target, "Intent", result.ToString());
        return result == IntentEnqueueResult.Accepted
            || result == IntentEnqueueResult.Coalesced;
    }

    static void LogEnqueue(
        ulong eventId,
        Entity target,
        string channel,
        string result)
    {
        if (!GameMainDebugSettings.ReactionDirection2206Log)
        {
            return;
        }

        Debug.Log(
            $"[Feedback] channel=Reaction result={channel} eventId={eventId} " +
            $"target={(target != null ? target.name : "null")} enqueueResult={result} log=220.6");
    }
}

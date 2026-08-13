using UnityEngine;

/// <summary>
/// Entity Runtime 反馈单点的施工占位。
/// A3：CombatEventBus.Resolved 的唯一运行时订阅方，负责按固定顺序提交伤害、效果、冲量与表现反馈。
/// </summary>
public sealed class CombatFeedbackRouter : MonoBehaviour
{
    static CombatFeedbackRouter s_instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRuntime()
    {
        s_instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (s_instance != null)
        {
            return;
        }

        var host = new GameObject(nameof(CombatFeedbackRouter))
        {
            hideFlags = HideFlags.HideAndDontSave,
        };
        DontDestroyOnLoad(host);
        host.AddComponent<CombatFeedbackRouter>();
    }

    void OnEnable()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_instance = this;
        CombatEventBus.Resolved -= OnResolved;
        CombatEventBus.Resolved += OnResolved;
        if (GameMainDebugSettings.CombatHit)
        {
            Debug.Log("[Feedback] channel=Router state=Ready subscriber=CombatFeedbackRouter");
        }
    }

    void OnDisable()
    {
        CombatEventBus.Resolved -= OnResolved;
        if (s_instance == this)
        {
            s_instance = null;
        }
    }

    static void OnResolved(CombatResolvedEvent evt)
    {
        switch (evt.Interaction)
        {
            case CombatInteraction.Miss:
            case CombatInteraction.Invincible:
                return;

            case CombatInteraction.Hit:
                ApplyHit(in evt);
                return;

            case CombatInteraction.Guard:
                return;

            case CombatInteraction.Parry:
                ApplyParryStagger(in evt);
                return;

            case CombatInteraction.Clash:
                ApplyClash(in evt);
                return;

            default:
                return;
        }
    }

    static void ApplyClash(in CombatResolvedEvent evt)
    {
        ActionTimeScaleDriver.Instance?.RequestHitStop(0.2f);
        ClashSession.Enter(evt.Source, evt.Target);

        if (GameMainDebugSettings.CombatHit)
        {
            var sourceName = evt.Source != null ? evt.Source.name : "?";
            var targetName = evt.Target != null ? evt.Target.name : "?";
            Debug.Log(
                $"[Feedback] channel=Clash result=Applied eventId={evt.EventId} " +
                $"source={sourceName} target={targetName}");
        }
    }

    static void ApplyParryStagger(in CombatResolvedEvent evt)
    {
        ActionTimeScaleDriver.Instance?.RequestHitStop(0.12f);

        if (evt.Source is IImpulseReceiver receiver && evt.Source != null && evt.Target != null)
        {
            var away = evt.Source.transform.position - evt.Target.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 1e-4f)
            {
                var request = new ImpulseRequest(
                    away,
                    2.5f,
                    0f,
                    ImpulseKind.Small,
                    evt.Target as IEntity);
                var result = receiver.TryApplyImpulse(in request);
                LogImpulseResult(evt.Source, in request, result, evt.EventId);
            }
            else if (GameMainDebugSettings.CombatHit)
            {
                Debug.Log(
                    $"[Feedback] channel=Impulse result=IgnoredByProfile eventId={evt.EventId} " +
                    $"target={evt.Source.name} force=2.5 launch=0.0 kind=Small");
            }
        }

        if (GameMainDebugSettings.CombatHit)
        {
            Debug.Log(
                $"[Feedback] channel=Parry result=Applied eventId={evt.EventId} " +
                $"source={(evt.Source != null ? evt.Source.name : "null")} target={(evt.Target != null ? evt.Target.name : "null")}");
        }
    }

    static void ApplyHit(in CombatResolvedEvent evt)
    {
        ApplyDamage(in evt);
        ApplyHeal(in evt);
        ApplyOnHitEffect(in evt);

        var hasImpulse = TryBuildImpulseRequest(in evt, out var impulse);
        var reactionResolved = ReactionChannel.TryResolve(
            evt.Target,
            in evt.Reaction,
            in impulse,
            evt.EventId,
            out var reactionResult);

        var applyImpulse = !reactionResult.HasProfile
            || (reactionResolved && reactionResult.Plan.ApplyImpulseMotor);
        if (hasImpulse && applyImpulse)
        {
            DispatchImpulse(evt.Target, in impulse, evt.EventId);
        }

        if (reactionResolved && reactionResult.Plan.EnqueueHitReact)
        {
            ReactionChannel.EnqueueHitReact(evt.Target, in reactionResult.Plan, evt.EventId);
        }

        ReactionPresentChannel.Present(evt.Target, in evt.Reaction, evt.EventId);

        ApplyHitStop(in evt);
        ApplyCameraShake(in evt);
    }

    static void ApplyHeal(in CombatResolvedEvent evt)
    {
        var amount = evt.Outcome.HealAmount;
        if (amount <= 0f || evt.Target == null)
        {
            return;
        }

        var resources = evt.Target.Resources;
        var current = resources.GetCurrent(ResourceType.HP);
        var maximum = resources.GetMax(ResourceType.HP);
        resources.SetCurrent(ResourceType.HP, Mathf.Min(maximum, current + amount));

        if (GameMainDebugSettings.CombatHit)
        {
            Debug.Log(
                $"[Feedback] channel=Heal result=Applied eventId={evt.EventId} " +
                $"target={evt.Target.name} amount={amount:F1}");
        }
    }

    static void ApplyDamage(in CombatResolvedEvent evt)
    {
        if (evt.FinalDamage <= 0f || evt.Target == null)
        {
            return;
        }

        var result = new DamageResult(evt.FinalDamage, evt.IsCritical);
        if (evt.Target is IDamageable damageable)
        {
            damageable.ReceiveDamage(in result, in evt.Context);
        }
        else
        {
            evt.Target.TakeDamage(evt.FinalDamage, evt.Source);
        }

        if (GameMainDebugSettings.CombatHit)
        {
            Debug.Log(
                $"[Feedback] channel=Damage result=Applied eventId={evt.EventId} " +
                $"source={(evt.Source != null ? evt.Source.name : "null")} target={evt.Target.name} " +
                $"amount={evt.FinalDamage:F1} critical={evt.IsCritical} targetDead={evt.Target.IsDead}");
        }
    }

    static void ApplyOnHitEffect(in CombatResolvedEvent evt)
    {
        var effect = evt.Outcome.Effect;
        if (effect == null || evt.Target == null)
        {
            return;
        }

        if (evt.Target is not IEffectReceiver receiver)
        {
            if (GameMainDebugSettings.CombatHit)
            {
                Debug.Log(
                    $"[Feedback] channel=Effect result=NoReceiver eventId={evt.EventId} " +
                    $"target={evt.Target.name} effect={effect.name}");
            }

            return;
        }

        var applied = EffectSystem.ApplyEffect(evt.Source, receiver, effect);
        if (GameMainDebugSettings.CombatHit)
        {
            Debug.Log(
                $"[Feedback] channel=Effect result={(applied ? "Applied" : "Failed")} eventId={evt.EventId} " +
                $"source={(evt.Source != null ? evt.Source.name : "null")} target={evt.Target.name} effect={effect.name}");
        }
    }

    /// <summary>
    /// 将任意已构建的冲量请求交给目标能力接收者，并统一处理目标免力策略与诊断日志。
    /// </summary>
    public static ImpulseApplyResult DispatchImpulse(
        Entity target,
        in ImpulseRequest request,
        ulong eventId = 0UL,
        int objectId = 0)
    {
        if (target == null)
        {
            return ImpulseApplyResult.IgnoredByProfile;
        }

        if (target.UnitKind is UnitKind.Structure or UnitKind.Ward)
        {
            const ImpulseApplyResult ignored = ImpulseApplyResult.IgnoredByProfile;
            LogImpulseResult(target, in request, ignored, eventId, objectId);
            return ignored;
        }

        if (target is not IImpulseReceiver receiver)
        {
            if (GameMainDebugSettings.CombatHit)
            {
                Debug.Log(
                    $"[Feedback] channel=Impulse result=NoReceiver eventId={eventId} objectId={objectId} " +
                    $"target={target.name} force={request.Force:F1} launch={request.LaunchUpSpeed:F1} kind={request.Kind}");
            }

            return ImpulseApplyResult.IgnoredByProfile;
        }

        var result = receiver.TryApplyImpulse(in request);
        LogImpulseResult(target, in request, result, eventId, objectId);
        return result;
    }

    static bool TryBuildImpulseRequest(
        in CombatResolvedEvent evt,
        out ImpulseRequest request)
    {
        var reaction = evt.Reaction;
        if (evt.Target == null
            || (reaction.ImpulseForce <= 0.01f && reaction.LaunchUpSpeed <= 0.01f))
        {
            request = new ImpulseRequest(
                Vector3.zero,
                0f,
                0f,
                ImpulseKind.Custom,
                evt.Source as IEntity);
            return false;
        }

        var sourceRotation = evt.Source != null ? evt.Source.transform.rotation : Quaternion.identity;
        var localDirection = reaction.ImpulseLocalDir.sqrMagnitude > 1e-4f
            ? reaction.ImpulseLocalDir.normalized
            : Vector3.forward;
        var worldDirection = sourceRotation * localDirection;
        worldDirection.y = 0f;

        request = new ImpulseRequest(
            worldDirection,
            reaction.ImpulseForce,
            reaction.LaunchUpSpeed,
            reaction.LaunchUpSpeed > 0.01f ? ImpulseKind.Launch : ImpulseKind.Custom,
            evt.Source as IEntity);
        return true;
    }

    static void LogImpulseResult(
        Entity target,
        in ImpulseRequest request,
        ImpulseApplyResult result,
        ulong eventId = 0UL,
        int objectId = 0)
    {
        var log2206 = GameMainDebugSettings.ReactionDirection2206Log;
        if (!GameMainDebugSettings.CombatHit && !log2206)
        {
            return;
        }

        Debug.Log(
            $"[Feedback] channel=Impulse result={result} eventId={eventId} objectId={objectId} " +
            $"target={target.name} force={request.Force:F1} launch={request.LaunchUpSpeed:F1} kind={request.Kind}" +
            (log2206 ? " log=220.6" : string.Empty));
    }

    static void ApplyHitStop(in CombatResolvedEvent evt)
    {
        var seconds = evt.Reaction.HitStopSeconds;
        if (seconds <= 0f)
        {
            return;
        }

        ActionTimeScaleDriver.Instance?.RequestHitStop(seconds);

        if (GameMainDebugSettings.CombatHit)
        {
            Debug.Log(
                $"[Feedback] channel=HitStop result=Applied eventId={evt.EventId} seconds={seconds:F2}");
        }
    }

    static void ApplyCameraShake(in CombatResolvedEvent evt)
    {
        var intensity = evt.Reaction.CameraShakeIntensity;
        if (intensity <= 0.01f)
        {
            return;
        }

        var duration = evt.Reaction.CameraShakeDuration > 0.01f
            ? evt.Reaction.CameraShakeDuration
            : 0.12f;

        if (GameModeManager.Instance?.ActiveCameraController is ActionCameraController camera)
        {
            camera.AddImpulseShake(intensity, duration);
        }

        if (GameMainDebugSettings.CombatHit)
        {
            Debug.Log(
                $"[Feedback] channel=Camera result=Applied eventId={evt.EventId} " +
                $"intensity={intensity:F2} duration={duration:F2}");
        }
    }
}

using UnityEngine;

/// <summary>
/// 216.3 M3 — CombatEvent 默认订阅方：伤害 / 击退 / Launch / HitStop / 震屏 / OnHitEffect。
/// <para>与判定层解耦：只订阅 <see cref="CombatEventBus.Resolved"/>。</para>
/// </summary>
public static class CombatEventApplicator
{
    static bool s_booted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (s_booted)
        {
            return;
        }

        s_booted = true;
        CombatEventBus.Resolved -= OnResolved;
        CombatEventBus.Resolved += OnResolved;
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
                // 216.3 M5 L2：格挡不掉血；削韧数值 OPEN（下一切片可挂 Poise）。
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

    /// <summary>216.3 M5 L3 — 拼刀：HitStop + 双方 ClashSession（StatusTag.Clash）。</summary>
    static void ApplyClash(in CombatResolvedEvent evt)
    {
        ActionTimeScaleDriver.Instance?.RequestHitStop(0.2f);
        ClashSession.Enter(evt.Source, evt.Target);

        if (GameMainDebugSettings.CombatHit)
        {
            var a = evt.Source != null ? evt.Source.name : "?";
            var b = evt.Target != null ? evt.Target.name : "?";
            Debug.Log($"[CombatEvt] ClashState entered {a} ↔ {b}");
        }
    }

    /// <summary>216.3 M5 L2 — 弹反：攻击方短 HitStop + 轻推离，防守方不掉血。</summary>
    static void ApplyParryStagger(in CombatResolvedEvent evt)
    {
        ActionTimeScaleDriver.Instance?.RequestHitStop(0.12f);

        if (evt.Source is Player attacker && evt.Target != null)
        {
            var away = attacker.transform.position - evt.Target.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 1e-4f)
            {
                away.Normalize();
                attacker.SetPlanarVelocity(away * 2.5f);
            }
        }

        if (GameMainDebugSettings.CombatHit)
        {
            Debug.Log(
                $"[CombatEvt] Parry stagger source={(evt.Source != null ? evt.Source.name : "null")}");
        }
    }

    static void ApplyHit(in CombatResolvedEvent evt)
    {
        ApplyDamage(in evt);
        ApplyImpulse(in evt);
        ApplyHitStop(in evt);
        ApplyCameraShake(in evt);
        ApplyOnHitEffect(in evt);
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
    }

    /// <summary>216.3 M3 L3 — OnHitEffect 单点：EffectSystem.ApplyEffect(EffectDefinitionSO)。</summary>
    static void ApplyOnHitEffect(in CombatResolvedEvent evt)
    {
        var effect = evt.Reaction.OnHitEffect;
        if (effect == null || evt.Target == null)
        {
            return;
        }

        if (evt.Target is not IEffectReceiver receiver)
        {
            if (GameMainDebugSettings.CombatHit)
            {
                Debug.Log($"[CombatEvt] OnHitEffect skip target={evt.Target.name} (not IEffectReceiver)");
            }

            return;
        }

        var ok = EffectSystem.ApplyEffect(evt.Source, receiver, effect);
        if (GameMainDebugSettings.CombatHit)
        {
            Debug.Log(
                $"[CombatEvt] OnHitEffect={(ok ? "applied" : "failed")} effect={effect.name} → {evt.Target.name}");
        }
    }

    static void ApplyImpulse(in CombatResolvedEvent evt)
    {
        var reaction = evt.Reaction;
        if (evt.Target is not Player player)
        {
            return;
        }

        var sourceRot = evt.Source != null ? evt.Source.transform.rotation : Quaternion.identity;

        if (reaction.ImpulseForce > 0.01f)
        {
            var dir = reaction.ImpulseLocalDir.sqrMagnitude > 1e-4f
                ? reaction.ImpulseLocalDir.normalized
                : Vector3.forward;
            var worldDir = sourceRot * dir;
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude > 1e-4f)
            {
                worldDir.Normalize();
                player.SetPlanarVelocity(worldDir * reaction.ImpulseForce);
            }
        }

        if (reaction.LaunchUpSpeed > 0.01f)
        {
            var current = player.PlanarVelocity;
            player.SetPlanarVelocity(current + Vector3.up * reaction.LaunchUpSpeed);
        }
    }

    static void ApplyHitStop(in CombatResolvedEvent evt)
    {
        var sec = evt.Reaction.HitStopSeconds;
        if (sec <= 0f)
        {
            return;
        }

        ActionTimeScaleDriver.Instance?.RequestHitStop(sec);

        if (GameMainDebugSettings.CombatHit)
        {
            Debug.Log($"[CombatEvt] HITSTOP {sec:F2}s");
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

        if (GameModeManager.Instance?.ActiveCameraController is ActionCameraController cam)
        {
            cam.AddImpulseShake(intensity, duration);
        }
    }
}

using UnityEngine;

/// <summary>
/// AttackInstance 的 ActionContact 执行面。
/// </summary>
public sealed partial class AttackInstance
{
    readonly ContactRuntimeState _contactRuntime = new ContactRuntimeState();

    bool _contactMode;
    bool _contactSaturationReported;

    public bool UsesContactSpec => _contactMode;
    public ContactRuntimeState ContactRuntime => _contactRuntime;

    public void BeginContact(
        in ResolvedContactSpec spec,
        Entity source,
        ActionDataSO action,
        string eventId,
        string debugName,
        uint actionLeaseVersion,
        in ResolvedContactPose beginPose)
    {
        if (Active)
        {
            End();
        }

        Source = source;
        OriginPos = beginPose.Position;
        OriginRot = beginPose.Rotation;
        PrevSamplePos = beginPose.Position;
        Active = true;
        IsExpired = false;
        ElapsedSec = 0f;
        HasLastHit = false;
        LastHitResult = default;
        _contactMode = true;
        _contactSaturationReported = false;
        _clashedPartnerIds.Clear();

        var policy = NormalizePolicy(in spec.HitPolicy);
        if (policy.Kind == HitPolicyKind.PerSwing)
        {
            _registry.ResetSwing();
        }
        else
        {
            _registry.Clear();
        }

        _contactRuntime.Begin(
            in spec,
            action,
            eventId,
            debugName,
            actionLeaseVersion,
            in beginPose);

        if (spec.ShapeMode == HitShapeMode.WeaponTrace)
        {
            AttackTraceRegistry.Register(source, this);
        }

        if (GameMainDebugSettings.CombatHit)
        {
            Debug.Log(
                $"[Contact] BEGIN action={SafeName(action != null ? action.name : null)} " +
                $"eventId={SafeName(eventId)} lease={actionLeaseVersion} " +
                $"binding={spec.BindingMode} sweep={spec.SweepPolicy} frozen={beginPose.IsFrozen} " +
                $"shape={spec.ShapeMode} motion={spec.Motion} definitionRevision={spec.DefinitionRevision}");
        }

        ContactPoseGeometryBaselineProbe.TryCaptureRuntimeOnce(
            action != null ? action.name : null,
            eventId,
            actionLeaseVersion,
            in spec,
            beginPose.Position,
            beginPose.Rotation);
    }

    public void TickContact(in ResolvedContactPose currentPose)
    {
        if (!Active || !_contactMode)
        {
            return;
        }

        if (Source == null || Source.IsDead)
        {
            End();
            return;
        }

        ElapsedSec += Time.deltaTime;
        var spec = _contactRuntime.Spec;
        var policy = NormalizePolicy(in spec.HitPolicy);
        var allowHits = _registry.BeginFrame(in policy, ElapsedSec, out _);
        var candidates = _contactRuntime.Sample(Source, in currentPose);
        candidates.SortStable(currentPose.Position);

        if (spec.ShapeMode == HitShapeMode.WeaponTrace)
        {
            var weaponSampler = _contactRuntime.WeaponSampler;
            AttackTraceRegistry.UpdateSamples(
                Source,
                weaponSampler.TraceSamples,
                weaponSampler.SampleCount);
            TryResolveContactWeaponClash();
        }

        if (_contactRuntime.IsSaturated && !_contactSaturationReported)
        {
            _contactSaturationReported = true;
            Debug.LogWarning(
                $"[Contact] SATURATED action={_contactRuntime.ActionNameForDiagnostics()} " +
                $"eventId={_contactRuntime.EventId} sample={_contactRuntime.SampleId}");
        }

        if (allowHits)
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                CommitCandidate(in candidate, in policy);
            }
        }

        PrevSamplePos = OriginPos;
        OriginPos = currentPose.Position;
        OriginRot = currentPose.Rotation;
    }

    void CommitCandidate(in ContactCandidate candidate, in HitPolicyParams policy)
    {
        var target = candidate.Target;
        if (target == null)
        {
            return;
        }

        var targetId = target.GetInstanceID();
        if (!_registry.TryAccept(in policy, targetId))
        {
            return;
        }

        var spec = _contactRuntime.Spec;
        var fact = new ContactFact(
            Source,
            target,
            _contactRuntime.Action,
            _contactRuntime.EventId,
            _contactRuntime.ActionLeaseVersion,
            _contactRuntime.SampleId,
            spec.DefinitionRevision,
            spec.ShapeMode,
            spec.Motion,
            candidate.Point,
            candidate.Normal,
            candidate.BoneName,
            _registry.GetHitCount(targetId),
            ElapsedSec);
        var result = ContactFactHitResultAdapter.ToHitResult(in fact);
        LastHitResult = result;
        HasLastHit = true;

        if (GameMainDebugSettings.CombatHit)
        {
            Debug.Log(
                $"[Contact] FACT action={fact.ActionName} eventId={fact.ContactEventId} " +
                $"lease={fact.ActionLeaseVersion} sample={fact.SampleId} " +
                $"target={target.name} hitCount={fact.HitCountOnTarget}");
        }

        CombatEventBus.PublishResolved(in result, in spec.AttackProfile.Reaction);
    }

    void TryResolveContactWeaponClash()
    {
        if (!AttackTraceRegistry.TryFindClashOpponent(Source, out var opponent, out var point))
        {
            return;
        }

        var partnerId = opponent.GetInstanceID();
        if (!_clashedPartnerIds.Add(partnerId))
        {
            return;
        }

        var spec = _contactRuntime.Spec;
        var fact = new ContactFact(
            Source,
            opponent,
            _contactRuntime.Action,
            _contactRuntime.EventId,
            _contactRuntime.ActionLeaseVersion,
            _contactRuntime.SampleId,
            spec.DefinitionRevision,
            spec.ShapeMode,
            spec.Motion,
            point,
            Vector3.up,
            "WeaponClash",
            1,
            ElapsedSec);
        var result = ContactFactHitResultAdapter.ToHitResult(in fact);
        LastHitResult = result;
        HasLastHit = true;
        CombatEventBus.PublishResolved(in result, in spec.AttackProfile.Reaction);
    }

    void EndContactRuntime()
    {
        if (_contactRuntime.Spec.ShapeMode == HitShapeMode.WeaponTrace)
        {
            AttackTraceRegistry.Unregister(Source, this);
        }

        if (GameMainDebugSettings.CombatHit)
        {
            Debug.Log(
                $"[Contact] END action={_contactRuntime.ActionNameForDiagnostics()} " +
                $"eventId={_contactRuntime.EventId} lease={_contactRuntime.ActionLeaseVersion}");
        }

        _contactRuntime.End();
        _contactMode = false;
        Active = false;
        IsExpired = true;
    }
}

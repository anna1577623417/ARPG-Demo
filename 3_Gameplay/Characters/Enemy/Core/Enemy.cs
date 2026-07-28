using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(EnemyMotor))]
public sealed class Enemy : Entity<Enemy>, IEntity, IIntentHost, IMovementIntentHost, IImpulseReceiver, ITagOwner, IDamageable, IEffectReceiver, IActionContext, IActionLeaseOwner, IActionIntentCommitter, IReactionProfileOwner
{
    const int DefaultIntentCapacity = 16;

    [Header("Skill Entries")]
    [SerializeField] SkillEntryLoadoutSO skillEntryLoadout;

    [Header("Reaction")]
    [SerializeField] ReactionProfileSO reactionProfile;

    [Header("Locomotion Presentation")]
    [SerializeField] LocomotionProfile locomotionProfile;

    GameplayTagContainer _tags;
    EnemyStateManager _stateManager;
    EnemyMotor _motor;
    ActionLease _pendingActionLease;
    ActionLease _activeActionLease;
    uint _nextActionLeaseVersion;
    bool _hasPendingActionLease;
    bool _hasActiveActionLease;
    Vector3 _movementIntent;
    bool _hasMovementIntent;
    bool _wantsRun;
    bool _runtimeReady;
    string _runtimeReadyFailure;

    public GameplayIntentBuffer IntentBuffer { get; } = new GameplayIntentBuffer(DefaultIntentCapacity);
    public CombatObjectSpawner CombatObjectSpawner { get; } = new CombatObjectSpawner();
    public SkillEntryLoadoutSO SkillEntryLoadout => skillEntryLoadout;
    public ReactionProfileSO ReactionProfile => reactionProfile;
    public LocomotionProfile LocomotionProfile => locomotionProfile;
    public SkillEntryService SkillEntries { get; private set; }
    public IEntityMotor Motor => _motor;
    public ref GameplayTagContainer Tags => ref _tags;
    public ref GameplayTagMask GameplayTags => ref _tags.State;
    GameplayTagContainer ITagOwner.Tags => _tags;
    public bool IsAlive => !IsDead;
    public bool IsRuntimeReady => _runtimeReady;
    public string RuntimeReadyFailure => _runtimeReadyFailure;
    public bool HasPendingAction => _hasPendingActionLease && _pendingActionLease.Action != null;
    public bool HasMovementIntent => _hasMovementIntent;
    public Vector3 MovementIntent => _hasMovementIntent ? _movementIntent : Vector3.zero;
    public bool WantsRun => _wantsRun;

    Entity IIntentHost.Owner => this;
    Transform IEntity.Transform => transform;
    IReadOnlyStatSet IEntity.Stats => Stats;
    IResourcePool IEntity.Resources => Resources;
    Entity ISkillHost.Entity => this;
    SkillEntryLoadoutSO ISkillHost.SkillEntryLoadout => skillEntryLoadout;
    GameplayTagContainer ISkillHost.Tags => _tags;
    InputSemanticResolver ISkillHost.InputSemantic => null;
    float ISkillHost.SkillTime => Time.time;
    CombatContextSnapshot ISkillHost.BuildCombatContext(
        bool hitConfirmedThisStage,
        Vector2 moveOverride,
        bool moveOverrideValid)
    {
        return new CombatContextSnapshot
        {
            IsAirborne = false,
            MoveDirection = MoveDirection8.Forward,
            HitConfirmedThisStage = hitConfirmedThisStage,
            SnapshotTime = Time.time,
        };
    }
    void ISkillHost.ArmPendingAction(
        GameplayIntentKind kind,
        ActionDataSO action,
        float normalizedStart)
    {
        var lease = CreateActionLease(kind, action, route: null, normalizedStart);
        TryArm(in lease);
    }
    ActionDataSO ISkillHost.PeekPendingAction() => HasPendingAction ? _pendingActionLease.Action : null;
    void ISkillHost.ClearPendingAction()
    {
        ClearPendingActionLease();
    }
    void ISkillHost.NotifyRouteStageAction(ActionDataSO action)
    {
        EnemyRuntimeDiag.LogState(this, "Action", "RouteStageAction");
    }
    void ISkillHost.RemoveTag(TagCategory category, ulong bits) => _tags.Remove(category, bits);
    Transform IActionContext.Transform => transform;
    Animator IActionContext.Animator => base.Animator;
    IEntityMotor IActionContext.Motor => _motor;
    LocalEventBus IActionContext.EventBus => base.EventBus;
    CombatObjectSpawner IActionContext.CombatObjectSpawner => CombatObjectSpawner;
    void IActionContext.PublishActionPresentation(ActionTimelineMarkerKind kind, string payload)
        => PublishEvent(new EntityActionPresentationEvent(GetInstanceID(), kind, payload));
    void IActionContext.PublishTeleported(Vector3 worldPosition)
        => PublishEvent(new EntityTeleportedEvent(GetInstanceID(), name, worldPosition));

    public void RequestActionPresentation(
        GameplayIntentKind kind,
        ActionDataSO action,
        float normalizedStart = 0f)
    {
        PublishEvent(new EntityActionPlaybackRequestEvent(
            GetInstanceID(),
            kind,
            action,
            Mathf.Clamp01(normalizedStart)));
    }

    IBuffStack IEffectReceiver.BuffStack => Buffs;
    IReadOnlyStatSet IEffectReceiver.Stats => Stats;
    IResourcePool IEffectReceiver.Resources => Resources;

    public EnemyStateManager StateManager => _stateManager;

    public void SetMovementIntent(Vector3 worldDirection, bool wantsRun)
    {
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude <= 0.0001f)
        {
            ClearMovementIntent();
            return;
        }

        _movementIntent = worldDirection.normalized;
        _hasMovementIntent = true;
        _wantsRun = wantsRun;
    }

    public void ClearMovementIntent()
    {
        _movementIntent = Vector3.zero;
        _hasMovementIntent = false;
        _wantsRun = false;
    }

    protected override void Awake()
    {
        unitKind = UnitKind.Monster;
        if (teamId == 0)
        {
            teamId = 1;
        }

        base.Awake();
        _motor = GetComponent<EnemyMotor>();
        _tags.Faction.Set((ulong)FactionTag.Enemy);
        SkillEntries = new SkillEntryService((ISkillHost)this);
        if (skillEntryLoadout != null)
        {
            SkillEntries.Rebuild(skillEntryLoadout);
        }
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        CombatObjectSpawner?.Tick(Time.deltaTime);
    }

    internal void BindStateManager(EnemyStateManager stateManager)
    {
        _stateManager = stateManager;
    }

    internal void ApplyDefinition(EnemyDefinitionSO definition)
    {
        if (definition == null)
        {
            return;
        }

        entityId = definition.Id;
        displayName = definition.DisplayName;
        unitKind = definition.UnitKind;
        teamId = definition.TeamId;
        _tags.Faction.Set((ulong)definition.Faction);

        if (definition.Stats != null)
        {
            var baseStats = definition.Stats.BaseStats;
            for (var i = 0; i < baseStats.Count; i++)
            {
                var entry = baseStats[i];
                Stats.SetBase(entry.Type, entry.BaseValue);
            }

            Resources.SetCurrent(ResourceType.HP, RuntimeStats.MaxHealth);
        }

        skillEntryLoadout = definition.SkillLoadout;
        reactionProfile = definition.ReactionProfile;
        locomotionProfile = definition.LocomotionProfile;
        SkillEntries?.Rebuild(skillEntryLoadout);
    }

    internal bool TryValidateRuntime(out string reason)
    {
        if (_motor == null)
        {
            reason = "missing-motor";
        }
        else if (SkillEntries == null)
        {
            reason = "missing-skill-host";
        }
        else if (skillEntryLoadout == null)
        {
            reason = "missing-skill-loadout";
        }
        else
        {
            var presentation = FindPresentationPort();
            reason = presentation == null
                ? "missing-presentation-port"
                : presentation.IsReady ? null : "presentation-not-ready";
        }

        _runtimeReadyFailure = reason;
        return string.IsNullOrEmpty(reason);
    }

    IActionPresentationPort FindPresentationPort()
    {
        var components = GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
        for (var i = 0; i < components.Length; i++)
        {
            if (components[i] is IActionPresentationPort presentation)
            {
                return presentation;
            }
        }

        return null;
    }

    internal void MarkRuntimeReady()
    {
        _runtimeReady = true;
        _runtimeReadyFailure = null;
    }

    public ActionLease CreateActionLease(
        GameplayIntentKind kind,
        ActionDataSO action,
        SkillRouteRuntime route,
        float normalizedStart = 0f,
        MotionProfileSO motionProfile = null)
    {
        return new ActionLease(
            ++_nextActionLeaseVersion,
            kind,
            action,
            route,
            Mathf.Clamp01(normalizedStart),
            motionProfile);
    }

    public bool TryCommitActionIntent(
        in GameplayIntent intent,
        in ArbitrationDecision decision,
        out string reason)
    {
        if (!decision.IsResolved)
        {
            reason = "missing-route";
            return false;
        }

        if (decision.FirstAction == null)
        {
            reason = "route-without-action";
            return true;
        }

        var lease = CreateActionLease(intent.Kind, decision.FirstAction, decision.Route);
        if (!TryArmActionLease(in lease))
        {
            reason = "action-lease-arm-failed";
            return false;
        }

        reason = "action-lease-armed";
        return true;
    }

    public bool TryArmActionLease(in ActionLease lease) => TryArm(in lease);

    public bool TryConsumePendingAction(out ActionLease lease)
    {
        if (!_hasPendingActionLease)
        {
            lease = default;
            return false;
        }

        return TryConsume(_pendingActionLease.Version, out lease);
    }

    public bool TryConsume(uint version, out ActionLease lease)
    {
        if (!_hasPendingActionLease || _pendingActionLease.Version != version)
        {
            lease = default;
            return false;
        }

        lease = _pendingActionLease;
        _pendingActionLease = default;
        _hasPendingActionLease = false;
        _activeActionLease = lease;
        _hasActiveActionLease = true;
        if (EnemyRuntimeDiag.IsEnabled)
        {
            Debug.Log(
                $"[Enemy] ActionLease event=Consume version={lease.Version} " +
                $"kind={lease.Kind} action={lease.Action?.name ?? "-"}",
                this);
        }

        return true;
    }

    public bool TryArm(in ActionLease lease)
    {
        if (lease.Version == 0 || lease.Action == null)
        {
            return false;
        }

        if (_hasActiveActionLease)
        {
            return false;
        }

        if (_hasPendingActionLease)
        {
            _pendingActionLease = default;
            _hasPendingActionLease = false;
        }

        _pendingActionLease = lease;
        _hasPendingActionLease = true;
        if (EnemyRuntimeDiag.IsEnabled)
        {
            Debug.Log(
                $"[Enemy] ActionLease event=Arm version={lease.Version} " +
                $"kind={lease.Kind} route={lease.Route?.Definition?.name ?? "-"}",
                this);
        }

        return true;
    }

    public void CompleteActionLease(uint version)
    {
        if (_hasActiveActionLease && _activeActionLease.Version == version)
        {
            _activeActionLease = default;
            _hasActiveActionLease = false;
            if (EnemyRuntimeDiag.IsEnabled)
            {
                Debug.Log($"[Enemy] ActionLease event=Complete version={version}", this);
            }
        }
    }

    public void CancelActionLease(uint version, ActionCancelReason reason)
    {
        if (_hasPendingActionLease && _pendingActionLease.Version == version)
        {
            _pendingActionLease = default;
            _hasPendingActionLease = false;
        }

        if (_hasActiveActionLease && _activeActionLease.Version == version)
        {
            _activeActionLease = default;
            _hasActiveActionLease = false;
        }

        if (EnemyRuntimeDiag.IsEnabled)
        {
            Debug.Log(
                $"[Enemy] ActionLease event=Cancel version={version} reason={reason}",
                this);
        }
    }

    public void CancelActive(ActionCancelReason reason)
    {
        var version = _hasActiveActionLease
            ? _activeActionLease.Version
            : _hasPendingActionLease ? _pendingActionLease.Version : 0u;
        _activeActionLease = default;
        _pendingActionLease = default;
        _hasActiveActionLease = false;
        _hasPendingActionLease = false;

        if (version != 0 && EnemyRuntimeDiag.IsEnabled)
        {
            Debug.Log($"[Enemy] ActionLease event=Cancel version={version} reason={reason}", this);
        }
    }

    public void CancelActiveRoute(ActionCancelReason reason)
    {
        if (SkillEntries?.ActiveRoute != null)
        {
            SkillEntries.NotifyRouteExited(wasInterrupted: true);
        }

        CancelActive(reason);
    }

    void ClearPendingActionLease()
    {
        if (_hasPendingActionLease)
        {
            CancelActionLease(_pendingActionLease.Version, ActionCancelReason.Replaced);
        }
    }

    public IntentEnqueueResult TryEnqueue(in GameplayIntent intent)
    {
        if (IsDead)
        {
            if (EnemyRuntimeDiag.IsEnabled)
            {
                Debug.Log($"[Intent] channel=Enqueue result=RejectedOwnerDead host=Enemy kind={intent.Kind}", this);
            }

            return IntentEnqueueResult.RejectedOwnerDead;
        }

        IntentBuffer.Enqueue(in intent);
        if (EnemyRuntimeDiag.IsEnabled)
        {
            Debug.Log(
                $"[Intent] channel=Enqueue result=Accepted host=Enemy kind={intent.Kind} " +
                $"timestamp={intent.TimeStamp:F3} expire={intent.ExpireTime:F3}",
                this);
        }

        return IntentEnqueueResult.Accepted;
    }

    /// <summary>
    /// 220.5 B3.5：供 EnemyStateManager 接入的最小技能解析入口。
    /// 本阶段只 Resolve Route，不消费 Intent、不提交 Active Route，也不驱动 ActionTimeline。
    /// </summary>
    public SkillRouteRuntime TryResolveSkillIntent(
        in GameplayIntent intent,
        in InputSnapshot inputSnapshot,
        float now,
        out bool discardIntent)
    {
        if (SkillEntries == null)
        {
            discardIntent = false;
            return null;
        }

        return SkillEntries.TryResolveForIntent(
            in intent,
            in inputSnapshot,
            now,
            out discardIntent);
    }

    public void FlushExpiredIntents(float now) => IntentBuffer.FlushExpired(now);

    public bool TryTakePendingAction(
        out GameplayIntentKind kind,
        out ActionDataSO action,
        out float normalizedStart)
    {
        if (!TryConsumePendingAction(out var lease))
        {
            kind = default;
            action = null;
            normalizedStart = 0f;
            return false;
        }

        kind = lease.Kind;
        action = lease.Action;
        normalizedStart = lease.NormalizedStart;
        return true;
    }

    /// <summary>220.5 B3.6：给 TransitionResolver 的最小实体快照。</summary>
    public FrameContext BuildFrameContext(float deltaTime)
    {
        var planar = new Vector3(Velocity.x, 0f, Velocity.z);
        return new FrameContext
        {
            Time = Time.time,
            DeltaTime = deltaTime,
            IsGrounded = true,
            PlanarVelocity = planar,
            CurrentPlanarSpeed = planar.magnitude,
            VerticalSpeed = Velocity.y,
            CurrentTags = _tags.State,
            CurrentAbilityTags = _tags.Ability,
            StaminaCurrent = Resources.GetCurrent(ResourceType.Stamina),
            StaminaMax = Resources.GetMax(ResourceType.Stamina),
            IsPrimaryAttackHeld = false,
        };
    }

    public ImpulseApplyResult TryApplyImpulse(in ImpulseRequest request)
    {
        if (IsDead)
        {
            return ImpulseApplyResult.RejectedDead;
        }

        if (_motor == null)
        {
            return ImpulseApplyResult.RejectedNoMotor;
        }

        return _motor.TryApplyImpulse(in request);
    }

    public bool HasTag(GameplayTagMask mask)
    {
        var bits = mask.Value;
        return _tags.State.HasAll(bits)
               || _tags.Status.HasAll(bits)
               || _tags.Ability.HasAll(bits)
               || _tags.Mechanic.HasAll(bits)
               || _tags.Faction.HasAll(bits);
    }

    public void TakeDamage(DamageInfo info)
    {
        var ctx = new CombatContext(
            attackerAttackPower: info.Amount,
            defenderDefense: Stats.Get(StatType.Defense),
            defenderCurrentHP: Resources.GetCurrent(ResourceType.HP),
            defenderMaxHP: Resources.GetMax(ResourceType.HP),
            attackerTags: 0UL,
            defenderTags: _tags.State.Value);
        var hit = new HitContext(
            baseDamage: Mathf.Max(0f, info.Amount),
            isCritical: false,
            criticalMultiplier: 1.5f,
            hitPoint: info.HitPoint);
        var result = DamagePipeline.Compute(in ctx, in hit);
        ReceiveDamage(in result, in ctx);
    }

    public void ReceiveDamage(in DamageResult result, in CombatContext ctx)
    {
        TakeDamage(result.FinalDamage, this);
    }
}

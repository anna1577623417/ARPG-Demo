using UnityEngine;

/// <summary>
/// 214.4 — 训练用敌人实体：继承 <see cref="Entity"/>，可被 CombatObject Overlap 命中。
/// 合并 TestDummy 的受击/回血/日志能力；场景需挂 CapsuleCollider（Trigger 可选）。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CapsuleCollider))]
public sealed class Enemy_Training : Entity, IEntity, IImpulseReceiver, ITagOwner, IDamageable, IEffectReceiver
{
    [SerializeField] TestDummyConfigSO config;
    [SerializeField] bool logDamageToConsole = true;
    [SerializeField, Min(0f)] float impulseDamping = 18f;

    GameplayTagContainer _tags;
    float _lastDamageTime = -999f;
    float _totalDamageReceived;
    Vector3 _impulseVelocity;
    Vector3 _impulseStartPosition;
    int _impulseRevision;
    bool _impulseMoveLogged;

    public ref GameplayTagContainer Tags => ref _tags;
    GameplayTagContainer ITagOwner.Tags => _tags;
    public bool IsAlive => !IsDead;

    Transform IEntity.Transform => transform;
    IReadOnlyStatSet IEntity.Stats => Stats;
    IResourcePool IEntity.Resources => Resources;

    IBuffStack IEffectReceiver.BuffStack => Buffs;
    IReadOnlyStatSet IEffectReceiver.Stats => Stats;
    IResourcePool IEffectReceiver.Resources => Resources;

    public ImpulseApplyResult TryApplyImpulse(in ImpulseRequest request)
    {
        if (IsDead)
        {
            return ImpulseApplyResult.RejectedDead;
        }

        var planarDirection = request.Direction;
        planarDirection.y = 0f;
        if (request.Force > 0.01f && planarDirection.sqrMagnitude > 0.0001f)
        {
            _impulseVelocity = planarDirection.normalized * request.Force;
            _impulseStartPosition = transform.position;
            _impulseRevision++;
            _impulseMoveLogged = false;
            if (GameMainDebugSettings.CombatHit)
            {
                Debug.Log(
                    $"[Enemy_Training] channel=Impulse result=Accepted revision={_impulseRevision} " +
                    $"pos={transform.position} velocity={_impulseVelocity}");
            }
            return ImpulseApplyResult.Applied;
        }

        if (request.LaunchUpSpeed > 0.01f)
        {
            return ImpulseApplyResult.RejectedNoMotor;
        }

        return ImpulseApplyResult.IgnoredByProfile;
    }

    protected override void Awake()
    {
        unitKind = UnitKind.Monster;

        if (teamId == 0)
        {
            teamId = 1;
        }

        base.Awake();

        var faction = config != null ? config.faction : FactionTag.Enemy;
        _tags.Faction.Set((ulong)faction);

        if (config != null && config.baseStats != null)
        {
            for (var i = 0; i < config.baseStats.Length; i++)
            {
                var entry = config.baseStats[i];
                Stats.SetBase(entry.Type, entry.BaseValue);
            }

            var maxHp = Mathf.Max(1f, Stats.Get(StatType.MaxHealth));
            Resources.SetCurrent(ResourceType.HP, maxHp);
        }
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();

        if (IsDead)
        {
            _impulseVelocity = Vector3.zero;
        }
        else if (_impulseVelocity.sqrMagnitude > 0.0001f)
        {
            transform.position += _impulseVelocity * Time.deltaTime;
            if (!_impulseMoveLogged
                && (transform.position - _impulseStartPosition).sqrMagnitude > 0.0001f
                && GameMainDebugSettings.CombatHit)
            {
                _impulseMoveLogged = true;
                Debug.Log(
                    $"[Enemy_Training] channel=ImpulseMove result=Applied revision={_impulseRevision} " +
                    $"delta={transform.position - _impulseStartPosition} pos={transform.position}");
            }
            _impulseVelocity = Vector3.MoveTowards(
                _impulseVelocity,
                Vector3.zero,
                impulseDamping * Time.deltaTime);
        }

        if (config != null
            && config.autoRegenerate
            && !IsDead
            && Time.time - _lastDamageTime > config.regenDelay)
        {
            Resources.Refill(ResourceType.HP, config.regenRate * Time.deltaTime, out _);
        }
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
        _lastDamageTime = Time.time;
        _totalDamageReceived += result.FinalDamage;

        if (logDamageToConsole || (config != null && config.logDamageToConsole))
        {
            Debug.Log(
                $"[Enemy_Training] damage={result.FinalDamage:F1} hp={Resources.GetCurrent(ResourceType.HP):F1} " +
                $"total={_totalDamageReceived:F1}");
        }
    }

    [ContextMenu("Reset Training Enemy")]
    public void ResetTraining()
    {
        RestoreHealthToFull();
        _totalDamageReceived = 0f;
        _lastDamageTime = -999f;
        _impulseVelocity = Vector3.zero;
        _impulseStartPosition = transform.position;
        _impulseMoveLogged = true;
        Debug.Log("[Enemy_Training] reset");
    }
}

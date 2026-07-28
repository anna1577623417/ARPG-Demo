using UnityEngine;

/// <summary>
/// 217.2 L5 — 训练用小兵实体（UnitKind.Minion），用于 TargetProfile 例 C 验收。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CapsuleCollider))]
public sealed class Minion_Training : Entity, ITagOwner, IDamageable, IEffectReceiver
{
    [SerializeField] TestDummyConfigSO config;
    [SerializeField] bool logDamageToConsole = true;

    GameplayTagContainer _tags;
    float _lastDamageTime = -999f;
    float _totalDamageReceived;

    public ref GameplayTagContainer Tags => ref _tags;
    GameplayTagContainer ITagOwner.Tags => _tags;
    public bool IsAlive => !IsDead;

    IBuffStack IEffectReceiver.BuffStack => Buffs;
    IReadOnlyStatSet IEffectReceiver.Stats => Stats;
    IResourcePool IEffectReceiver.Resources => Resources;

    protected override void Awake()
    {
        unitKind = UnitKind.Minion;

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
                $"[Minion_Training] damage={result.FinalDamage:F1} hp={Resources.GetCurrent(ResourceType.HP):F1} " +
                $"total={_totalDamageReceived:F1}");
        }
    }

    [ContextMenu("Reset Training Minion")]
    public void ResetTraining()
    {
        RestoreHealthToFull();
        _totalDamageReceived = 0f;
        _lastDamageTime = -999f;
        Debug.Log("[Minion_Training] reset");
    }
}

using UnityEngine;

/// <summary>
/// 训练假人：无 AI，但完整实现战斗受体接口，专用于管线验证。
/// </summary>
[System.Obsolete("CombatObject 请使用 Enemy_Training（Entity 子类）。本组件保留仅供旧场景过渡。")]
public sealed class TestDummy : MonoBehaviour, IEntity, IDamageable, IEffectReceiver
{
    [SerializeField] TestDummyConfigSO config;

    StatSet _stats;
    ResourcePool _resources;
    BuffStack _buffStack;
    GameplayTagContainer _tags;

    float _lastDamageTime = -999f;
    float _totalDamageReceived;

    public Transform Transform => transform;
    public IReadOnlyStatSet Stats => _stats;
    public IResourcePool Resources => _resources;
    public GameplayTagContainer Tags => _tags;
    public bool IsAlive => _resources != null && _resources.GetCurrent(ResourceType.HP) > 0f;
    public IBuffStack BuffStack => _buffStack;

    void Awake()
    {
        InitializeFromConfig();
    }

    void Update()
    {
        _buffStack?.Tick(Time.deltaTime);

        if (config != null
            && config.autoRegenerate
            && IsAlive
            && Time.time - _lastDamageTime > config.regenDelay)
        {
            _resources.Refill(ResourceType.HP, config.regenRate * Time.deltaTime, out _);
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
            defenderDefense: _stats.Get(StatType.Defense),
            defenderCurrentHP: _resources.GetCurrent(ResourceType.HP),
            defenderMaxHP: _resources.GetMax(ResourceType.HP),
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
        _resources.Drain(ResourceType.HP, result.FinalDamage, out _);
        _lastDamageTime = Time.time;
        _totalDamageReceived += result.FinalDamage;

        GlobalEventBus.Publish(new DummyDamagedEvent
        {
            Dummy = this,
            Result = result,
            RemainingHP = _resources.GetCurrent(ResourceType.HP)
        });

        if (config != null && config.logDamageToConsole)
        {
            Debug.Log($"[Dummy] damage={result.FinalDamage:F1}, crit={result.IsCritical}, hp={_resources.GetCurrent(ResourceType.HP):F1}");
        }

        if (!IsAlive)
        {
            _tags.Status.Add((ulong)StatusTag.Stagger);
            GlobalEventBus.Publish(new DummyDeathEvent
            {
                Dummy = this,
                TotalDamage = _totalDamageReceived
            });
        }
    }

    [ContextMenu("Reset Dummy")]
    public void ResetDummy()
    {
        InitializeFromConfig();
        _totalDamageReceived = 0f;
        Debug.Log("[Dummy] reset");
    }

    void InitializeFromConfig()
    {
        _stats = new StatSet();
        _resources = new ResourcePool();
        _buffStack = new BuffStack(_stats);
        _tags = default;

        if (config != null && config.baseStats != null)
        {
            for (var i = 0; i < config.baseStats.Length; i++)
            {
                _stats.SetBase(config.baseStats[i].Type, config.baseStats[i].BaseValue);
            }
        }

        var maxHp = Mathf.Max(1f, _stats.Get(StatType.MaxHealth));
        _resources.RegisterSlot(ResourceType.HP, () => _stats.Get(StatType.MaxHealth), maxHp);

        var faction = config != null ? config.faction : FactionTag.Enemy;
        _tags.Faction.Set((ulong)faction);
    }
}

public struct DummyDamagedEvent : IGameEvent
{
    public TestDummy Dummy;
    public DamageResult Result;
    public float RemainingHP;
}

public struct DummyDeathEvent : IGameEvent
{
    public TestDummy Dummy;
    public float TotalDamage;
}

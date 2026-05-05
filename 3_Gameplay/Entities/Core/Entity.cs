using System;
using UnityEngine;

/// <summary>
/// 实体基类（玩家、怪物、NPC 共用）。
/// 面向 3D 战斗 + 动画系统设计，预留属性系统和 Buff 系统扩展点。
///
/// 字段设计原则：
/// - 只放所有实体共有的基础属性，角色特有的放子类（如 Player）。
/// - Animator 在基类持有，因为所有战斗实体都需要动画驱动。
/// - LocalEventBus 实例化在基类，保证每个实体都有独立的事件域。
/// </summary>
public abstract class Entity : MonoBehaviour
{
    // ─── 身份标识 ───

    [Header("Identity")]
    [SerializeField] protected string entityId = "entity";
    [SerializeField] protected string displayName = "Entity";
    [SerializeField] protected int teamId = 0;

    // ─── 基础属性（无 Stats 蓝图时使用；有蓝图时由 RuntimeEntityStats 覆盖同步） ───

    [Header("Stats Blueprint")]
    [Tooltip("可选。指定后 MaxHealth / 走跑速 / 旋转等以资产为准，且运行时不改资产。")]
    [SerializeField] protected EntityStatsSO statsBlueprint;

    [Header("Core Attributes (legacy / fallback)")]
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] protected float baseMoveSpeed = 5f;
    [SerializeField] protected float baseRotationSpeed = 540f;

    // ─── 战斗属性（为属性系统和 Buff 系统预留） ───

    [Header("Combat Attributes")]
    [SerializeField] protected float baseAttackPower = 10f;
    [SerializeField] protected float baseDefense = 0f;

    // ─── 事件配置 ───

    [Header("EventBus")]
    [SerializeField] protected bool publishToGlobalBus = false;

    // ─── 运行时状态 ───

    private readonly RuntimeEntityStats m_runtimeStats = new RuntimeEntityStats();
    private readonly StatSet m_statSet = new StatSet();
    private readonly ResourcePool m_resources = new ResourcePool();
    private BuffStack m_buffStack;
    private Vector3 m_lastPosition;
    private Vector3 m_moveDirection = Vector3.forward;
    private Vector3 m_velocity;

    // ─── 组件缓存 ───

    private Animator m_animator;

    // ─── 公开属性 ───

    /// <summary>实体局部事件总线，供状态机和组件内部解耦通信。</summary>
    public LocalEventBus EventBus { get; } = new LocalEventBus();

    /// <summary>Animator 组件（懒加载，首次访问时 GetComponent）。</summary>
    public Animator Animator
    {
        get
        {
            if (m_animator == null)
            {
                m_animator = GetComponentInChildren<Animator>();
            }
            return m_animator;
        }
    }

    public string EntityId => entityId;
    public string DisplayName => displayName;
    public int TeamId => teamId;

    public float MaxHealth => m_runtimeStats.MaxHealth;
    [Obsolete("Use Resources.GetCurrent(ResourceType.HP). Removed in Phase 4.")]
    public float Health => m_resources.GetCurrent(ResourceType.HP);
    public float HealthNormalized => m_resources.GetNormalized(ResourceType.HP);
    public bool IsDead => m_resources.IsEmpty(ResourceType.HP);

    /// <summary>兼容旧逻辑：等价于当前奔跑速度上限。</summary>
    public float BaseMoveSpeed => m_runtimeStats.RunSpeed;

    public float BaseRotationSpeed => m_runtimeStats.RotationSpeed;

    public RuntimeEntityStats RuntimeStats => m_runtimeStats;
    public IStatSet Stats => m_statSet;
    public IResourcePool Resources => m_resources;
    public IBuffStack Buffs => m_buffStack;
    public float BaseAttackPower => m_runtimeStats.AttackPower;
    public float BaseDefense => m_runtimeStats.Defense;

    public Vector3 Position => transform.position;
    public Vector3 LastPosition => m_lastPosition;
    public Vector3 Velocity => m_velocity;
    public float Speed => m_velocity.magnitude;
    public Vector3 MoveDirection => m_moveDirection;
    public Vector3 Forward => transform.forward;

    // ─── 生命周期 ───

    protected virtual void Awake()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        baseMoveSpeed = Mathf.Max(0f, baseMoveSpeed);
        baseRotationSpeed = Mathf.Max(0f, baseRotationSpeed);

        m_runtimeStats.Bind(m_statSet);

        if (statsBlueprint != null)
        {
            for (var i = 0; i < statsBlueprint.BaseStats.Count; i++)
            {
                var entry = statsBlueprint.BaseStats[i];
                var value = entry.BaseValue;
                if (entry.Type == StatType.MaxHealth)
                {
                    value = Mathf.Max(1f, value);
                }
                else if (entry.Type == StatType.WalkSpeed
                         || entry.Type == StatType.RunSpeed
                         || entry.Type == StatType.RotationSpeed)
                {
                    value = Mathf.Max(0f, value);
                }

                m_statSet.SetBase(entry.Type, value);
            }
        }
        else
        {
            var walk = baseMoveSpeed * 0.5f;
            var run = baseMoveSpeed;
            m_statSet.SetBase(StatType.MaxHealth, maxHealth);
            m_statSet.SetBase(StatType.WalkSpeed, walk);
            m_statSet.SetBase(StatType.RunSpeed, run);
            m_statSet.SetBase(StatType.RotationSpeed, baseRotationSpeed);
            m_statSet.SetBase(StatType.AttackPower, baseAttackPower);
            m_statSet.SetBase(StatType.Defense, baseDefense);
        }

        m_resources.RegisterSlot(
            ResourceType.HP,
            maxProvider: () => m_statSet.Get(StatType.MaxHealth),
            initialCurrent: MaxHealth);
        m_buffStack = new BuffStack(m_statSet);
        m_lastPosition = transform.position;
    }

    protected virtual void OnEnable()
    {
        m_resources.OnCurrentChanged += OnResourceCurrentChanged;
        if (m_buffStack != null)
        {
            m_buffStack.OnPeriodElapsed += OnBuffPeriodElapsed;
        }
    }

    protected virtual void OnDisable()
    {
        if (m_buffStack != null)
        {
            m_buffStack.OnPeriodElapsed -= OnBuffPeriodElapsed;
        }

        m_resources.OnCurrentChanged -= OnResourceCurrentChanged;
    }

    protected virtual void LateUpdate()
    {
        var currentPosition = transform.position;
        m_velocity = (currentPosition - m_lastPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
        m_lastPosition = currentPosition;
        m_buffStack?.Tick(Time.deltaTime);
    }

    // ─── 移动与朝向 ───

    public virtual void SetMoveDirection(Vector3 worldDirection)
    {
        if (worldDirection.sqrMagnitude <= 0.0001f) return;
        m_moveDirection = worldDirection.normalized;
    }

    public virtual void LookAtDirection(Vector3 worldDirection, bool immediate = false)
    {
        if (worldDirection.sqrMagnitude <= 0.0001f) return;

        var target = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
        var rotSpeed = BaseRotationSpeed;
        if (immediate || rotSpeed <= 0f)
        {
            transform.rotation = target;
            return;
        }

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, target, rotSpeed * Time.deltaTime);
    }

    // ─── 生命值 ───

    public virtual void RestoreHealthToFull()
    {
        var oldValue = Health;
        m_resources.SetCurrent(ResourceType.HP, m_resources.GetMax(ResourceType.HP));
        PublishHealthChanged(oldValue, Health, "RestoreFull");
    }

    public virtual void TakeDamage(float amount, UnityEngine.Object damageSource = null)
    {
        if (IsDead || amount <= 0f) return;

        var oldValue = Health;
        m_resources.Drain(ResourceType.HP, amount, out _);
        PublishHealthChanged(oldValue, Health, "Damage");

        if (IsDead)
        {
            PublishEvent(new EntityDiedEvent(GetInstanceID(), name,
                damageSource ? damageSource.name : "Unknown"));
        }
    }

    public virtual void Heal(float amount, UnityEngine.Object healSource = null)
    {
        if (IsDead || amount <= 0f) return;

        var oldValue = Health;
        m_resources.Refill(ResourceType.HP, amount, out _);
        PublishHealthChanged(oldValue, Health, "Heal");
    }

    // ─── 事件发布 ───

    /// <summary>发布实体事件到 LocalBus，按开关可选转发到 GlobalBus。</summary>
    public virtual void PublishEvent<TEvent>(in TEvent evt) where TEvent : struct, IGameEvent
    {
        EventBus.Publish(evt);
        if (publishToGlobalBus)
        {
            GlobalEventBus.Publish(evt);
        }
    }

    private void PublishHealthChanged(float oldValue, float newValue, string reason)
    {
        PublishEvent(new EntityHealthChangedEvent(
            GetInstanceID(), name, oldValue, newValue, reason));
    }

    void OnResourceCurrentChanged(ResourceType type, float oldValue, float newValue)
    {
        PublishEvent(new EntityResourceChangedEvent(
            GetInstanceID(),
            name,
            type,
            oldValue,
            newValue));
    }

    protected virtual void OnBuffPeriodElapsed(BuffInstance instance)
    {
        var def = instance.Definition;
        if (def == null || !def.ApplyPeriodicResourceDelta)
        {
            return;
        }

        var amount = def.PeriodicAmount;
        var resourceType = def.PeriodicResource;
        if (resourceType == ResourceType.HP)
        {
            var oldHp = Health;
            if (amount > 0f)
            {
                m_resources.Drain(ResourceType.HP, amount, out _);
                PublishHealthChanged(oldHp, Health, "BuffPeriodicDrain");
                if (IsDead)
                {
                    PublishEvent(new EntityDiedEvent(GetInstanceID(), name, def.name));
                }
            }
            else if (amount < 0f)
            {
                m_resources.Refill(ResourceType.HP, -amount, out _);
                PublishHealthChanged(oldHp, Health, "BuffPeriodicRefill");
            }
            return;
        }

        if (amount > 0f)
        {
            m_resources.Drain(resourceType, amount, out _);
        }
        else if (amount < 0f)
        {
            m_resources.Refill(resourceType, -amount, out _);
        }
    }

}

/// <summary>
/// 泛型实体基类，与状态机系统泛型约束保持一致（CRTP 模式）。
/// </summary>
public abstract class Entity<T> : Entity where T : Entity<T> { }

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

    // ─── 基础属性（未来接入属性系统后，这些作为 Base 值，最终值由属性系统计算） ───

    [Header("Core Attributes")]
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

    private float m_health;
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

    public float MaxHealth => maxHealth;
    public float Health => m_health;
    public float HealthNormalized => maxHealth <= 0f ? 0f : Mathf.Clamp01(m_health / maxHealth);
    public bool IsDead => m_health <= 0f;

    public float BaseMoveSpeed => baseMoveSpeed;
    public float BaseRotationSpeed => baseRotationSpeed;
    public float BaseAttackPower => baseAttackPower;
    public float BaseDefense => baseDefense;

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

        m_health = maxHealth;
        m_lastPosition = transform.position;
    }

    protected virtual void LateUpdate()
    {
        var currentPosition = transform.position;
        m_velocity = (currentPosition - m_lastPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
        m_lastPosition = currentPosition;
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
        if (immediate || baseRotationSpeed <= 0f)
        {
            transform.rotation = target;
            return;
        }

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, target, baseRotationSpeed * Time.deltaTime);
    }

    // ─── 生命值 ───

    public virtual void RestoreHealthToFull()
    {
        var oldValue = m_health;
        m_health = maxHealth;
        PublishHealthChanged(oldValue, m_health, "RestoreFull");
    }

    public virtual void TakeDamage(float amount, Object source = null)
    {
        if (IsDead || amount <= 0f) return;

        var oldValue = m_health;
        m_health = Mathf.Max(0f, m_health - amount);
        PublishHealthChanged(oldValue, m_health, "Damage");

        if (m_health <= 0f)
        {
            PublishEvent(new EntityDiedEvent(GetInstanceID(), name,
                source ? source.name : "Unknown"));
        }
    }

    public virtual void Heal(float amount, Object source = null)
    {
        if (IsDead || amount <= 0f) return;

        var oldValue = m_health;
        m_health = Mathf.Min(maxHealth, m_health + amount);
        PublishHealthChanged(oldValue, m_health, "Heal");
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
}

/// <summary>
/// 泛型实体基类，与状态机系统泛型约束保持一致（CRTP 模式）。
/// </summary>
public abstract class Entity<T> : Entity where T : Entity<T> { }

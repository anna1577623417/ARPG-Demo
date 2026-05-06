using UnityEngine;

/// <summary>
/// 技能用直线飞行体（无Rigidbody）；由 <see cref="SpawnProjectileTask"/> 装配速度/生命周期。
/// </summary>
public sealed class SkillStraightProjectile : MonoBehaviour
{
    [SerializeField] LayerMask damageLayers;

    SkillContext m_ctx;
    Vector3 m_dir;
    float m_speed;
    float m_expireAt;
    bool m_spawnedHit;

    public void Configure(in SkillStraightProjectileSpawn spawn)
    {
        m_ctx = spawn.Context;
        m_dir = spawn.Direction.sqrMagnitude > 1e-6f ? spawn.Direction.normalized : Vector3.forward;
        m_speed = spawn.Speed;
        m_expireAt = Time.time + spawn.LifetimeSeconds;
        transform.rotation = Quaternion.LookRotation(m_dir, Vector3.up);

        var col = gameObject.GetComponent<SphereCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<SphereCollider>();
        }

        col.isTrigger = true;
        col.radius = spawn.CollisionRadius;
    }

    void Update()
    {
        if (Time.time >= m_expireAt)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += m_dir * (m_speed * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (m_ctx == null || m_spawnedHit)
        {
            return;
        }

        if (((1 << other.gameObject.layer) & damageLayers.value) == 0)
        {
            return;
        }

        var dmg = other.GetComponentInParent<IDamageable>();
        if (dmg == null || m_ctx.Self != null && other.GetComponentInParent<Entity>() == m_ctx.Self)
        {
            return;
        }

        m_spawnedHit = true;
        var mul = Mathf.Max(0f, m_ctx.Runtime != null
            ? m_ctx.Runtime.GetScaledDamage(m_ctx.ResolvedSkillSheet)
            : 0f);
        mul *= m_ctx.FinalModifiers.DamageScale;

        var info = new DamageInfo
        {
            Amount = mul,
            HitPoint = other.bounds.center,
            Force = Vector3.zero,
            Source = m_ctx.Self != null ? m_ctx.Self.gameObject : gameObject,
        };
        dmg.TakeDamage(info);
        Destroy(gameObject);
    }
}

public struct SkillStraightProjectileSpawn
{
    public SkillContext Context;
    public Vector3 Direction;
    public float Speed;
    public float LifetimeSeconds;
    public float CollisionRadius;
}

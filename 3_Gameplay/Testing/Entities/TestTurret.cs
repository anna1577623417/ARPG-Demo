using UnityEngine;

/// <summary>
/// 测试炮台：最小 AI 发起者，验证感知->CD->投射物链路。
/// </summary>
public sealed class TestTurret : MonoBehaviour, IEntity
{
    [Header("Perception | 感知")]
    [Tooltip("detectionRange（探测范围）：增大=更远距离锁定目标。")]
    [SerializeField] float detectionRange = 15f;

    [Tooltip("rotateSpeed（旋转速度）：增大=瞄准更快。")]
    [SerializeField] float rotateSpeed = 5f;

    [Tooltip("targetLayerMask（目标层）：只扫描这些层。")]
    [SerializeField] LayerMask targetLayerMask;

    [Header("Attack | 攻击")]
    [Tooltip("shootSkill（发射技能）：仅用于复用 SkillRuntime CD 规则。")]
    [SerializeField] SkillDataSO shootSkill;

    [Tooltip("projectilePrefab（投射物预制体）：必须带 ProjectileController。")]
    [SerializeField] ProjectileController projectilePrefab;

    [Tooltip("projectileData（投射物数据）：速度/半径/穿透等参数。")]
    [SerializeField] ProjectileDataSO projectileData;

    [Tooltip("muzzle（炮口点）：为空时使用炮台自身位置。")]
    [SerializeField] Transform muzzle;

    [Header("Stats | 属性")]
    [SerializeField] TestDummyConfigSO statsConfig;

    StatSet _stats;
    ResourcePool _resources;
    GameplayTagContainer _tags;
    SkillRuntime _skillRuntime;
    IEntity _currentTarget;
    readonly Collider[] _scanBuffer = new Collider[16];

    public Transform Transform => transform;
    public IReadOnlyStatSet Stats => _stats;
    public IResourcePool Resources => _resources;
    public GameplayTagContainer Tags => _tags;
    public bool IsAlive => true;

    public bool HasTag(GameplayTagMask mask)
    {
        var bits = mask.Value;
        return _tags.State.HasAll(bits)
               || _tags.Status.HasAll(bits)
               || _tags.Ability.HasAll(bits)
               || _tags.Mechanic.HasAll(bits)
               || _tags.Faction.HasAll(bits);
    }

    void Awake()
    {
        InitStats();
        _skillRuntime = new SkillRuntime(shootSkill) { Level = 1 };
        _tags.Faction.Set((ulong)FactionTag.Enemy);
    }

    void Update()
    {
        _skillRuntime?.TickCooldown(Time.deltaTime, _stats);
        _currentTarget = ScanForTarget();
        if (_currentTarget == null)
        {
            return;
        }

        RotateTowards(_currentTarget.Transform.position);
        if (_skillRuntime != null && _skillRuntime.IsCooldownReady())
        {
            Fire();
        }
    }

    IEntity ScanForTarget()
    {
        var count = Physics.OverlapSphereNonAlloc(
            transform.position,
            detectionRange,
            _scanBuffer,
            targetLayerMask,
            QueryTriggerInteraction.Collide);

        IEntity nearest = null;
        var nearestDist = float.MaxValue;
        for (var i = 0; i < count; i++)
        {
            var entity = _scanBuffer[i].GetComponentInParent<IEntity>();
            if (entity == null || !entity.IsAlive)
            {
                continue;
            }

            if (!entity.Tags.Faction.HasAny((ulong)FactionTag.Player))
            {
                continue;
            }

            var dist = Vector3.SqrMagnitude(transform.position - entity.Transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = entity;
            }
        }

        return nearest;
    }

    void RotateTowards(Vector3 targetPos)
    {
        var dir = targetPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
        {
            return;
        }

        var targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
    }

    void Fire()
    {
        if (projectilePrefab == null || projectileData == null)
        {
            return;
        }

        var spawnPos = muzzle != null ? muzzle.position : transform.position + transform.forward;
        var projectile = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        projectile.Init(this, transform.forward, projectileData);
        _skillRuntime.StartCooldown(_stats);
    }

    void InitStats()
    {
        _stats = new StatSet();
        if (statsConfig != null && statsConfig.baseStats != null)
        {
            for (var i = 0; i < statsConfig.baseStats.Length; i++)
            {
                _stats.SetBase(statsConfig.baseStats[i].Type, statsConfig.baseStats[i].BaseValue);
            }
        }

        _resources = new ResourcePool();
        _resources.RegisterSlot(ResourceType.HP, () => _stats.Get(StatType.MaxHealth), Mathf.Max(1f, _stats.Get(StatType.MaxHealth)));
    }
}

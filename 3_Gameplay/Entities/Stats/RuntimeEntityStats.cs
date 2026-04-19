using UnityEngine;

/// <summary>
/// 运行时属性快照（可在此后叠加 Buff/Debuff 修饰，勿写回 ScriptableObject）。
/// </summary>
public sealed class RuntimeEntityStats
{
    float _maxHealth;
    float _walkSpeed;
    float _runSpeed;
    float _rotationSpeed;
    float _attackPower;
    float _defense;
    bool _initialized;

    public bool IsInitialized => _initialized;

    public float MaxHealth => _maxHealth;
    public float WalkSpeed => _walkSpeed;
    public float RunSpeed => _runSpeed;
    public float RotationSpeed => _rotationSpeed;
    public float AttackPower => _attackPower;
    public float Defense => _defense;

    public void Initialize(EntityStatsSO blueprint)
    {
        if (blueprint == null)
        {
            return;
        }

        _maxHealth = Mathf.Max(1f, blueprint.MaxHealth);
        _walkSpeed = Mathf.Max(0f, blueprint.BaseWalkSpeed);
        _runSpeed = Mathf.Max(0f, blueprint.BaseRunSpeed);
        _rotationSpeed = Mathf.Max(0f, blueprint.BaseRotationSpeed);
        _attackPower = blueprint.BaseAttackPower;
        _defense = blueprint.BaseDefense;
        _initialized = true;
    }

    /// <summary>无 SO 时由 Entity 序列化字段构造；行走取单速的一部分以区分走/跑。</summary>
    public void InitializeLegacy(
        float maxHealth,
        float walkSpeed,
        float runSpeed,
        float rotationSpeed,
        float attackPower,
        float defense)
    {
        _maxHealth = Mathf.Max(1f, maxHealth);
        _walkSpeed = Mathf.Max(0f, walkSpeed);
        _runSpeed = Mathf.Max(0f, runSpeed);
        _rotationSpeed = Mathf.Max(0f, rotationSpeed);
        _attackPower = attackPower;
        _defense = defense;
        _initialized = true;
    }
}

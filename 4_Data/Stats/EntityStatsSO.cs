using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;

/// <summary>
/// 实体基础属性蓝图：统一以 <see cref="StatBaseEntry"/> 列表驱动 StatSet 初始化。
/// </summary>
[CreateAssetMenu(fileName = "EntityStats", menuName = "GameMain/Stats/Entity Stats")]
public class EntityStatsSO : ScriptableObject
{
    [SerializeField] protected List<StatBaseEntry> baseStats = new List<StatBaseEntry>
    {
        new StatBaseEntry { Type = StatType.MaxHealth, BaseValue = 100f },
        new StatBaseEntry { Type = StatType.WalkSpeed, BaseValue = 2.5f },
        new StatBaseEntry { Type = StatType.RunSpeed, BaseValue = 5.5f },
        new StatBaseEntry { Type = StatType.RotationSpeed, BaseValue = 540f },
        new StatBaseEntry { Type = StatType.AttackPower, BaseValue = 10f },
        new StatBaseEntry { Type = StatType.Defense, BaseValue = 0f },
    };

    public IReadOnlyList<StatBaseEntry> BaseStats => baseStats;

    public bool TryGetBase(StatType type, out float value)
    {
        for (var i = 0; i < baseStats.Count; i++)
        {
            if (baseStats[i].Type != type)
            {
                continue;
            }

            value = baseStats[i].BaseValue;
            return true;
        }

        value = 0f;
        return false;
    }

    protected void SetOrAddBaseStat(StatType type, float baseValue)
    {
        if (baseStats == null)
        {
            baseStats = new List<StatBaseEntry>();
        }

        for (var i = 0; i < baseStats.Count; i++)
        {
            if (baseStats[i].Type != type)
            {
                continue;
            }

            baseStats[i] = new StatBaseEntry { Type = type, BaseValue = baseValue };
            return;
        }

        baseStats.Add(new StatBaseEntry { Type = type, BaseValue = baseValue });
    }

    [Header("Legacy Migration (hidden)")]
    [FormerlySerializedAs("MaxHealth")]
    [SerializeField, HideInInspector] float maxHealthLegacy = 100f;
    [FormerlySerializedAs("BaseWalkSpeed")]
    [SerializeField, HideInInspector] float baseWalkSpeedLegacy = 2.5f;
    [FormerlySerializedAs("BaseRunSpeed")]
    [SerializeField, HideInInspector] float baseRunSpeedLegacy = 5.5f;
    [FormerlySerializedAs("BaseRotationSpeed")]
    [SerializeField, HideInInspector] float baseRotationSpeedLegacy = 540f;
    [FormerlySerializedAs("BaseAttackPower")]
    [SerializeField, HideInInspector] float baseAttackPowerLegacy = 10f;
    [FormerlySerializedAs("BaseDefense")]
    [SerializeField, HideInInspector] float baseDefenseLegacy;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (baseStats != null && baseStats.Count > 0)
        {
            return;
        }

        baseStats = new List<StatBaseEntry>
        {
            new StatBaseEntry { Type = StatType.MaxHealth, BaseValue = Mathf.Max(1f, maxHealthLegacy) },
            new StatBaseEntry { Type = StatType.WalkSpeed, BaseValue = Mathf.Max(0f, baseWalkSpeedLegacy) },
            new StatBaseEntry { Type = StatType.RunSpeed, BaseValue = Mathf.Max(0f, baseRunSpeedLegacy) },
            new StatBaseEntry { Type = StatType.RotationSpeed, BaseValue = Mathf.Max(0f, baseRotationSpeedLegacy) },
            new StatBaseEntry { Type = StatType.AttackPower, BaseValue = baseAttackPowerLegacy },
            new StatBaseEntry { Type = StatType.Defense, BaseValue = baseDefenseLegacy },
        };
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}

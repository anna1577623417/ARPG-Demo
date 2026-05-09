using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 玩家专用属性蓝图（在 <see cref="EntityStatsSO"/> 基础上扩展资源与 ARPG 常用项）。
/// 体力上限 / 法力上限仅通过 <see cref="EntityStatsSO.BaseStats"/> 列表编辑，不使用容器外重复字段。
/// </summary>
[CreateAssetMenu(fileName = "PlayerStats", menuName = "GameMain/Stats/Player Stats")]
public class PlayerStatsSO : EntityStatsSO
{
    [FormerlySerializedAs("MaxStamina")]
    [SerializeField, HideInInspector]
    float _migrateStandaloneMaxStamina;

    /// <summary>旧版在 SO 根上序列化的 Max Stamina：在玩家 Awake 时灌入 <see cref="IStatSet"/> 并清掉内存中的遗留值（若已在编辑器中迁移到 Base Stats 则此字段为 0）。</summary>
    public void ApplyLegacyStandaloneMaxStaminaToStats(IStatSet stats)
    {
        if (stats == null || _migrateStandaloneMaxStamina < 1f)
        {
            return;
        }

        stats.SetBase(StatType.MaxStamina, Mathf.Max(1f, _migrateStandaloneMaxStamina));
        _migrateStandaloneMaxStamina = 0f;
    }

    [Header("Optional Player BaseStats Preset")]
    [SerializeField] bool usePlayerBaseStatsPreset;
    [SerializeField, Min(1f)] float presetMaxHealth = 100f;
    [SerializeField, Min(0f)] float presetWalkSpeed = 2.4f;
    [SerializeField, Min(0f)] float presetRunSpeed = 5.5f;
    [SerializeField, Min(0f)] float presetRotationSpeed = 540f;
    [SerializeField] float presetAttackPower = 10f;
    [SerializeField] float presetDefense = 0f;
    [SerializeField, Min(1f)] float presetMaxStamina = 100f;
    [SerializeField, Min(0f)] float presetMaxMana = 100f;

#if UNITY_EDITOR
    void OnValidate()
    {
        MigrateStandaloneMaxStaminaOnce();

        if (!usePlayerBaseStatsPreset)
        {
            return;
        }

        SetOrAddBaseStat(StatType.MaxHealth, Mathf.Max(1f, presetMaxHealth));
        SetOrAddBaseStat(StatType.WalkSpeed, Mathf.Max(0f, presetWalkSpeed));
        SetOrAddBaseStat(StatType.RunSpeed, Mathf.Max(0f, presetRunSpeed));
        SetOrAddBaseStat(StatType.RotationSpeed, Mathf.Max(0f, presetRotationSpeed));
        SetOrAddBaseStat(StatType.AttackPower, presetAttackPower);
        SetOrAddBaseStat(StatType.Defense, presetDefense);
        SetOrAddBaseStat(StatType.MaxStamina, Mathf.Max(1f, presetMaxStamina));
        SetOrAddBaseStat(StatType.MaxMana, Mathf.Max(0f, presetMaxMana));
        UnityEditor.EditorUtility.SetDirty(this);
    }

    void MigrateStandaloneMaxStaminaOnce()
    {
        if (_migrateStandaloneMaxStamina < 1f)
        {
            return;
        }

        SetOrAddBaseStat(StatType.MaxStamina, Mathf.Max(1f, _migrateStandaloneMaxStamina));
        _migrateStandaloneMaxStamina = 0f;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}

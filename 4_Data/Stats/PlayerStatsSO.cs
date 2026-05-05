using UnityEngine;

/// <summary>
/// 玩家专用属性蓝图（在 <see cref="EntityStatsSO"/> 基础上扩展资源与 ARPG 常用项）。
/// </summary>
[CreateAssetMenu(fileName = "PlayerStats", menuName = "GameMain/Stats/Player Stats")]
public class PlayerStatsSO : EntityStatsSO
{
    [Min(1f)] public float MaxStamina = 100f;

    [Header("Optional Player BaseStats Preset")]
    [SerializeField] bool usePlayerBaseStatsPreset;
    [SerializeField, Min(1f)] float presetMaxHealth = 100f;
    [SerializeField, Min(0f)] float presetWalkSpeed = 2.4f;
    [SerializeField, Min(0f)] float presetRunSpeed = 5.5f;
    [SerializeField, Min(0f)] float presetRotationSpeed = 540f;
    [SerializeField] float presetAttackPower = 10f;
    [SerializeField] float presetDefense = 0f;

#if UNITY_EDITOR
    void OnValidate()
    {
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
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}

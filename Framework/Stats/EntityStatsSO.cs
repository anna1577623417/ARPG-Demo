using UnityEngine;

/// <summary>
/// 实体属性蓝图（只读模板）。运行时禁止修改实例上的数值；最终属性由 <see cref="RuntimeEntityStats"/> 持有。
/// </summary>
[CreateAssetMenu(fileName = "EntityStats", menuName = "GameMain/Stats/Entity Stats")]
public class EntityStatsSO : ScriptableObject
{
    [Min(1f)] public float MaxHealth = 100f;

    [Tooltip("地面行走目标速度上限（m/s）。")]
    public float BaseWalkSpeed = 2.5f;

    [Tooltip("地面奔跑目标速度上限（m/s）。")]
    public float BaseRunSpeed = 5.5f;

    public float BaseRotationSpeed = 540f;
    public float BaseAttackPower = 10f;
    public float BaseDefense = 0f;
}

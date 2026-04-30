using UnityEngine;

/// <summary>
/// Motion 到实体马达的单向接口。
/// Why: 状态只生产期望速度，不直接改坐标，物理执行点统一在 Motor 适配层。
/// </summary>
public interface IMotorAdapter
{
    void SetDesiredVelocity(Vector3 velocity);
    float GetActualSpeed();
}

using UnityEngine;

/// <summary>
/// Motion 到实体马达的单向接口。
/// Why: 状态只生产期望速度，不直接改坐标，物理执行点统一在 Motor 适配层。
/// </summary>
public interface IMotorAdapter
{
    void SetDesiredVelocity(Vector3 velocity);
    void SetMotionComposeContext(MotionYAxisConfig yAxisConfig);

    float GetActualSpeed();

    /// <summary>GroundTargeted：自 worldPos 向下探测地面世界 Y。</summary>
    bool TryProbeGroundHeight(Vector3 worldPos, float maxCastDistance, out float groundWorldY);
}

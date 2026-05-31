using UnityEngine;

/// <summary>
/// MotionExecutor 单帧位移贡献（角色局部：X=Right, Y=Up, Z=Forward）。
/// </summary>
public struct MotionContribution
{
    public Vector3 LocalDelta;
    public MotionYAxisConfig YAxisConfig;
    public bool IsActive;

    public static MotionContribution Inactive => default;
}

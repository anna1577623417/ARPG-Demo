using UnityEngine;

/// <summary>
/// 模块：2_Framework / Locomotion。职责：一帧内只读运动事实，不播动画、不切状态、不写朝向。
/// 237.3 LA — Raw / DesiredTravel / ActualTravel / DesiredFacing / CommittedFacing 分列。
/// </summary>
public readonly struct LocomotionRuntimeContext
{
    public readonly Vector2 Raw;
    public readonly Vector3 DesiredTravel;
    public readonly Vector3 ActualTravel;
    public readonly Vector3 DesiredFacing;
    public readonly Vector3 CommittedFacing;

    public LocomotionRuntimeContext(
        Vector2 raw,
        Vector3 desiredTravel,
        Vector3 actualTravel,
        Vector3 desiredFacing,
        Vector3 committedFacing)
    {
        Raw = raw;
        DesiredTravel = desiredTravel;
        ActualTravel = actualTravel;
        DesiredFacing = desiredFacing;
        CommittedFacing = committedFacing;
    }
}

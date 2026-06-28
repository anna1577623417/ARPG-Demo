/// <summary>
/// 179.1 — Stride Matching 外部依赖接口（最小化耦合）。
/// </summary>

/// <summary>动画驱动接口 —— PlayerAnimController 实现。</summary>
public interface IStrideAnimDriver
{
    /// <summary>设置当前 Loop 的播放速度倍率。</summary>
    void SetPlaybackSpeed(float ratio);
}

/// <summary>Motor 接口 —— PlayerKCCMotor 实现。</summary>
public interface IStrideMotor
{
    /// <summary>当前水平速度 m/s（XZ 投影）。</summary>
    float HorizontalSpeed { get; }
}

/// <summary>179.1 W6 — 步频更新事件（接 FootIK / 178.1 PhaseMatch / 音效 footstep）。</summary>
public struct StrideUpdatedEvent
{
    public ActionDataSO Loop;
    public float SmoothedSpeed;
    public float Ratio;
    public float FootPhase;
    public LoopTier Tier;
}

/// <summary>179.1 W5 — Tier 切换事件（PlayerLocomotionState 订阅）。</summary>
public struct LocomotionTierSwitchEvent
{
    public LoopTier From;
    public LoopTier To;
    public float CurrentSpeed;
}

/// <summary>
/// Action 播放期间的位移与物理模拟权威。
/// 表现身份、连续/离散语义仍由 LocomotionProfile State 与 ActionData 其他字段负责。
/// </summary>
public enum ActionMotionDriverMode : byte
{
    /// <summary>迁移兼容：RootMotion 优先，其次 MotionProfile，否则保持旧无驱动行为。</summary>
    LegacyAuto = 0,

    /// <summary>继承当前 Grounded/Airborne State 的基础 Motor，每帧恰好提交一次。</summary>
    InheritStateMotor = 1,

    /// <summary>由 MotionExecutor + MotionProfile 唯一提交 Motor。</summary>
    MotionProfile = 2,

    /// <summary>由 Animator Clip Root Motion 驱动；保持既有兼容边界。</summary>
    ClipRootMotion = 3,

    /// <summary>禁止平面意图，但继续提交基础 Motor 维护重力、垂直速度与接地。</summary>
    Stationary = 4,
}

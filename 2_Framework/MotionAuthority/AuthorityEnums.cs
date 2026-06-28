/// <summary>
/// 187.3 W1 — Motion Authority 共用数据结构。
/// <para>核心思想：散落在 LocomotionState / ActionState / TurnResolver / IntentRouter 的隐式优先级判定，
/// 显式建模为 AuthorityHolder + Priority + AuthorityChangeEvent，统一仲裁。</para>
/// </summary>

/// <summary>权威持有者（187.3 §3.2）。</summary>
public enum AuthorityHolder : byte
{
    None = 0,

    /// <summary>玩家 WASD → Motor 平面位移；Turn 转向。</summary>
    Locomotion = 1,

    /// <summary>ActionData.MotionProfile / 动作 Yaw 曲线 / Rotation 锁定。</summary>
    Action = 2,

    /// <summary>Animator 根骨写 Motor（179.1 W9 兜底路径）。</summary>
    RootMotion = 3,

    /// <summary>剧情 / QTE 接管。</summary>
    Cinematic = 4,

    /// <summary>177.1 Stance 切换的 Enter/Exit Action 占用。</summary>
    Stance = 5,

    /// <summary>184.3 WalkEnd/RunEnd/Turn/Pivot 等过渡动作（可被任何主动 Intent 抢占）。</summary>
    Recovery = 6,
}

/// <summary>权威通道：Move 与 Rotate 各自独立持有者（187.3 §3.3）。</summary>
public enum AuthorityChannel : byte
{
    Move = 0,
    Rotate = 1,
}

/// <summary>权威切换上下文事件（外部订阅以同步表现层）。</summary>
public struct AuthorityChangedEvent
{
    public AuthorityChannel Channel;
    public AuthorityHolder Previous;
    public AuthorityHolder Current;
    public int PreviousPriority;
    public int CurrentPriority;
    public string Reason;
    public float TimeStamp;
}

/// <summary>权威申请请求（187.3 §2.1）。</summary>
public struct AuthorityRequest
{
    public AuthorityHolder Holder;

    /// <summary>数字越小优先级越高。0=Cinematic / 100=Locomotion 兜底。</summary>
    public int Priority;

    /// <summary>调试日志用。</summary>
    public string DebugReason;
}

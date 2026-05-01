/// <summary>
/// 地面对 Transform 写权限的语义闸门：隔离「可硬吸附」与「仅碰撞 / 探针」两类策略。
/// Why: 根治空中动作 + SphereCast + HardSnap 的Ground Loop；并与钝角非法坡面解耦。
/// </summary>
public enum MotorGroundingPolicy : byte
{
    /// <summary>Locomotion：硬吸附 Y、下探贴地、Step 等强耦合（未接 Step 前仅吸附）。</summary>
    FullTerrainCoupling = 0,

    /// <summary>滞空 / 动作挂起：跑 CapsuleSweep，IsGrounded 用收紧探针，不写死脚底 Y。</summary>
    PhysicsPassThrough = 1,
}

/// <summary>每帧求解时由状态机提交的地面策略快照。</summary>
public readonly struct MotorSolveContext
{
    public MotorGroundingPolicy GroundingPolicy { get; }

    public MotorSolveContext(MotorGroundingPolicy groundingPolicy)
    {
        GroundingPolicy = groundingPolicy;
    }

    public static MotorSolveContext Locomotion => new(MotorGroundingPolicy.FullTerrainCoupling);

    public static MotorSolveContext Airborne => new(MotorGroundingPolicy.PhysicsPassThrough);

    public static MotorSolveContext DeadPhysics => new(MotorGroundingPolicy.PhysicsPassThrough);

    /// <inheritdoc cref="MotorGroundingPolicy.PhysicsPassThrough"/>
    /// <summary>重力挂起的动作语义：强制禁止硬拽地吸附。</summary>
    public static MotorSolveContext ActionSuspendedPhysics =>
        new(MotorGroundingPolicy.PhysicsPassThrough);

    public bool AllowsHardGroundSnap => GroundingPolicy == MotorGroundingPolicy.FullTerrainCoupling;
}

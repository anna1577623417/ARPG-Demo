/// <summary>
/// Motion Y 三权配置：Y 来源 / 重力 / 地面约束（蓝图 135.1）。
/// </summary>
public readonly struct MotionYAxisConfig
{
    public readonly YMotionMode YMotion;
    public readonly GravityMode Gravity;
    public readonly GroundConstraintMode GroundConstraint;

    public MotionYAxisConfig(
        YMotionMode yMotion,
        GravityMode gravity,
        GroundConstraintMode groundConstraint)
    {
        YMotion = yMotion;
        Gravity = gravity;
        GroundConstraint = groundConstraint;
    }

    public static MotionYAxisConfig DefaultLocomotion =>
        new MotionYAxisConfig(YMotionMode.None, GravityMode.UseGravity, GroundConstraintMode.None);
}

/// <summary>
/// 旧 YAxisPolicy → 三权配置（未勾选 yAxisV2Configured 的资产走此映射）。
/// </summary>
public static class MotionYAxisLegacyMapping
{
    public static MotionYAxisConfig FromLegacy(YAxisPolicy policy)
    {
        switch (policy)
        {
            case YAxisPolicy.MotionControlled:
                return new MotionYAxisConfig(
                    YMotionMode.Curve,
                    GravityMode.SuspendGravity,
                    GroundConstraintMode.None);

            case YAxisPolicy.AdditiveGravity:
                return new MotionYAxisConfig(
                    YMotionMode.Curve,
                    GravityMode.AdditiveGravity,
                    GroundConstraintMode.None);

            case YAxisPolicy.SuspendGravity:
                return new MotionYAxisConfig(
                    YMotionMode.None,
                    GravityMode.SuspendGravity,
                    GroundConstraintMode.None);

            case YAxisPolicy.GroundTargeted:
                return new MotionYAxisConfig(
                    YMotionMode.GroundTargeted,
                    GravityMode.SuspendGravity,
                    GroundConstraintMode.None);

            case YAxisPolicy.UseGravity:
            default:
                return new MotionYAxisConfig(
                    YMotionMode.Curve,
                    GravityMode.UseGravity,
                    GroundConstraintMode.ClampToGround);
        }
    }
}

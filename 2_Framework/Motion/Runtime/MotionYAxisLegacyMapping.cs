/// <summary>
/// 旧 Y 轴策略整型值 → 三权配置（未勾选 yAxisV2Configured 的资产走此映射）。
/// 值域与已废弃的 <c>YAxisPolicy</c> 枚举一致，避免编译期引用 Obsolete 类型。
/// </summary>
public static class MotionYAxisLegacyMapping
{
    public const byte UseGravity = 0;
    public const byte SuspendGravity = 1;
    public const byte MotionControlled = 2;
    public const byte AdditiveGravity = 3;
    public const byte GroundTargeted = 4;

    public static MotionYAxisConfig FromLegacy(byte legacyPolicy)
    {
        switch (legacyPolicy)
        {
            case MotionControlled:
                return new MotionYAxisConfig(
                    YMotionMode.Curve,
                    GravityMode.SuspendGravity,
                    GroundConstraintMode.None);

            case AdditiveGravity:
                return new MotionYAxisConfig(
                    YMotionMode.Curve,
                    GravityMode.AdditiveGravity,
                    GroundConstraintMode.None);

            case SuspendGravity:
                return new MotionYAxisConfig(
                    YMotionMode.None,
                    GravityMode.SuspendGravity,
                    GroundConstraintMode.None);

            case GroundTargeted:
                return new MotionYAxisConfig(
                    YMotionMode.GroundTargeted,
                    GravityMode.SuspendGravity,
                    GroundConstraintMode.None);

            case UseGravity:
            default:
                return new MotionYAxisConfig(
                    YMotionMode.Curve,
                    GravityMode.UseGravity,
                    GroundConstraintMode.ClampToGround);
        }
    }
}

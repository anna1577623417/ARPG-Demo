/// <summary>
/// 重力通道单帧垂直速度贡献（m/s），由 PlayerKCCMotor 产出、MotionComposer 消费。
/// </summary>
public struct GravityContribution
{
    public float Vy;
    public bool IsActive;

    public static GravityContribution Inactive => default;
}

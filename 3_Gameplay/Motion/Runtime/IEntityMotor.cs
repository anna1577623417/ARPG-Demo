using UnityEngine;

/// <summary>
/// 220.5 B4：所有战斗实体可共享的最小 Motor 能力。
/// <para>状态与 Feedback 只提交速度/冲量，不直接写 Transform。</para>
/// </summary>
public interface IEntityMotor
{
    Vector3 PlanarVelocity { get; }
    float VerticalSpeed { get; }
    bool IsGrounded { get; }

    void SetPlanarVelocity(Vector3 planarVelocity);
    void SetVerticalSpeed(float verticalSpeed);
    void ApplyMotor(in MotorSolveContext context);
}

using UnityEngine;

/// <summary>
/// 地面约束层：在 MotionComposer 之后、Motor 求解之前应用。
/// </summary>
public static class MotionGroundConstraint
{
    public static void ApplyClamp(ref Vector3 worldVelocity, in MotionYAxisConfig config, bool isGrounded)
    {
        if (config.GroundConstraint != GroundConstraintMode.ClampToGround || !isGrounded)
        {
            return;
        }

        if (worldVelocity.y > 0f)
        {
            worldVelocity.y = 0f;
        }
    }
}

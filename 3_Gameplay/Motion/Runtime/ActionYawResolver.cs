using UnityEngine;

/// <summary>
/// 210.5 Landing 1 — Action 期 Yaw 采样与 forward 解析（表现层；与位移解耦）。
/// </summary>
public static class ActionYawResolver
{
    public static float SampleActionYawDegrees(MotionProfileSO profile, float motionT)
    {
        if (profile == null)
        {
            return 0f;
        }

        var t = Mathf.Clamp01(motionT);
        switch (profile.YawPolicy)
        {
            case YawPolicyMode.None:
                return 0f;

            case YawPolicyMode.Constant:
                return profile.YawStartDegrees;

            case YawPolicyMode.Curve:
            {
                var blend = profile.SampleYawBlendFactor(t);
                return Mathf.LerpAngle(profile.YawStartDegrees, profile.YawEndDegrees, blend);
            }

            default:
                return 0f;
        }
    }

    public static Vector3 ResolveForwardFromBurstYaw(float burstForwardYawDegrees, float actionYawOffsetDegrees)
    {
        var yaw = burstForwardYawDegrees + actionYawOffsetDegrees;
        return ForwardFromYaw(yaw);
    }

    public static Vector3 ResolveForwardFromBurstForward(
        Vector3 burstForward,
        float actionYawOffsetDegrees)
    {
        return ResolveForwardFromBurstYaw(YawFromForward(burstForward), actionYawOffsetDegrees);
    }

    public static float YawFromForward(Vector3 forward)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            return 0f;
        }

        forward.Normalize();
        return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
    }

    public static Vector3 ForwardFromYaw(float yawDegrees)
        => Quaternion.Euler(0f, yawDegrees, 0f) * Vector3.forward;
}

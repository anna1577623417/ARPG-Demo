using UnityEngine;

/// <summary>
/// GroundTargeted 落地 Y 插值（世界 Up）。
/// </summary>
public static class MotionGroundLanding
{
    public static bool TryResolveEndHeight(
        IMotorAdapter motor,
        Vector3 startWorldPos,
        MotionProfileSO profile,
        out float startY,
        out float endY)
    {
        startY = startWorldPos.y;
        endY = startY;
        if (profile == null || !profile.UsesGroundTargetedLanding)
        {
            return false;
        }

        var offset = profile.LandingOffset;
        if (motor != null
            && motor.TryProbeGroundHeight(
                startWorldPos,
                profile.LandingDetectionRadius,
                out var groundY))
        {
            endY = groundY + offset;
            return true;
        }

        endY = startY + offset;
        return true;
    }

    public static float SampleTargetWorldY(
        float startY,
        float endY,
        AnimationCurve landingCurve,
        float normalizedTime)
    {
        var curve = landingCurve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
        var u = Mathf.Clamp01(curve.Evaluate(Mathf.Clamp01(normalizedTime)));
        return Mathf.Lerp(startY, endY, u);
    }
}

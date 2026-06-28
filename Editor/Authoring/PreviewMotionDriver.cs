#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// 171.1 — Editor 预览用 MotionProfile 采样（与 MotionExecutor LocalDeltaToWorld 同口径）。
/// </summary>
public static class PreviewMotionDriver
{
    public const int DefaultPathSampleCount = 48;

    public static Vector3 EvaluateLocalPosition(MotionProfileSO profile, float normalizedTime, float motionScale = 1f)
    {
        if (profile == null || !profile.UsesAxisCurves)
        {
            return Vector3.zero;
        }

        return profile.AxisCurves.SampleLocalPosition(Mathf.Clamp01(normalizedTime), motionScale);
    }

    public static Vector3 LocalToWorld(Vector3 localDelta, Vector3 anchorOrigin, Vector3 planarForward)
    {
        var fwd = planarForward.sqrMagnitude > 0.0001f ? planarForward.normalized : Vector3.forward;
        var right = Vector3.Cross(Vector3.up, fwd);
        if (right.sqrMagnitude < 0.0001f)
        {
            right = Vector3.right;
        }
        else
        {
            right.Normalize();
        }

        return anchorOrigin + right * localDelta.x + Vector3.up * localDelta.y + fwd * localDelta.z;
    }

    public static Vector3 EvaluateWorldPosition(
        MotionProfileSO profile,
        float normalizedTime,
        Vector3 anchorOrigin,
        Vector3 planarForward,
        float motionScale = 1f)
    {
        var local = EvaluateLocalPosition(profile, normalizedTime, motionScale);
        return LocalToWorld(local, anchorOrigin, planarForward);
    }

    public static void BuildMotionProfilePath(
        MotionProfileSO profile,
        Vector3 anchorOrigin,
        Vector3 planarForward,
        int sampleCount,
        Vector3[] outWorldPoints)
    {
        if (outWorldPoints == null || outWorldPoints.Length == 0)
        {
            return;
        }

        var count = outWorldPoints.Length;
        if (profile == null || !profile.UsesAxisCurves)
        {
            for (var i = 0; i < count; i++)
            {
                outWorldPoints[i] = anchorOrigin;
            }

            return;
        }

        for (var i = 0; i < count; i++)
        {
            var t = count <= 1 ? 0f : i / (float)(count - 1);
            outWorldPoints[i] = EvaluateWorldPosition(profile, t, anchorOrigin, planarForward);
        }
    }
}
#endif

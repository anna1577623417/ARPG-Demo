#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Humanoid AnimationClip RootT 曲线采样（与 RefSpeedExtractor 同源）。
/// </summary>
internal static class ClipMotionRootTSampling
{
    public struct RootTCurves
    {
        public AnimationCurve X;
        public AnimationCurve Y;
        public AnimationCurve Z;
        public bool HasAny => X != null || Y != null || Z != null;
    }

    public static bool TryGetRootTCurves(AnimationClip clip, out RootTCurves curves)
    {
        curves = default;
        if (clip == null)
        {
            return false;
        }

        var bindings = AnimationUtility.GetCurveBindings(clip);
        for (var i = 0; i < bindings.Length; i++)
        {
            var b = bindings[i];
            var c = AnimationUtility.GetEditorCurve(clip, b);
            if (c == null)
            {
                continue;
            }

            switch (b.propertyName)
            {
                case "RootT.x":
                    curves.X = c;
                    break;
                case "RootT.y":
                    curves.Y = c;
                    break;
                case "RootT.z":
                    curves.Z = c;
                    break;
            }
        }

        return curves.HasAny;
    }

    public static Vector3 Evaluate(in RootTCurves curves, float timeSeconds)
    {
        return new Vector3(
            curves.X != null ? curves.X.Evaluate(timeSeconds) : 0f,
            curves.Y != null ? curves.Y.Evaluate(timeSeconds) : 0f,
            curves.Z != null ? curves.Z.Evaluate(timeSeconds) : 0f);
    }

    public static float SumAbsDisplacement(in RootTCurves curves, float clipLength)
    {
        if (!curves.HasAny || clipLength <= 1e-5f)
        {
            return 0f;
        }

        var origin = Evaluate(in curves, 0f);
        var end = Evaluate(in curves, clipLength);
        var delta = end - origin;
        return Mathf.Abs(delta.x) + Mathf.Abs(delta.y) + Mathf.Abs(delta.z);
    }
}
#endif

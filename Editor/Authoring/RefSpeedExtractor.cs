#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 从 AnimationClip Root 曲线离线提取参考速度。
/// Why: 运行时混合态采样不稳定，离线提取可得到更一致的空间对齐基准值。
/// </summary>
public static class RefSpeedExtractor
{
    public static void Extract()
    {
        var clip = Selection.activeObject as AnimationClip;
        if (clip == null)
        {
            Debug.LogError("[Motion] Please select an AnimationClip first.");
            return;
        }

        var totalDisplacement = 0f;
        if (ClipMotionRootTSampling.TryGetRootTCurves(clip, out var curves))
        {
            totalDisplacement = Mathf.Abs(ClipMotionRootTSampling.Evaluate(in curves, clip.length).z
                - ClipMotionRootTSampling.Evaluate(in curves, 0f).z);
        }

        var refSpeed = totalDisplacement / Mathf.Max(0.0001f, clip.length);
        Debug.Log($"[Motion RefSpeed] Clip={clip.name}, Length={clip.length:F3}s, Disp={totalDisplacement:F3}m, Ref={refSpeed:F3}m/s");
    }
}
#endif

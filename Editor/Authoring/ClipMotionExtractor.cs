#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// AnimationClip 根节点局部位移 → MotionProfile AxisCurves（t∈[0,1]，起点归零）。
/// </summary>
public static class ClipMotionExtractor
{
    [Flags]
    public enum AxisMask
    {
        None = 0,
        X = 1,
        Y = 2,
        Z = 4,
        XYZ = X | Y | Z,
    }

    public struct Options
    {
        public int SamplingFps;
        public bool ZeroOrigin;
        public bool NormalizeCurvesTo01;
        public AxisMask Axes;
        public MotionCurveFitMode FitMode;
        public MotionCurveFilterMode FilterMode;
        public int FilterWindow;
        public float ErrorTolerance;

        public static Options Default => new Options
        {
            SamplingFps = 30,
            ZeroOrigin = true,
            NormalizeCurvesTo01 = true,
            Axes = AxisMask.XYZ,
            FitMode = MotionCurveFitMode.Smooth,
            FilterMode = MotionCurveFilterMode.MovingAverage,
            FilterWindow = 5,
            ErrorTolerance = 0.01f,
        };
    }

    public struct ExtractReport
    {
        public int RawSamples;
        public int KeysX;
        public int KeysY;
        public int KeysZ;
    }

    public static ExtractReport LastReport { get; private set; }

    public static bool ExtractInto(
        AnimationClip clip,
        Transform sampleRoot,
        MotionProfileSO target,
        Options opt)
    {
        if (clip == null || sampleRoot == null || target == null)
        {
            Debug.LogWarning("[MotionXYZ][Extract] clip / root / target 为空");
            return false;
        }

        opt.SamplingFps = Mathf.Clamp(opt.SamplingFps, 8, 120);
        var duration = Mathf.Max(clip.length, 1f / opt.SamplingFps);
        var frameCount = Mathf.Max(2, Mathf.RoundToInt(duration * opt.SamplingFps));

        var positions = new Vector3[frameCount];
        var times = new float[frameCount];
        var wasActive = sampleRoot.gameObject.activeSelf;
        if (!wasActive)
        {
            sampleRoot.gameObject.SetActive(true);
        }

        try
        {
            for (var i = 0; i < frameCount; i++)
            {
                var tNorm = frameCount <= 1 ? 0f : (float)i / (frameCount - 1);
                times[i] = tNorm;
                var tSec = tNorm * clip.length;
                clip.SampleAnimation(sampleRoot.gameObject, tSec);
                positions[i] = sampleRoot.localPosition;
            }
        }
        finally
        {
            if (!wasActive)
            {
                sampleRoot.gameObject.SetActive(false);
            }
        }

        var origin = opt.ZeroOrigin ? positions[0] : Vector3.zero;
        var maxX = 0f;
        var maxY = 0f;
        var maxZ = 0f;
        var valsX = new float[frameCount];
        var valsY = new float[frameCount];
        var valsZ = new float[frameCount];

        for (var i = 0; i < frameCount; i++)
        {
            var p = positions[i] - origin;
            maxX = Mathf.Max(maxX, Mathf.Abs(p.x));
            maxY = Mathf.Max(maxY, Mathf.Abs(p.y));
            maxZ = Mathf.Max(maxZ, Mathf.Abs(p.z));
            valsX[i] = p.x;
            valsY[i] = p.y;
            valsZ[i] = p.z;
        }

        if (opt.NormalizeCurvesTo01)
        {
            for (var i = 0; i < frameCount; i++)
            {
                valsX[i] = maxX > 1e-5f ? valsX[i] / maxX : 0f;
                valsY[i] = maxY > 1e-5f ? valsY[i] / maxY : 0f;
                valsZ[i] = maxZ > 1e-5f ? valsZ[i] / maxZ : 0f;
            }
        }

        var fitSettings = new MotionCurveFitPipeline.Settings
        {
            FitMode = opt.FitMode,
            FilterMode = opt.FilterMode,
            FilterWindow = opt.FilterWindow,
            ErrorTolerance = opt.ErrorTolerance,
        };

        Undo.RecordObject(target, "Extract Motion From Clip");
        MotionCurveFitPipeline.Result rx = default;
        MotionCurveFitPipeline.Result ry = default;
        MotionCurveFitPipeline.Result rz = default;

        if ((opt.Axes & AxisMask.X) != 0)
        {
            target.AxisCurves.XCurve = MotionCurveFitPipeline.BuildCurve(times, valsX, in fitSettings, out rx);
            target.AxisCurves.XScale = opt.NormalizeCurvesTo01 ? maxX : 1f;
        }

        if ((opt.Axes & AxisMask.Y) != 0)
        {
            target.AxisCurves.YCurve = MotionCurveFitPipeline.BuildCurve(times, valsY, in fitSettings, out ry);
            target.AxisCurves.YScale = opt.NormalizeCurvesTo01 ? maxY : 1f;
        }

        if ((opt.Axes & AxisMask.Z) != 0)
        {
            target.AxisCurves.ZCurve = MotionCurveFitPipeline.BuildCurve(times, valsZ, in fitSettings, out rz);
            target.AxisCurves.ZScale = opt.NormalizeCurvesTo01 ? maxZ : 1f;
        }

        LastReport = new ExtractReport
        {
            RawSamples = frameCount,
            KeysX = rx.OutputKeyCount,
            KeysY = ry.OutputKeyCount,
            KeysZ = rz.OutputKeyCount,
        };

        EditorUtility.SetDirty(target);
        Debug.Log(
            $"[MotionXYZ][Extract] {clip.name} → {target.name} raw={frameCount} keys=({rx.OutputKeyCount},{ry.OutputKeyCount},{rz.OutputKeyCount}) " +
            $"fit={opt.FitMode} scale=({target.AxisCurves.XScale:F2},{target.AxisCurves.YScale:F2},{target.AxisCurves.ZScale:F2})m " +
            $"compress≈{rx.CompressionRatio * 100f:F0}%");
        return true;
    }

    public static Transform ResolveSampleRoot(GameObject rigRoot, string bonePath)
    {
        if (rigRoot == null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(bonePath))
        {
            return rigRoot.transform;
        }

        var t = rigRoot.transform.Find(bonePath);
        return t != null ? t : rigRoot.transform;
    }
}
#endif

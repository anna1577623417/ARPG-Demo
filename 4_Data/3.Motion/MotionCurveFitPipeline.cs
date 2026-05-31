using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Clip 提取后曲线优化等级（132.1 Motion Curve Fit Pipeline）。
/// </summary>
public enum MotionCurveFitMode : byte
{
    Raw = 0,
    Smooth = 1,
    AggressiveSmooth = 2,
}

public enum MotionCurveFilterMode : byte
{
    None = 0,
    MovingAverage = 1,
    SavitzkyGolay = 2,
}

/// <summary>
/// 采样序列 → 降噪 → Key 约简 → Catmull-Rom 切线 → AnimationCurve。
/// </summary>
public static class MotionCurveFitPipeline
{
    public struct Settings
    {
        public MotionCurveFitMode FitMode;
        public MotionCurveFilterMode FilterMode;
        public int FilterWindow;
        public float ErrorTolerance;

        public static Settings DefaultSmooth => new Settings
        {
            FitMode = MotionCurveFitMode.Smooth,
            FilterMode = MotionCurveFilterMode.MovingAverage,
            FilterWindow = 5,
            ErrorTolerance = 0.01f,
        };

        public static Settings DefaultAggressive => new Settings
        {
            FitMode = MotionCurveFitMode.AggressiveSmooth,
            FilterMode = MotionCurveFilterMode.SavitzkyGolay,
            FilterWindow = 7,
            ErrorTolerance = 0.01f,
        };
    }

    public struct Result
    {
        public int RawSampleCount;
        public int OutputKeyCount;
        public float CompressionRatio;
    }

    public static AnimationCurve BuildCurve(float[] times, float[] values, in Settings settings, out Result result)
    {
        result = default;
        if (times == null || values == null || times.Length == 0 || times.Length != values.Length)
        {
            return new AnimationCurve();
        }

        result.RawSampleCount = times.Length;
        var t = (float[])times.Clone();
        var v = (float[])values.Clone();

        if (settings.FitMode != MotionCurveFitMode.Raw)
        {
            v = ApplyFilter(v, settings.FilterMode, settings.FilterWindow);
        }

        List<int> keep;
        if (settings.FitMode == MotionCurveFitMode.Raw)
        {
            keep = BuildIndexList(t.Length);
        }
        else
        {
            keep = ReduceIndices(t, v, settings.ErrorTolerance);
            if (keep.Count < 2)
            {
                keep = BuildIndexList(t.Length);
            }
        }

        var keys = new Keyframe[keep.Count];
        for (var i = 0; i < keep.Count; i++)
        {
            var idx = keep[i];
            keys[i] = new Keyframe(t[idx], v[idx]);
        }

        if (settings.FitMode == MotionCurveFitMode.AggressiveSmooth && keys.Length >= 3)
        {
            ApplyCatmullRomTangents(keys, t, v, keep);
        }

        result.OutputKeyCount = keys.Length;
        result.CompressionRatio = result.RawSampleCount > 0
            ? 1f - (float)result.OutputKeyCount / result.RawSampleCount
            : 0f;

        return new AnimationCurve(keys);
    }

    static List<int> BuildIndexList(int count)
    {
        var list = new List<int>(count);
        for (var i = 0; i < count; i++)
        {
            list.Add(i);
        }

        return list;
    }

    static float[] ApplyFilter(float[] samples, MotionCurveFilterMode mode, int window)
    {
        switch (mode)
        {
            case MotionCurveFilterMode.MovingAverage:
                return ApplyMovingAverage(samples, window);
            case MotionCurveFilterMode.SavitzkyGolay:
                return ApplySavitzkyGolay(samples, window);
            default:
                return samples;
        }
    }

    static float[] ApplySavitzkyGolay(float[] samples, int window)
    {
        if (samples == null || samples.Length < 3)
        {
            return samples;
        }

        window = NormalizeOddWindow(window);
        if (samples.Length < window)
        {
            return ApplyMovingAverage(samples, window);
        }

        var half = window / 2;
        var coeffs = GetSavitzkyGolayCoeffs(window);
        var outV = new float[samples.Length];

        for (var i = 0; i < samples.Length; i++)
        {
            if (i < half || i >= samples.Length - half)
            {
                outV[i] = samples[i];
                continue;
            }

            var sum = 0f;
            for (var k = 0; k < window; k++)
            {
                sum += coeffs[k] * samples[i - half + k];
            }

            outV[i] = sum;
        }

        return outV;
    }

    static int NormalizeOddWindow(int window)
    {
        window = Mathf.Clamp(window, 5, 11);
        if ((window & 1) == 0)
        {
            window += 1;
        }

        if (window != 5 && window != 7 && window != 9 && window != 11)
        {
            window = window <= 6 ? 5 : window <= 8 ? 7 : window <= 10 ? 9 : 11;
        }

        return window;
    }

    static float[] GetSavitzkyGolayCoeffs(int window)
    {
        switch (window)
        {
            case 5:
                return new[] { -3f / 35f, 12f / 35f, 17f / 35f, 12f / 35f, -3f / 35f };
            case 7:
                return new[] { -2f / 21f, 3f / 21f, 6f / 21f, 7f / 21f, 6f / 21f, 3f / 21f, -2f / 21f };
            case 9:
                return new[]
                {
                    -21f / 231f, 14f / 231f, 39f / 231f, 54f / 231f, 59f / 231f,
                    54f / 231f, 39f / 231f, 14f / 231f, -21f / 231f,
                };
            case 11:
                return new[]
                {
                    -36f / 429f, 9f / 429f, 44f / 429f, 69f / 429f, 84f / 429f, 89f / 429f,
                    84f / 429f, 69f / 429f, 44f / 429f, 9f / 429f, -36f / 429f,
                };
            default:
                return GetSavitzkyGolayCoeffs(5);
        }
    }

    static float[] ApplyMovingAverage(float[] samples, int window)
    {
        if (samples == null || samples.Length == 0)
        {
            return samples;
        }

        window = Mathf.Clamp(window, 3, 15);
        if ((window & 1) == 0)
        {
            window += 1;
        }

        var half = window / 2;
        var outV = new float[samples.Length];
        for (var i = 0; i < samples.Length; i++)
        {
            var sum = 0f;
            var n = 0;
            for (var j = i - half; j <= i + half; j++)
            {
                if (j < 0 || j >= samples.Length)
                {
                    continue;
                }

                sum += samples[j];
                n++;
            }

            outV[i] = n > 0 ? sum / n : samples[i];
        }

        return outV;
    }

    static List<int> ReduceIndices(float[] t, float[] v, float tolerance)
    {
        var eps = Mathf.Max(1e-5f, tolerance);
        var keep = new List<int> { 0 };
        DouglasPeuckerRecursive(t, v, 0, t.Length - 1, eps, keep);
        if (keep[keep.Count - 1] != t.Length - 1)
        {
            keep.Add(t.Length - 1);
        }

        keep.Sort();
        DeduplicateSorted(keep);
        return keep;
    }

    static void DeduplicateSorted(List<int> keep)
    {
        if (keep.Count < 2)
        {
            return;
        }

        var w = 1;
        for (var r = 1; r < keep.Count; r++)
        {
            if (keep[r] != keep[w - 1])
            {
                keep[w++] = keep[r];
            }
        }

        if (w < keep.Count)
        {
            keep.RemoveRange(w, keep.Count - w);
        }
    }

    static void DouglasPeuckerRecursive(float[] t, float[] v, int start, int end, float eps, List<int> keep)
    {
        if (end <= start + 1)
        {
            return;
        }

        var maxDist = 0f;
        var index = start;
        for (var i = start + 1; i < end; i++)
        {
            var d = PerpendicularDistance(t[start], v[start], t[end], v[end], t[i], v[i]);
            if (d > maxDist)
            {
                maxDist = d;
                index = i;
            }
        }

        if (maxDist > eps)
        {
            DouglasPeuckerRecursive(t, v, start, index, eps, keep);
            keep.Add(index);
            DouglasPeuckerRecursive(t, v, index, end, eps, keep);
        }
    }

    static float PerpendicularDistance(float x1, float y1, float x2, float y2, float px, float py)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        if (Mathf.Abs(dx) < 1e-8f && Mathf.Abs(dy) < 1e-8f)
        {
            return Vector2.Distance(new Vector2(x1, y1), new Vector2(px, py));
        }

        var u = ((px - x1) * dx + (py - y1) * dy) / (dx * dx + dy * dy);
        u = Mathf.Clamp01(u);
        var projX = x1 + u * dx;
        var projY = y1 + u * dy;
        return Vector2.Distance(new Vector2(px, py), new Vector2(projX, projY));
    }

    static void ApplyCatmullRomTangents(Keyframe[] keys, float[] t, float[] v, List<int> keep)
    {
        for (var i = 0; i < keys.Length; i++)
        {
            var i0 = Mathf.Max(0, i - 1);
            var i1 = i;
            var i2 = Mathf.Min(keys.Length - 1, i + 1);
            var i3 = Mathf.Min(keys.Length - 1, i + 2);

            var t1 = t[keep[i1]];
            var t2 = t[keep[i2]];
            var dt = t2 - t1;
            if (Mathf.Abs(dt) < 1e-6f)
            {
                continue;
            }

            var slope = (v[keep[i2]] - v[keep[i1]]) / dt;
            var k = keys[i1];
            k.inTangent = i > 0 ? slope : k.inTangent;
            k.outTangent = i < keys.Length - 1 ? slope : k.outTangent;
            keys[i1] = k;
        }
    }
}

using NUnit.Framework;
using UnityEngine;

public sealed class MotionCurveFitPipelineTests
{
    [Test]
    public void Smooth_ReducesKeyCount()
    {
        const int n = 40;
        var t = new float[n];
        var v = new float[n];
        for (var i = 0; i < n; i++)
        {
            t[i] = (float)i / (n - 1);
            v[i] = Mathf.Sin(t[i] * Mathf.PI);
        }

        var settings = MotionCurveFitPipeline.Settings.DefaultSmooth;
        settings.ErrorTolerance = 0.02f;
        var curve = MotionCurveFitPipeline.BuildCurve(t, v, in settings, out var result);

        Assert.Less(result.OutputKeyCount, result.RawSampleCount);
        Assert.GreaterOrEqual(curve.length, 2);
    }

    [Test]
    public void Raw_KeepsAllSamples()
    {
        var t = new[] { 0f, 0.5f, 1f };
        var v = new[] { 0f, 1f, 0f };
        var settings = new MotionCurveFitPipeline.Settings { FitMode = MotionCurveFitMode.Raw };
        var curve = MotionCurveFitPipeline.BuildCurve(t, v, in settings, out var result);

        Assert.AreEqual(3, result.OutputKeyCount);
        Assert.AreEqual(3, curve.length);
    }

    [Test]
    public void SavitzkyGolay_ReducesKeyCountWithSmooth()
    {
        const int n = 40;
        var t = new float[n];
        var v = new float[n];
        for (var i = 0; i < n; i++)
        {
            t[i] = (float)i / (n - 1);
            v[i] = Mathf.Sin(t[i] * Mathf.PI) + (i % 3 == 0 ? 0.02f : 0f);
        }

        var settings = new MotionCurveFitPipeline.Settings
        {
            FitMode = MotionCurveFitMode.Smooth,
            FilterMode = MotionCurveFilterMode.SavitzkyGolay,
            FilterWindow = 7,
            ErrorTolerance = 0.015f,
        };
        var curve = MotionCurveFitPipeline.BuildCurve(t, v, in settings, out var result);

        Assert.Less(result.OutputKeyCount, result.RawSampleCount);
        Assert.GreaterOrEqual(curve.length, 2);
    }
}

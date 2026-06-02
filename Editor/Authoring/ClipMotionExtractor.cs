#if UNITY_EDITOR
using System;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// AnimationClip → MotionProfile AxisCurves（143.1 多源提取：RootT / 骨骼 / Animator RM）。
/// </summary>
public static class ClipMotionExtractor
{
    const float DisplacementEpsilon = 1e-4f;

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
        public MotionExtractMode ExtractMode;
        public string SampleBonePath;
        public MotionAxisExtractSource XSource;
        public MotionAxisExtractSource YSource;
        public MotionAxisExtractSource ZSource;
        public AnimationClip ClipPrimary;
        public AnimationClip ClipX;
        public AnimationClip ClipY;
        public AnimationClip ClipZ;

        public static Options Default => new Options
        {
            SamplingFps = 30,
            ZeroOrigin = true,
            NormalizeCurvesTo01 = true,
            Axes = AxisMask.XYZ,
            FitMode = MotionCurveFitMode.CatmullRom,
            FilterMode = MotionCurveFilterMode.MovingAverage,
            FilterWindow = 5,
            ErrorTolerance = 0.01f,
            ExtractMode = MotionExtractMode.Auto,
            SampleBonePath = "Hips",
            XSource = MotionAxisExtractSource.Auto,
            YSource = MotionAxisExtractSource.Auto,
            ZSource = MotionAxisExtractSource.Auto,
        };
    }

    public struct ExtractReport
    {
        public bool Succeeded;
        public MotionExtractSourceKind Source;
        public MotionExtractSourceKind SourceX;
        public MotionExtractSourceKind SourceY;
        public MotionExtractSourceKind SourceZ;
        public int RawSamples;
        public int KeysX;
        public int KeysY;
        public int KeysZ;
        public Vector3 MaxAbsMeters;
        public Vector3 Quality;
        public string Message;
    }

    public static ExtractReport LastReport { get; private set; }

    public static bool ExtractInto(
        AnimationClip clip,
        GameObject previewRig,
        MotionProfileSO target,
        Options opt)
    {
        LastReport = default;

        if (target == null)
        {
            LogFail(clip, target, "target 为空");
            return false;
        }

        opt = OptionsFromProfile(target, opt);
        if (clip != null)
        {
            opt.ClipPrimary = clip;
        }

        if (!HasAnyClip(opt))
        {
            LogFail(clip, target, "无 SourceClip / 按轴 Clip");
            return false;
        }

        opt.SamplingFps = Mathf.Clamp(opt.SamplingFps, 8, 120);

        if (ShouldUsePerAxisPipeline(target, opt))
        {
            return ExtractPerAxis(previewRig, target, opt);
        }

        var primary = ResolvePrimaryClip(opt);
        if (primary == null)
        {
            LogFail(null, target, "无可用 SourceClip");
            return false;
        }

        if (!TrySampleDisplacement(primary, previewRig, opt, out var motionSamples, out var source, out var diag))
        {
            LogFail(primary, target, diag);
            return false;
        }

        return WriteCurves(primary, target, opt, motionSamples, source);
    }

    public static Options OptionsFromProfile(MotionProfileSO profile, Options template)
    {
        var opt = template;
        if (profile == null)
        {
            return opt;
        }

        opt.XSource = profile.XExtractSource;
        opt.YSource = profile.YExtractSource;
        opt.ZSource = profile.ZExtractSource;
        opt.ClipPrimary = profile.SourceClip;
        opt.ClipX = profile.XSourceClip;
        opt.ClipY = profile.YSourceClip;
        opt.ClipZ = profile.ZSourceClip;
        return opt;
    }

    static bool ShouldUsePerAxisPipeline(MotionProfileSO profile, Options opt) =>
        profile != null && profile.UsesPerAxisSourceClips
        || UsesPerAxisSourceModes(opt);

    static bool UsesPerAxisSourceModes(Options opt) =>
        opt.XSource != MotionAxisExtractSource.Auto
        || opt.YSource != MotionAxisExtractSource.Auto
        || opt.ZSource != MotionAxisExtractSource.Auto;

    static AnimationClip ResolvePrimaryClip(Options opt) =>
        opt.ClipPrimary != null ? opt.ClipPrimary : opt.ClipX ?? opt.ClipY ?? opt.ClipZ;

    static AnimationClip ResolveClipForAxis(Options opt, int axisIndex) => axisIndex switch
    {
        0 => opt.ClipX ?? opt.ClipPrimary,
        1 => opt.ClipY ?? opt.ClipPrimary,
        2 => opt.ClipZ ?? opt.ClipPrimary,
        _ => opt.ClipPrimary,
    };

    static bool HasAnyClip(Options opt) =>
        opt.ClipPrimary != null || opt.ClipX != null || opt.ClipY != null || opt.ClipZ != null;

    /// <summary>兼容旧调用：仅 Legacy 根 Transform。</summary>
    public static bool ExtractInto(
        AnimationClip clip,
        Transform sampleRoot,
        MotionProfileSO target,
        Options opt)
    {
        if (sampleRoot == null)
        {
            return ExtractInto(clip, (GameObject)null, target, opt);
        }

        if (opt.ExtractMode == MotionExtractMode.Auto)
        {
            return ExtractInto(clip, sampleRoot.gameObject, target, opt);
        }

        opt.ExtractMode = MotionExtractMode.LegacyRootTransform;
        return ExtractInto(clip, sampleRoot.gameObject, target, opt);
    }

    static bool TrySampleDisplacement(
        AnimationClip clip,
        GameObject previewRig,
        Options opt,
        out Vector3[] motionLocal,
        out MotionExtractSourceKind source,
        out string failReason)
    {
        motionLocal = null;
        source = MotionExtractSourceKind.None;
        failReason = null;

        var mode = opt.ExtractMode;
        if (mode == MotionExtractMode.Auto)
        {
            if (TrySampleRootTCurves(clip, opt, out motionLocal))
            {
                source = MotionExtractSourceKind.RootTCurve;
                return true;
            }

            if (previewRig == null)
            {
                failReason = BuildFailReason(clip, "Auto：无 RootT 有效曲线，且未选中 Hierarchy Rig。");
                return false;
            }

            if (TrySampleBonePath(clip, previewRig, opt, out motionLocal))
            {
                source = MotionExtractSourceKind.SampleBone;
                return true;
            }

            if (TrySampleAnimatorRootMotion(clip, previewRig, opt, out motionLocal))
            {
                source = MotionExtractSourceKind.AnimatorRootMotion;
                return true;
            }

            if (TrySampleLegacyRootTransform(clip, previewRig.transform, opt, out motionLocal))
            {
                source = MotionExtractSourceKind.LegacyRootTransform;
                return true;
            }

            failReason = BuildFailReason(clip, "Auto：RootT / 骨骼 / Animator / Legacy 均无有效位移。");
            return false;
        }

        if (previewRig == null && mode != MotionExtractMode.RootTCurve)
        {
            failReason = "请在 Hierarchy 选中预览 Rig（角色根节点）。";
            return false;
        }

        var ok = mode switch
        {
            MotionExtractMode.RootTCurve => TrySampleRootTCurves(clip, opt, out motionLocal),
            MotionExtractMode.SampleBone => TrySampleBonePath(clip, previewRig, opt, out motionLocal),
            MotionExtractMode.AnimatorRootMotion => TrySampleAnimatorRootMotion(clip, previewRig, opt, out motionLocal),
            MotionExtractMode.LegacyRootTransform => TrySampleLegacyRootTransform(clip, previewRig.transform, opt, out motionLocal),
            _ => false,
        };

        if (!ok)
        {
            failReason = BuildFailReason(clip, $"模式 {mode} 未采到有效位移。");
            return false;
        }

        source = ModeToSource(mode);
        return true;
    }

    static MotionExtractSourceKind ModeToSource(MotionExtractMode mode) => mode switch
    {
        MotionExtractMode.RootTCurve => MotionExtractSourceKind.RootTCurve,
        MotionExtractMode.SampleBone => MotionExtractSourceKind.SampleBone,
        MotionExtractMode.AnimatorRootMotion => MotionExtractSourceKind.AnimatorRootMotion,
        MotionExtractMode.LegacyRootTransform => MotionExtractSourceKind.LegacyRootTransform,
        _ => MotionExtractSourceKind.None,
    };

    static bool TrySampleRootTCurves(AnimationClip clip, Options opt, out Vector3[] motionLocal)
    {
        motionLocal = null;
        if (!ClipMotionRootTSampling.TryGetRootTCurves(clip, out var curves))
        {
            return false;
        }

        if (ClipMotionRootTSampling.SumAbsDisplacement(in curves, clip.length) < DisplacementEpsilon)
        {
            return false;
        }

        var frameCount = BuildFrameCount(clip, opt.SamplingFps);
        var rootSamples = new Vector3[frameCount];
        for (var i = 0; i < frameCount; i++)
        {
            var tSec = NormalizedToSeconds(i, frameCount, clip.length);
            rootSamples[i] = ClipMotionRootTSampling.Evaluate(in curves, tSec);
        }

        motionLocal = MapRootTSamplesToMotionLocal(rootSamples, opt.ZeroOrigin);
        return HasSignificantDisplacement(motionLocal);
    }

    static bool TrySampleBonePath(AnimationClip clip, GameObject rigRoot, Options opt, out Vector3[] motionLocal)
    {
        motionLocal = null;
        var rigT = rigRoot.transform;
        var bone = ResolveSampleBone(rigT, opt.SampleBonePath);
        if (bone == null)
        {
            return false;
        }

        var frameCount = BuildFrameCount(clip, opt.SamplingFps);
        var worldDeltas = new Vector3[frameCount];
        var wasActive = rigRoot.activeSelf;
        if (!wasActive)
        {
            rigRoot.SetActive(true);
        }

        try
        {
            for (var i = 0; i < frameCount; i++)
            {
                var tSec = NormalizedToSeconds(i, frameCount, clip.length);
                clip.SampleAnimation(rigRoot, tSec);
                worldDeltas[i] = rigT.InverseTransformVector(bone.position - rigT.position);
            }
        }
        finally
        {
            if (!wasActive)
            {
                rigRoot.SetActive(false);
            }
        }

        motionLocal = ProjectWorldDeltasToMotionLocal(worldDeltas, rigT, opt.ZeroOrigin);
        return HasSignificantDisplacement(motionLocal);
    }

    static bool TrySampleAnimatorRootMotion(AnimationClip clip, GameObject rigRoot, Options opt, out Vector3[] motionLocal)
    {
        motionLocal = null;
        var animator = rigRoot.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            return false;
        }

        var wasActive = rigRoot.activeSelf;
        var prevApplyRm = animator.applyRootMotion;
        if (!wasActive)
        {
            rigRoot.SetActive(true);
        }

        var frameCount = BuildFrameCount(clip, opt.SamplingFps);
        var worldDeltas = new Vector3[frameCount];
        var enteredAnimMode = false;

        try
        {
            animator.applyRootMotion = true;
            if (!AnimationMode.InAnimationMode())
            {
                AnimationMode.StartAnimationMode();
                enteredAnimMode = true;
            }

            AnimationMode.BeginSampling();
            for (var i = 0; i < frameCount; i++)
            {
                var tSec = NormalizedToSeconds(i, frameCount, clip.length);
                AnimationMode.SampleAnimationClip(animator.gameObject, clip, tSec);
                worldDeltas[i] = animator.rootPosition;
            }

            AnimationMode.EndSampling();
        }
        finally
        {
            animator.applyRootMotion = prevApplyRm;
            if (!wasActive)
            {
                rigRoot.SetActive(false);
            }

            if (enteredAnimMode && AnimationMode.InAnimationMode())
            {
                AnimationMode.StopAnimationMode();
            }
        }

        motionLocal = ProjectWorldDeltasToMotionLocal(worldDeltas, animator.transform, opt.ZeroOrigin);
        return HasSignificantDisplacement(motionLocal);
    }

    static bool TrySampleLegacyRootTransform(AnimationClip clip, Transform sampleRoot, Options opt, out Vector3[] motionLocal)
    {
        motionLocal = null;
        if (sampleRoot == null)
        {
            return false;
        }

        var frameCount = BuildFrameCount(clip, opt.SamplingFps);
        var local = new Vector3[frameCount];
        var wasActive = sampleRoot.gameObject.activeSelf;
        if (!wasActive)
        {
            sampleRoot.gameObject.SetActive(true);
        }

        try
        {
            for (var i = 0; i < frameCount; i++)
            {
                var tSec = NormalizedToSeconds(i, frameCount, clip.length);
                clip.SampleAnimation(sampleRoot.gameObject, tSec);
                local[i] = sampleRoot.localPosition;
            }
        }
        finally
        {
            if (!wasActive)
            {
                sampleRoot.gameObject.SetActive(false);
            }
        }

        motionLocal = MapRootTSamplesToMotionLocal(local, opt.ZeroOrigin);
        return HasSignificantDisplacement(motionLocal);
    }

    static Vector3[] MapRootTSamplesToMotionLocal(Vector3[] rootSpaceSamples, bool zeroOrigin)
    {
        var origin = zeroOrigin && rootSpaceSamples.Length > 0 ? rootSpaceSamples[0] : Vector3.zero;
        var result = new Vector3[rootSpaceSamples.Length];
        for (var i = 0; i < rootSpaceSamples.Length; i++)
        {
            result[i] = rootSpaceSamples[i] - origin;
        }

        return result;
    }

    static Vector3[] ProjectWorldDeltasToMotionLocal(Vector3[] worldPositions, Transform reference, bool zeroOrigin)
    {
        var up = Vector3.up;
        var forward = Vector3.ProjectOnPlane(reference.forward, up);
        if (forward.sqrMagnitude < 1e-6f)
        {
            forward = Vector3.forward;
        }
        else
        {
            forward.Normalize();
        }

        var right = Vector3.Cross(up, forward);
        var origin = zeroOrigin && worldPositions.Length > 0 ? worldPositions[0] : Vector3.zero;
        var result = new Vector3[worldPositions.Length];
        for (var i = 0; i < worldPositions.Length; i++)
        {
            var delta = worldPositions[i] - origin;
            result[i] = new Vector3(
                Vector3.Dot(delta, right),
                Vector3.Dot(delta, up),
                Vector3.Dot(delta, forward));
        }

        return result;
    }

    static bool ExtractPerAxis(GameObject previewRig, MotionProfileSO target, Options opt)
    {
        Vector3[] unified = null;
        MotionExtractSourceKind unifiedSource = MotionExtractSourceKind.None;
        var primary = ResolvePrimaryClip(opt);
        var needUnified = !target.UsesPerAxisSourceClips
            && (opt.XSource == MotionAxisExtractSource.Auto
                || opt.YSource == MotionAxisExtractSource.Auto
                || opt.ZSource == MotionAxisExtractSource.Auto);
        if (needUnified)
        {
            string diag = null;
            if (primary == null || !TrySampleDisplacement(primary, previewRig, opt, out unified, out unifiedSource, out diag))
            {
                LogFail(primary, target, diag ?? "统一采样失败");
                return false;
            }
        }

        Undo.RecordObject(target, "Extract Motion From Clip");
        var fitSettings = BuildFitSettings(opt);
        var rx = default(MotionCurveFitPipeline.Result);
        var ry = default(MotionCurveFitPipeline.Result);
        var rz = default(MotionCurveFitPipeline.Result);
        var maxMeters = Vector3.zero;
        var srcX = MotionExtractSourceKind.Manual;
        var srcY = MotionExtractSourceKind.Manual;
        var srcZ = MotionExtractSourceKind.Manual;
        var wroteAny = false;

        if ((opt.Axes & AxisMask.X) != 0)
        {
            wroteAny |= TryWriteAxis(
                previewRig, target, opt, 0, opt.XSource, unified, unifiedSource,
                fitSettings, ref target.AxisCurves.XCurve, ref target.AxisCurves.XScale,
                out maxMeters.x, out srcX, out rx);
        }

        if ((opt.Axes & AxisMask.Y) != 0)
        {
            wroteAny |= TryWriteAxis(
                previewRig, target, opt, 1, opt.YSource, unified, unifiedSource,
                fitSettings, ref target.AxisCurves.YCurve, ref target.AxisCurves.YScale,
                out maxMeters.y, out srcY, out ry);
        }

        if ((opt.Axes & AxisMask.Z) != 0)
        {
            wroteAny |= TryWriteAxis(
                previewRig, target, opt, 2, opt.ZSource, unified, unifiedSource,
                fitSettings, ref target.AxisCurves.ZCurve, ref target.AxisCurves.ZScale,
                out maxMeters.z, out srcZ, out rz);
        }

        if (!wroteAny)
        {
            LogFail(primary, target, "三轴均为 Manual，未写入任何曲线。");
            return false;
        }

        if (maxMeters.x + maxMeters.y + maxMeters.z < DisplacementEpsilon
            && !HasManualOnly(opt))
        {
            LogFail(primary, target, "按轴提取后三轴幅度仍 < ε");
            return false;
        }

        var reportSamples = Mathf.Max(
            rx.RawSampleCount,
            Mathf.Max(ry.RawSampleCount, rz.RawSampleCount));

        LastReport = new ExtractReport
        {
            Succeeded = true,
            Source = unifiedSource,
            SourceX = srcX,
            SourceY = srcY,
            SourceZ = srcZ,
            RawSamples = reportSamples,
            KeysX = rx.OutputKeyCount,
            KeysY = ry.OutputKeyCount,
            KeysZ = rz.OutputKeyCount,
            MaxAbsMeters = maxMeters,
            Quality = new Vector3(rx.ApproximationQuality, ry.ApproximationQuality, rz.ApproximationQuality),
            Message = $"per-axis X={srcX} Y={srcY} Z={srcZ}",
        };

        EditorUtility.SetDirty(target);
        var clipLabel = primary != null ? primary.name : target.name;
        Debug.Log(
            $"[MotionXYZ][Extract] OK {clipLabel} → {target.name} per-axis " +
            $"src=({srcX},{srcY},{srcZ}) max=({maxMeters.x:F2},{maxMeters.y:F2},{maxMeters.z:F2})m " +
            $"keys=({rx.OutputKeyCount},{ry.OutputKeyCount},{rz.OutputKeyCount}) " +
            $"quality=({LastReport.Quality.x:P0},{LastReport.Quality.y:P0},{LastReport.Quality.z:P0}) fit={opt.FitMode}");
        return true;
    }

    static bool HasManualOnly(Options opt) =>
        (opt.XSource == MotionAxisExtractSource.Manual || (opt.Axes & AxisMask.X) == 0)
        && (opt.YSource == MotionAxisExtractSource.Manual || (opt.Axes & AxisMask.Y) == 0)
        && (opt.ZSource == MotionAxisExtractSource.Manual || (opt.Axes & AxisMask.Z) == 0);

    static bool TryWriteAxis(
        GameObject previewRig,
        MotionProfileSO target,
        Options opt,
        int axisIndex,
        MotionAxisExtractSource axisSource,
        Vector3[] unified,
        MotionExtractSourceKind unifiedSource,
        MotionCurveFitPipeline.Settings fitSettings,
        ref AnimationCurve curve,
        ref float scale,
        out float maxAbs,
        out MotionExtractSourceKind sourceKind,
        out MotionCurveFitPipeline.Result fitResult)
    {
        fitResult = default;
        maxAbs = 0f;
        sourceKind = MotionExtractSourceKind.Manual;

        if (axisSource == MotionAxisExtractSource.Manual)
        {
            return false;
        }

        if (axisSource == MotionAxisExtractSource.ConstantZero)
        {
            curve = AnimationCurve.Constant(0f, 0f, 1f);
            scale = 0f;
            sourceKind = MotionExtractSourceKind.ConstantZero;
            fitResult.ApproximationQuality = 1f;
            fitResult.OutputKeyCount = 2;
            fitResult.RawSampleCount = 2;
            return true;
        }

        var axisClip = ResolveClipForAxis(opt, axisIndex);
        if (axisClip == null)
        {
            return false;
        }

        var frameCount = BuildFrameCount(axisClip, opt.SamplingFps);
        var times = BuildNormalizedTimes(frameCount);
        float[] raw;

        if (axisSource == MotionAxisExtractSource.Auto && unified != null && !target.UsesPerAxisSourceClips)
        {
            raw = ExtractComponent(unified, axisIndex);
            sourceKind = unifiedSource;
            fitResult.RawSampleCount = unified.Length;
        }
        else if (axisSource == MotionAxisExtractSource.Auto)
        {
            if (!TrySampleDisplacement(axisClip, previewRig, opt, out var motion, out sourceKind, out _))
            {
                return false;
            }

            raw = ExtractComponent(motion, axisIndex);
            fitResult.RawSampleCount = motion.Length;
        }
        else if (!TrySampleAxisBySource(axisClip, previewRig, opt, axisSource, out var motion, out sourceKind))
        {
            return false;
        }
        else
        {
            raw = ExtractComponent(motion, axisIndex);
            fitResult.RawSampleCount = motion.Length;
        }

        maxAbs = ComputeMaxAbs(raw);
        if (maxAbs < DisplacementEpsilon)
        {
            return false;
        }

        var vals = (float[])raw.Clone();
        if (opt.NormalizeCurvesTo01)
        {
            for (var i = 0; i < vals.Length; i++)
            {
                vals[i] /= maxAbs;
            }
        }

        curve = MotionCurveFitPipeline.BuildCurve(times, vals, in fitSettings, out fitResult);
        scale = opt.NormalizeCurvesTo01 ? maxAbs : 1f;
        return true;
    }

    static bool TrySampleAxisBySource(
        AnimationClip clip,
        GameObject previewRig,
        Options opt,
        MotionAxisExtractSource axisSource,
        out Vector3[] motionLocal,
        out MotionExtractSourceKind kind)
    {
        var mode = AxisSourceToMode(axisSource);
        var localOpt = opt;
        localOpt.ExtractMode = mode;
        return TrySampleDisplacement(clip, previewRig, localOpt, out motionLocal, out kind, out _);
    }

    static MotionExtractMode AxisSourceToMode(MotionAxisExtractSource source) => source switch
    {
        MotionAxisExtractSource.RootT => MotionExtractMode.RootTCurve,
        MotionAxisExtractSource.SampleBone => MotionExtractMode.SampleBone,
        MotionAxisExtractSource.AnimatorRootMotion => MotionExtractMode.AnimatorRootMotion,
        MotionAxisExtractSource.LegacyRootTransform => MotionExtractMode.LegacyRootTransform,
        _ => MotionExtractMode.Auto,
    };

    static float[] ExtractComponent(Vector3[] motion, int axisIndex) => axisIndex switch
    {
        0 => ExtractComponentX(motion),
        1 => ExtractComponentY(motion),
        _ => ExtractComponentZ(motion),
    };

    static float[] ExtractComponentX(Vector3[] motion)
    {
        var a = new float[motion.Length];
        for (var i = 0; i < motion.Length; i++)
        {
            a[i] = motion[i].x;
        }

        return a;
    }

    static float[] ExtractComponentY(Vector3[] motion)
    {
        var a = new float[motion.Length];
        for (var i = 0; i < motion.Length; i++)
        {
            a[i] = motion[i].y;
        }

        return a;
    }

    static float[] ExtractComponentZ(Vector3[] motion)
    {
        var a = new float[motion.Length];
        for (var i = 0; i < motion.Length; i++)
        {
            a[i] = motion[i].z;
        }

        return a;
    }

    static float ComputeMaxAbs(float[] values)
    {
        var max = 0f;
        for (var i = 0; i < values.Length; i++)
        {
            max = Mathf.Max(max, Mathf.Abs(values[i]));
        }

        return max;
    }

    static float[] BuildNormalizedTimes(int frameCount)
    {
        var times = new float[frameCount];
        for (var i = 0; i < frameCount; i++)
        {
            times[i] = frameCount <= 1 ? 0f : (float)i / (frameCount - 1);
        }

        return times;
    }

    static MotionCurveFitPipeline.Settings BuildFitSettings(Options opt) => new()
    {
        FitMode = opt.FitMode,
        FilterMode = opt.FilterMode,
        FilterWindow = opt.FilterWindow,
        ErrorTolerance = opt.ErrorTolerance,
    };

    static bool WriteCurves(
        AnimationClip clip,
        MotionProfileSO target,
        Options opt,
        Vector3[] motionLocal,
        MotionExtractSourceKind source)
    {
        var frameCount = motionLocal.Length;
        var times = new float[frameCount];
        var valsX = new float[frameCount];
        var valsY = new float[frameCount];
        var valsZ = new float[frameCount];

        var maxX = 0f;
        var maxY = 0f;
        var maxZ = 0f;
        for (var i = 0; i < frameCount; i++)
        {
            times[i] = frameCount <= 1 ? 0f : (float)i / (frameCount - 1);
            var p = motionLocal[i];
            maxX = Mathf.Max(maxX, Mathf.Abs(p.x));
            maxY = Mathf.Max(maxY, Mathf.Abs(p.y));
            maxZ = Mathf.Max(maxZ, Mathf.Abs(p.z));
            valsX[i] = p.x;
            valsY[i] = p.y;
            valsZ[i] = p.z;
        }

        if (maxX + maxY + maxZ < DisplacementEpsilon)
        {
            LogFail(clip, target, $"source={source} 采样后三轴幅度仍 < {DisplacementEpsilon}");
            return false;
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

        var fitSettings = BuildFitSettings(opt);

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
            Succeeded = true,
            Source = source,
            SourceX = source,
            SourceY = source,
            SourceZ = source,
            RawSamples = frameCount,
            KeysX = rx.OutputKeyCount,
            KeysY = ry.OutputKeyCount,
            KeysZ = rz.OutputKeyCount,
            MaxAbsMeters = new Vector3(maxX, maxY, maxZ),
            Quality = new Vector3(rx.ApproximationQuality, ry.ApproximationQuality, rz.ApproximationQuality),
            Message = $"source={source}",
        };

        EditorUtility.SetDirty(target);
        Debug.Log(
            $"[MotionXYZ][Extract] OK {clip.name} → {target.name} source={source} raw={frameCount} " +
            $"max=({maxX:F2},{maxY:F2},{maxZ:F2})m scale=({target.AxisCurves.XScale:F2},{target.AxisCurves.YScale:F2},{target.AxisCurves.ZScale:F2})m " +
            $"keys=({rx.OutputKeyCount},{ry.OutputKeyCount},{rz.OutputKeyCount}) " +
            $"quality=({LastReport.Quality.x:P0},{LastReport.Quality.y:P0},{LastReport.Quality.z:P0}) fit={opt.FitMode}");
        return true;
    }

    static int BuildFrameCount(AnimationClip clip, int samplingFps)
    {
        var duration = Mathf.Max(clip.length, 1f / samplingFps);
        return Mathf.Max(2, Mathf.RoundToInt(duration * samplingFps));
    }

    static float NormalizedToSeconds(int index, int frameCount, float clipLength)
    {
        var tNorm = frameCount <= 1 ? 0f : (float)index / (frameCount - 1);
        return tNorm * clipLength;
    }

    static bool HasSignificantDisplacement(Vector3[] motionLocal)
    {
        if (motionLocal == null || motionLocal.Length == 0)
        {
            return false;
        }

        var maxX = 0f;
        var maxY = 0f;
        var maxZ = 0f;
        for (var i = 0; i < motionLocal.Length; i++)
        {
            maxX = Mathf.Max(maxX, Mathf.Abs(motionLocal[i].x));
            maxY = Mathf.Max(maxY, Mathf.Abs(motionLocal[i].y));
            maxZ = Mathf.Max(maxZ, Mathf.Abs(motionLocal[i].z));
        }

        return maxX + maxY + maxZ >= DisplacementEpsilon;
    }

    static Transform ResolveSampleBone(Transform rigRoot, string bonePath)
    {
        if (rigRoot == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(bonePath))
        {
            var t = rigRoot.Find(bonePath);
            if (t != null)
            {
                return t;
            }
        }

        return rigRoot.Find("Hips")
            ?? rigRoot.Find("mixamorig:Hips")
            ?? rigRoot.Find("Armature/Hips");
    }

    public static Transform ResolveSampleRoot(GameObject rigRoot, string bonePath)
    {
        if (rigRoot == null)
        {
            return null;
        }

        return ResolveSampleBone(rigRoot.transform, bonePath) ?? rigRoot.transform;
    }

    static string BuildFailReason(AnimationClip clip, string headline)
    {
        var sb = new StringBuilder(headline);
        if (clip != null)
        {
            var hasRootT = ClipMotionRootTSampling.TryGetRootTCurves(clip, out var curves);
            sb.Append($" | clip={clip.name} len={clip.length:F2}s bindings={AnimationUtility.GetCurveBindings(clip).Length}");
            sb.Append(hasRootT ? " RootT=present" : " RootT=missing");
            if (hasRootT)
            {
                sb.Append($" RootTΔ={ClipMotionRootTSampling.SumAbsDisplacement(in curves, clip.length):F3}");
            }
        }

        return sb.ToString();
    }

    static void LogFail(AnimationClip clip, MotionProfileSO target, string reason)
    {
        LastReport = new ExtractReport
        {
            Succeeded = false,
            Source = MotionExtractSourceKind.None,
            Message = reason,
        };

        var name = target != null ? target.name : "?";
        var clipName = clip != null ? clip.name : "?";
        Debug.LogWarning($"[MotionXYZ][Extract] FAIL {clipName} → {name} {reason}");
    }
}
#endif

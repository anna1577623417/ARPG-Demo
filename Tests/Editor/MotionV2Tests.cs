using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 174.2 — Motion Profile V2 验收测试。
/// 覆盖 §8 验收条目中可在 EditMode 自动化的部分。
/// </summary>
public sealed class MotionV2Tests
{
    // ─── §8.1 P0 核心 ─────────────────────────────────────────

    [Test]
    public void Default_Profile_AllV2SamplersReturn_V1Equivalents()
    {
        var p = ScriptableObject.CreateInstance<MotionProfileSO>();
        try
        {
            Assert.AreEqual(1f, p.SampleGravityWeight(0.5f), 0.001f, "默认 GravityWeight 应=1");
            Assert.AreEqual(1f, p.SampleFacingInputWeight(0.5f), 0.001f, "默认 FacingW=1");
            Assert.AreEqual(0f, p.SampleMoveInputWeight(0.5f), 0.001f, "默认 MoveW=0");
            Assert.AreEqual(1f, p.SampleTargetTrackingWeight(0.5f), 0.001f, "默认 TrackW=1");
            Assert.AreEqual(0f, p.SampleRootMotionBlend(0.5f), 0.001f, "默认 RM Blend=0");
            Assert.AreEqual(1f, p.SampleHitstopMultiplier(0.5f), 0.001f, "默认 HitstopMul=1");
        }
        finally
        {
            Object.DestroyImmediate(p);
        }
    }

    // ─── §8.2 互斥与边界 ─────────────────────────────────────

    [Test]
    public void GravityWeight_DefaultPolicy_ReturnsOne()
    {
        var p = ScriptableObject.CreateInstance<MotionProfileSO>();
        try
        {
            p.V2GravityWeightMode = GravityWeightMode.DefaultPolicy;
            p.V2GravityWeight = AnimationCurve.Constant(0f, 1f, 5f); // 配了曲线但模式 DefaultPolicy
            Assert.AreEqual(1f, p.SampleGravityWeight(0.5f), 0.001f, "DefaultPolicy 模式应忽略曲线");
        }
        finally
        {
            Object.DestroyImmediate(p);
        }
    }

    [Test]
    public void GravityWeight_CurveMode_ReadsCurve()
    {
        var p = ScriptableObject.CreateInstance<MotionProfileSO>();
        try
        {
            p.V2GravityWeightMode = GravityWeightMode.Curve;
            p.V2GravityWeight = AnimationCurve.Constant(0f, 1f, 3f);
            Assert.AreEqual(3f, p.SampleGravityWeight(0.5f), 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(p);
        }
    }

    [Test]
    public void Composer_GravityWeight_Scales_GravityVy()
    {
        var config = new MotionYAxisConfig(YMotionMode.Curve, GravityMode.UseGravity, GroundConstraintMode.None);
        var motion = new MotionContribution
        {
            IsActive = true,
            YAxisConfig = config,
            HasGravityWeightOverride = true,
            GravityWeightOverride = 2f,
        };
        var gravity = new GravityContribution { Vy = -1f, IsActive = true };
        var final = MotionComposer.ComposeWorldVelocity(in motion, in gravity, new Vector3(0f, 0f, 0f), in config);
        Assert.AreEqual(-2f, final.y, 0.001f, "GravityWeight=2 应将 -1 重力放大为 -2");
    }

    [Test]
    public void Composer_GravityWeight_Zero_SuspendsGravity()
    {
        var config = new MotionYAxisConfig(YMotionMode.Curve, GravityMode.UseGravity, GroundConstraintMode.None);
        var motion = new MotionContribution
        {
            IsActive = true,
            YAxisConfig = config,
            HasGravityWeightOverride = true,
            GravityWeightOverride = 0f,
        };
        var gravity = new GravityContribution { Vy = -9.8f, IsActive = true };
        var final = MotionComposer.ComposeWorldVelocity(in motion, in gravity, new Vector3(0f, 2f, 0f), in config);
        Assert.AreEqual(2f, final.y, 0.001f, "GravityWeight=0 应等效 Suspend");
    }

    [Test]
    public void Composer_DefaultMotion_NoOverride_BehavesLikeV1()
    {
        var config = new MotionYAxisConfig(YMotionMode.None, GravityMode.UseGravity, GroundConstraintMode.None);
        var motion = new MotionContribution { IsActive = true, YAxisConfig = config };
        var gravity = new GravityContribution { Vy = -2f, IsActive = true };
        var final = MotionComposer.ComposeWorldVelocity(in motion, in gravity, Vector3.forward, in config);
        Assert.AreEqual(-2f, final.y, 0.001f, "未启用 V2 时 GravityWeight=1，行为与 V1 完全一致");
    }

    [Test]
    public void Contribution_Resolvers_DefaultValues()
    {
        var inactive = MotionContribution.Inactive;
        Assert.AreEqual(1f, inactive.ResolvedGravityWeight, 0.001f);
        Assert.AreEqual(1f, inactive.ResolvedFacingInputWeight, 0.001f);
        Assert.AreEqual(0f, inactive.ResolvedMoveInputWeight, 0.001f);
        Assert.AreEqual(1f, inactive.ResolvedHitstopMultiplier, 0.001f);
    }

    [Test]
    public void Contribution_GravityOverride_Zero_IsDistinguishedFromUnset()
    {
        var unset = new MotionContribution { IsActive = true };
        var explicitZero = new MotionContribution
        {
            IsActive = true,
            HasGravityWeightOverride = true,
            GravityWeightOverride = 0f,
        };
        Assert.AreEqual(1f, unset.ResolvedGravityWeight, 0.001f);
        Assert.AreEqual(0f, explicitZero.ResolvedGravityWeight, 0.001f);
    }

    // ─── §8 YStrategy ────────────────────────────────────────

    [Test]
    public void YStrategy_HoverHold_HoldsPeak_BetweenPeakAndRelease()
    {
        var p = ScriptableObject.CreateInstance<MotionProfileSO>();
        try
        {
            p.AxisCurves.YCurve = MotionCurveLibrary.Make(MotionCurveLibrary.Segment.ApexHold);
            p.AxisCurves.YScale = 5f;
            p.V2YStrategy = YStrategyV2.HoverHold;

            // ApexHold 峰值在 t=0.25~0.75；策略 release 在 0.75
            // 在 t=0.5（介于 peak 与 release）应仍持平
            Assert.AreEqual(5f, p.SampleV2LocalYPosition(0.5f), 0.01f);

            // t=0.9（release 之后）应回落，远小于 5
            Assert.Less(p.SampleV2LocalYPosition(0.9f), 5f);
        }
        finally
        {
            Object.DestroyImmediate(p);
        }
    }

    [Test]
    public void YStrategy_Default_DoesNotChange_RawCurve()
    {
        var p = ScriptableObject.CreateInstance<MotionProfileSO>();
        try
        {
            p.AxisCurves.YCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            p.AxisCurves.YScale = 2f;
            p.V2YStrategy = YStrategyV2.Default;
            Assert.AreEqual(1f, p.SampleV2LocalYPosition(0.5f), 0.01f);
        }
        finally
        {
            Object.DestroyImmediate(p);
        }
    }

    // ─── CurveLibrary ───────────────────────────────────────

    [Test]
    public void CurveLibrary_AllSegments_ProduceNonNullCurves()
    {
        foreach (MotionCurveLibrary.Segment seg in System.Enum.GetValues(typeof(MotionCurveLibrary.Segment)))
        {
            var curve = MotionCurveLibrary.Make(seg);
            Assert.IsNotNull(curve, $"{seg} 曲线为空");
            Assert.GreaterOrEqual(curve.length, 1, $"{seg} 至少一个关键帧");
            Assert.IsNotEmpty(MotionCurveLibrary.GetUseCase(seg), $"{seg} 缺少用例文档");
        }
    }
}

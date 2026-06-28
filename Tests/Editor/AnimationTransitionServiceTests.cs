using NUnit.Framework;
using UnityEngine;

/// <summary>178.1 — AnimationTransitionService 四层降级 + PoseCost 验收测试。</summary>
public sealed class AnimationTransitionServiceTests
{
    TransitionTagMatrixSO _matrix;
    TransitionConfigSO _config;

    [SetUp]
    public void Setup()
    {
        _matrix = ScriptableObject.CreateInstance<TransitionTagMatrixSO>();
        // 用默认 12×12 表填充
        var tbl = TransitionTagMatrixSO.DefaultBlendTable;
        for (var i = 0; i < TransitionTagMatrixSO.TagCount; i++)
        for (var j = 0; j < TransitionTagMatrixSO.TagCount; j++)
        {
            _matrix.SetEntry((TransitionTag)i, (TransitionTag)j, new TransitionTagMatrixSO.Entry
            {
                BlendDuration = tbl[i, j],
                Mode = TransitionMode.CrossFade,
                EaseCurve = null,
            });
        }

        _config = ScriptableObject.CreateInstance<TransitionConfigSO>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_matrix != null) Object.DestroyImmediate(_matrix);
        if (_config != null) Object.DestroyImmediate(_config);
    }

    // ─── ① Override 层 ──────────────────────────────────────

    [Test]
    public void Resolve_OverrideAction_HitsOverrideLayer()
    {
        var to = ScriptableObject.CreateInstance<ActionDataSO>();
        try
        {
            to.OverrideIncomingTransition = true;
            to.OverrideBlendDuration = 0.25f;
            to.OverrideTransitionMode = TransitionMode.CrossFade;

            var svc = new AnimationTransitionService(_matrix, null, _config);
            var p = svc.Resolve(null, to);
            Assert.AreEqual(TransitionResolveLayer.Override, p.Layer);
            Assert.AreEqual(0.25f, p.BlendDuration, 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(to);
        }
    }

    // ─── ③ Tag Matrix 层 ────────────────────────────────────

    [Test]
    public void Resolve_IdleToLocomotion_FromMatrix()
    {
        var from = MakeAction(TransitionTag.Idle);
        var to = MakeAction(TransitionTag.Locomotion);
        try
        {
            var svc = new AnimationTransitionService(_matrix, null, _config);
            var p = svc.Resolve(from, to);
            Assert.AreEqual(TransitionResolveLayer.TagMatrix, p.Layer);
            Assert.AreEqual(0.15f, p.BlendDuration, 0.001f);
            Assert.AreEqual(TransitionMode.CrossFade, p.Mode);
        }
        finally
        {
            Object.DestroyImmediate(from);
            Object.DestroyImmediate(to);
        }
    }

    [Test]
    public void Resolve_LocomotionToLocomotion_PhaseMatchAutoEnabled()
    {
        var from = MakeAction(TransitionTag.Locomotion);
        var to = MakeAction(TransitionTag.Locomotion);
        try
        {
            var svc = new AnimationTransitionService(_matrix, null, _config);
            var p = svc.Resolve(from, to);
            Assert.AreEqual(TransitionMode.PhaseMatch, p.Mode,
                "Loco→Loco 同 Tag 应自动升级到 PhaseMatch");
        }
        finally
        {
            Object.DestroyImmediate(from);
            Object.DestroyImmediate(to);
        }
    }

    [Test]
    public void Resolve_HitToAnything_SnapMode()
    {
        var from = MakeAction(TransitionTag.Hit);
        var to = MakeAction(TransitionTag.Idle);
        try
        {
            var svc = new AnimationTransitionService(_matrix, null, _config);
            var p = svc.Resolve(from, to);
            Assert.AreEqual(TransitionMode.Snap, p.Mode);
            Assert.AreEqual(0f, p.BlendDuration, 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(from);
            Object.DestroyImmediate(to);
        }
    }

    [Test]
    public void Resolve_CrouchToCrouch_PhaseMatch()
    {
        var from = MakeAction(TransitionTag.Crouch);
        var to = MakeAction(TransitionTag.Crouch);
        try
        {
            var svc = new AnimationTransitionService(_matrix, null, _config);
            var p = svc.Resolve(from, to);
            Assert.AreEqual(TransitionMode.PhaseMatch, p.Mode);
        }
        finally
        {
            Object.DestroyImmediate(from);
            Object.DestroyImmediate(to);
        }
    }

    // ─── ④ Default 兜底层 ──────────────────────────────────

    [Test]
    public void Resolve_NullMatrix_FallsBackToDefault()
    {
        var to = MakeAction(TransitionTag.Locomotion);
        try
        {
            var svc = new AnimationTransitionService(null, null, _config);
            var p = svc.Resolve(null, to);
            Assert.AreEqual(TransitionResolveLayer.Default, p.Layer);
            Assert.AreEqual(_config.DefaultBlendDuration, p.BlendDuration, 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(to);
        }
    }

    [Test]
    public void Resolve_NullEverything_DoesNotCrash()
    {
        var to = MakeAction(TransitionTag.Idle);
        try
        {
            var svc = new AnimationTransitionService(null, null, null);
            var p = svc.Resolve(null, to);
            Assert.AreEqual(TransitionResolveLayer.Default, p.Layer);
            Assert.Greater(p.BlendDuration, 0f, "兜底必须 > 0");
        }
        finally
        {
            Object.DestroyImmediate(to);
        }
    }

    [Test]
    public void Resolve_NullTarget_ReturnsDefault()
    {
        var svc = new AnimationTransitionService(_matrix, null, _config);
        var p = svc.Resolve(null, null);
        Assert.AreEqual(TransitionResolveLayer.Default, p.Layer);
    }

    // ─── PoseCost & 推荐 Blend ──────────────────────────────

    [Test]
    public void RecommendedBlend_PoseCostZero_ReturnsMin()
    {
        var b = PoseCostCalculator.RecommendedBlend(0f);
        Assert.AreEqual(0.03f, b, 0.001f);
    }

    [Test]
    public void RecommendedBlend_PoseCostOne_ReturnsMax()
    {
        var b = PoseCostCalculator.RecommendedBlend(1f);
        Assert.AreEqual(0.20f, b, 0.001f);
    }

    [Test]
    public void RecommendedBlend_Monotonic_IncreasingWithCost()
    {
        var b0 = PoseCostCalculator.RecommendedBlend(0.0f);
        var b3 = PoseCostCalculator.RecommendedBlend(0.3f);
        var b5 = PoseCostCalculator.RecommendedBlend(0.5f);
        var b8 = PoseCostCalculator.RecommendedBlend(0.8f);
        Assert.Less(b0, b3);
        Assert.Less(b3, b5);
        Assert.Less(b5, b8);
    }

    [Test]
    public void PoseCost_IdenticalPose_ReturnsZero()
    {
        var pose = MakePose();
        var cost = PoseCostCalculator.ComputeCost(in pose, in pose);
        Assert.AreEqual(0f, cost, 0.001f);
    }

    [Test]
    public void PoseCost_RotatedHip_IncreasesCost()
    {
        var a = MakePose();
        var b = MakePose();
        b.LocalRot[(int)HumanBodyBones.Hips] = Quaternion.Euler(90f, 0f, 0f);
        var cost = PoseCostCalculator.ComputeCost(in a, in b);
        Assert.Greater(cost, 0.05f, "髋部 90° 偏差应产生显著 cost");
    }

    [Test]
    public void PoseCost_EmptyPose_ReturnsZero()
    {
        var empty = PoseSample.Empty;
        var cost = PoseCostCalculator.ComputeCost(in empty, in empty);
        Assert.AreEqual(0f, cost, 0.001f);
    }

    // ─── Default Matrix 数值断言（确认默认值未漂移）─────

    [Test]
    public void DefaultMatrix_HitRow_IsAllZero()
    {
        var tbl = TransitionTagMatrixSO.DefaultBlendTable;
        for (var j = 0; j < TransitionTagMatrixSO.TagCount; j++)
        {
            Assert.AreEqual(0f, tbl[(int)TransitionTag.Hit, j], 0.001f, $"Hit→{(TransitionTag)j} 应为 0 (Snap)");
        }
    }

    [Test]
    public void DefaultMatrix_LocoToLoco_HasReasonableBlend()
    {
        var blend = TransitionTagMatrixSO.DefaultBlendTable[
            (int)TransitionTag.Locomotion, (int)TransitionTag.Locomotion];
        Assert.Greater(blend, 0f);
        Assert.Less(blend, 0.20f, "Loco→Loco 应是合理小值");
    }

    [Test]
    public void IsPhaseMatchPair_LocoToLoco_True()
    {
        Assert.IsTrue(TransitionTagMatrixSO.IsPhaseMatchPair(
            TransitionTag.Locomotion, TransitionTag.Locomotion));
    }

    [Test]
    public void IsPhaseMatchPair_LocoToIdle_False()
    {
        Assert.IsFalse(TransitionTagMatrixSO.IsPhaseMatchPair(
            TransitionTag.Locomotion, TransitionTag.Idle));
    }

    // ─── Utility ──────────────────────────────────────────

    static ActionDataSO MakeAction(TransitionTag tag)
    {
        var a = ScriptableObject.CreateInstance<ActionDataSO>();
        a.TransitionTag = tag;
        return a;
    }

    static PoseSample MakePose()
    {
        var n = (int)HumanBodyBones.LastBone;
        var rots = new Quaternion[n];
        for (var i = 0; i < n; i++) rots[i] = Quaternion.identity;
        return new PoseSample
        {
            LocalRot = rots,
            HipPos = Vector3.zero,
            HipVel = Vector3.zero,
            FootPhase = 0f,
        };
    }
}

using NUnit.Framework;
using UnityEngine;

/// <summary>179.1 — Stride Matching EditMode 验收测试。</summary>
public sealed class StrideMatchingTests
{
    sealed class FakeMotor : IStrideMotor { public float HorizontalSpeed { get; set; } }
    sealed class FakeAnim : IStrideAnimDriver
    {
        public float LastSpeed = -1f;
        public void SetPlaybackSpeed(float ratio) => LastSpeed = ratio;
    }

    LocomotionRuntimeProfileSO _cfg;

    [SetUp]
    public void Setup()
    {
        _cfg = ScriptableObject.CreateInstance<LocomotionRuntimeProfileSO>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_cfg != null) Object.DestroyImmediate(_cfg);
    }

    // ─── 主流程 ─────────────────────────────────────────────

    [Test]
    public void Ratio_AtReferenceSpeed_IsOne()
    {
        var loop = MakeLoop(refDist: 4f, refDur: 0.8f); // refSpeed=5
        var motor = new FakeMotor { HorizontalSpeed = 5f };
        var anim = new FakeAnim();
        var rt = new LocomotionRuntime(_cfg, anim, motor);
        rt.OnEnterLoop(loop, LoopTier.Run);

        // 大 dt 让 EMA 一步到位
        rt.Tick(1f);

        Assert.AreEqual(1f, anim.LastSpeed, 0.01f);
        Object.DestroyImmediate(loop);
    }

    [Test]
    public void Ratio_BuffAccelerated_1_5x()
    {
        var loop = MakeLoop(refDist: 4f, refDur: 0.8f); // refSpeed=5
        loop.MaxPlaybackSpeed = 2f;
        var motor = new FakeMotor { HorizontalSpeed = 7.5f };
        var anim = new FakeAnim();
        var rt = new LocomotionRuntime(_cfg, anim, motor);
        rt.OnEnterLoop(loop, LoopTier.Run);

        rt.Tick(1f);

        Assert.AreEqual(1.5f, anim.LastSpeed, 0.01f, "Buff×1.5 应使动画 150% 步频");
        Object.DestroyImmediate(loop);
    }

    [Test]
    public void Ratio_DebuffSlowed_06x()
    {
        var loop = MakeLoop(refDist: 4f, refDur: 0.8f); // refSpeed=5
        loop.MinPlaybackSpeed = 0.5f;
        var motor = new FakeMotor { HorizontalSpeed = 3f };
        var anim = new FakeAnim();
        var rt = new LocomotionRuntime(_cfg, anim, motor);
        rt.OnEnterLoop(loop, LoopTier.Run);

        // 关闭自动 Tier 切换避免下降到 Walk
        loop.AutoTierSwitch = false;
        rt.Tick(1f);

        Assert.AreEqual(0.6f, anim.LastSpeed, 0.01f, "ratio=0.6 应被 MinPlaybackSpeed 钳制");
        Object.DestroyImmediate(loop);
    }

    [Test]
    public void Smoothing_FollowsMotor_GraduallyOverTau()
    {
        var loop = MakeLoop(refDist: 5f, refDur: 1f); // refSpeed=5
        loop.AutoTierSwitch = false;
        var motor = new FakeMotor { HorizontalSpeed = 5f };
        var anim = new FakeAnim();
        var rt = new LocomotionRuntime(_cfg, anim, motor);
        rt.OnEnterLoop(loop, LoopTier.Run);

        // tau=0.08；一帧 dt=0.016 → 进度约 18%
        rt.Tick(0.016f);
        Assert.Greater(rt.SmoothedSpeed, 0.5f);
        Assert.Less(rt.SmoothedSpeed, 1.5f);

        // 大 dt 全速到位
        rt.Tick(1f);
        Assert.AreEqual(5f, rt.SmoothedSpeed, 0.1f);

        Object.DestroyImmediate(loop);
    }

    // ─── Tier 切换 ──────────────────────────────────────────

    [Test]
    public void TierResolver_Walk_OverUpperBound_UpgradesToRun()
    {
        var next = TierResolver.Resolve(speed: 5f, current: LoopTier.Walk, _cfg);
        Assert.AreEqual(LoopTier.Run, next, "5 > Walk 上限 3 × 1.15");
    }

    [Test]
    public void TierResolver_Run_BelowLowerBound_DowngradesToWalk()
    {
        var next = TierResolver.Resolve(speed: 1f, current: LoopTier.Run, _cfg);
        Assert.AreEqual(LoopTier.Walk, next);
    }

    [Test]
    public void TierResolver_Sprint_BelowLowerBound_DowngradesToRun()
    {
        var next = TierResolver.Resolve(speed: 5f, current: LoopTier.Sprint, _cfg);
        Assert.AreEqual(LoopTier.Run, next);
    }

    [Test]
    public void TierResolver_Hysteresis_Stays()
    {
        // Run 区间 [2.5, 7.5]，迟滞 ±15%；6.5 在区间内 → 保持
        var next = TierResolver.Resolve(speed: 6.5f, current: LoopTier.Run, _cfg);
        Assert.AreEqual(LoopTier.Run, next);
    }

    [Test]
    public void Runtime_AutoTier_FiresTierSwitchEvent_OnUpgrade()
    {
        var loop = MakeLoop(refDist: 4f, refDur: 0.8f);
        var motor = new FakeMotor { HorizontalSpeed = 10f }; // 远超 Run 区间
        var anim = new FakeAnim();
        var rt = new LocomotionRuntime(_cfg, anim, motor);
        rt.OnEnterLoop(loop, LoopTier.Run);

        LocomotionTierSwitchEvent? captured = null;
        rt.TierSwitchRequested += e => captured = e;

        rt.Tick(1f);

        Assert.IsNotNull(captured);
        Assert.AreEqual(LoopTier.Run, captured.Value.From);
        Assert.AreEqual(LoopTier.Sprint, captured.Value.To);
        Object.DestroyImmediate(loop);
    }

    // ─── 边界 ──────────────────────────────────────────────

    [Test]
    public void MotionDrivenAction_DoesNotApply_Stride()
    {
        var loop = ScriptableObject.CreateInstance<ActionDataSO>();
        loop.MovementMode = MovementMode.MotionDriven;
        var motor = new FakeMotor { HorizontalSpeed = 100f };
        var anim = new FakeAnim();
        var rt = new LocomotionRuntime(_cfg, anim, motor);
        rt.OnEnterLoop(loop, LoopTier.Walk);

        rt.Tick(1f);
        Assert.AreEqual(-1f, anim.LastSpeed, "MotionDriven 不应写 SetPlaybackSpeed");
        Object.DestroyImmediate(loop);
    }

    [Test]
    public void MissingReferenceSpeed_FallsBackToOne()
    {
        var loop = ScriptableObject.CreateInstance<ActionDataSO>();
        loop.MovementMode = MovementMode.MotorDriven;
        loop.EnableStrideMatching = true;
        loop.ReferenceDistance = 0f;
        loop.ReferenceDuration = 0f;
        loop.MinPlaybackSpeed = 0.5f;

        var motor = new FakeMotor { HorizontalSpeed = 5f };
        var anim = new FakeAnim();
        var rt = new LocomotionRuntime(_cfg, anim, motor);
        rt.OnEnterLoop(loop, LoopTier.Run);

        rt.Tick(1f);
        Assert.AreEqual(1f, anim.LastSpeed, 0.001f, "烘焙缺失应 fallback playback=1");
        Object.DestroyImmediate(loop);
    }

    [Test]
    public void OnExitLoop_ResetsPlaybackToOne()
    {
        var loop = MakeLoop(refDist: 4f, refDur: 0.8f);
        loop.AutoTierSwitch = false;
        var motor = new FakeMotor { HorizontalSpeed = 10f };
        var anim = new FakeAnim();
        var rt = new LocomotionRuntime(_cfg, anim, motor);
        rt.OnEnterLoop(loop, LoopTier.Run);
        rt.Tick(1f);
        Assert.Greater(anim.LastSpeed, 1f);

        rt.OnExitLoop();
        Assert.AreEqual(1f, anim.LastSpeed);
        Assert.IsFalse(rt.IsActive);
        Object.DestroyImmediate(loop);
    }

    // ─── Utility ──────────────────────────────────────────

    static ActionDataSO MakeLoop(float refDist, float refDur)
    {
        var a = ScriptableObject.CreateInstance<ActionDataSO>();
        a.MovementMode = MovementMode.MotorDriven;
        a.EnableStrideMatching = true;
        a.ReferenceDistance = refDist;
        a.ReferenceDuration = refDur;
        a.MinPlaybackSpeed = 0.6f;
        a.MaxPlaybackSpeed = 1.4f;
        a.AutoTierSwitch = true;
        return a;
    }
}

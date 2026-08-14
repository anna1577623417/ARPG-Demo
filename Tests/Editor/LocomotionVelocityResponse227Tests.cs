using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class LocomotionVelocityResponse227Tests
{
    static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

    static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(ProjectRoot, "Assets/GameMain/Scripts", relativePath));

    static readonly LocomotionVelocityResponse.Settings Settings =
        new LocomotionVelocityResponse.Settings(
            riseTime: 0.2f,
            releaseTime: 0.12f,
            turnTime: 0.09f,
            reverseTime: 0.16f,
            startSpeedFloorRatio: 0.25f);

    [Test]
    public void Start_FirstTickProvidesConfiguredSpeedFloor()
    {
        var result = LocomotionVelocityResponse.Resolve(
            Vector3.zero,
            Vector3.forward,
            6f,
            1f / 60f,
            in Settings);

        Assert.AreEqual(LocomotionVelocityResponse.Branch.Start, result.ResponseBranch);
        Assert.That(result.Velocity.magnitude, Is.EqualTo(1.5f).Within(0.0001f));
        Assert.Greater(Vector3.Dot(result.Velocity.normalized, Vector3.forward), 0.999f);
    }

    [Test]
    public void NinetyDegreeTurn_FirstTickUsesCommandDirectionAndKeepsSpeedMagnitude()
    {
        var result = LocomotionVelocityResponse.Resolve(
            Vector3.forward * 6f,
            Vector3.right,
            6f,
            1f / 60f,
            in Settings);

        Assert.AreEqual(LocomotionVelocityResponse.Branch.Turn, result.ResponseBranch);
        Assert.That(Vector3.Angle(result.Velocity, Vector3.right), Is.LessThan(0.01f));
        Assert.That(result.Velocity.magnitude, Is.EqualTo(6f).Within(0.0001f));
        Assert.That(result.LateralAfter, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void Reverse_FirstTickUsesCommandDirectionWhileMagnitudeBrakes()
    {
        var result = LocomotionVelocityResponse.Resolve(
            Vector3.forward * 6f,
            Vector3.back,
            6f,
            1f / 60f,
            in Settings);

        Assert.AreEqual(LocomotionVelocityResponse.Branch.ReverseBrake, result.ResponseBranch);
        Assert.That(Vector3.Angle(result.Velocity, Vector3.back), Is.LessThan(0.01f));
        Assert.Less(result.Velocity.magnitude, 6f);
        Assert.Greater(result.Velocity.magnitude, 0f);
    }

    [Test]
    public void Release_ConvergesTowardZero()
    {
        var result = LocomotionVelocityResponse.Resolve(
            Vector3.forward * 2.4f,
            Vector3.zero,
            0f,
            1f / 60f,
            in Settings);

        Assert.AreEqual(LocomotionVelocityResponse.Branch.Release, result.ResponseBranch);
        Assert.That(result.Velocity.magnitude, Is.LessThan(2.4f));
        Assert.That(result.Velocity.magnitude, Is.GreaterThan(0f));
    }

    [Test]
    public void AbilityContext_MoveDownDoesNotDelayLocomotionFacing()
    {
        var context = new InputContextResolver();
        context.SetLoadoutHasDirectionalModifier(true);

        context.TickMoveContext(Vector2.right, 0.12f, 10f, Vector3.forward, 0.10f);

        Assert.AreEqual(RotationArbitrationPolicy.Immediate, context.ResolvePolicy(10f));
        Assert.IsFalse(context.ShouldSuppressLocomotionRotation(10f));
        Assert.IsTrue(context.TryGetMoveDownPlanarForward(out var captured));
        Assert.That(Vector3.Angle(captured, Vector3.forward), Is.LessThan(0.01f));
    }

    [Test]
    public void DirectionalCommitFreezesFacingAndKeepsMoveDownBasis()
    {
        var context = new InputContextResolver();
        context.SetLoadoutHasDirectionalModifier(true);
        context.TickMoveContext(Vector2.right, 0.12f, 20f, Vector3.forward, 0.10f);
        context.TickMoveContext(Vector2.right, 0.12f, 20.05f, Vector3.right, 0.10f);

        Assert.IsTrue(context.TryGetMoveDownPlanarForward(out var captured));
        Assert.That(Vector3.Angle(captured, Vector3.forward), Is.LessThan(0.01f),
            "持续 WASD 不得覆盖 MoveDown 技能基准快照");

        context.CommitDirectionalAbility(captured, Vector3.right, 0.05f, 0.12f, "test");
        Assert.AreEqual(RotationArbitrationPolicy.FrozenDuringDirectionalAction, context.ResolvePolicy(20.05f));
        Assert.IsTrue(context.ShouldSuppressLocomotionRotation(20.05f));
    }

    [Test]
    public void FreeLocomotionProductionPathHasNoHorizontalRotateTowards()
    {
        var player = Read("3_Gameplay/Characters/Player/Core/Player.cs");
        var visual = Read("3_Gameplay/Characters/Player/Presentation/VisualFacingDriver.cs");

        Assert.IsFalse(player.Contains("Vector3.RotateTowards("),
            "FreeLocomotion LogicFacing 不得恢复水平角速度积分");
        Assert.IsFalse(visual.Contains("Quaternion.RotateTowards("),
            "普通 VisualRoot 不得恢复世界 Yaw 缓追");
    }

    [Test]
    public void TapReleaseDoesNotDispatchLegacyTapFacing()
    {
        var controller = Read("3_Gameplay/Characters/Player/Core/PlayerController.cs");
        var tapBranch = controller.IndexOf("if (tense == InputTense.Tap)", System.StringComparison.Ordinal);
        var noInputBranch = controller.IndexOf("if (!hasMoveInput)", tapBranch, System.StringComparison.Ordinal);
        Assert.GreaterOrEqual(tapBranch, 0);
        Assert.Greater(noInputBranch, tapBranch);

        var body = controller.Substring(tapBranch, noInputBranch - tapBranch);
        Assert.IsFalse(body.Contains("HandleTapFacing"));
        Assert.IsFalse(body.Contains("ArmTapTurnPresentation"));
        Assert.IsTrue(body.Contains("ClearMovementIntent"));
        Assert.IsFalse(controller.Contains("void HandleTapFacing("),
            "Legacy TapFacing 方法必须退出生产代码，避免未来误接回 release 边沿");
    }

    [Test]
    public void LocomotionStateRejectsTurnPresentationDuringMovementSession()
    {
        var state = Read("3_Gameplay/Characters/Player/States/PlayerLocomotionState.cs");
        StringAssert.Contains("player.HasMovementIntent || player.ShouldSuppressLocomotionRotation()", state);
        StringAssert.Contains("free_locomotion_immediate", state);
    }

    [TestCase(59f, TurnType.None)]
    [TestCase(60f, TurnType.Turn90)]
    [TestCase(134f, TurnType.Turn90)]
    [TestCase(135f, TurnType.Turn180)]
    [TestCase(-90f, TurnType.Turn90)]
    [TestCase(-170f, TurnType.Turn180)]
    public void TurnCompensationResolver_ClassifiesPreSnapAngle(float signedAngle, TurnType expected)
    {
        var command = Quaternion.Euler(0f, signedAngle, 0f) * Vector3.forward;
        Assert.IsTrue(TurnCompensationResolver.TryResolve(
            Vector3.forward,
            command,
            enabled: true,
            isLockOn: false,
            directionalCommitted: false,
            turn90ThresholdDeg: 60f,
            turn180ThresholdDeg: 135f,
            generation: 1U,
            sourceFrame: 10,
            out var cue));

        Assert.AreEqual(expected, cue.Type);
        Assert.AreEqual(expected != TurnType.None, cue.IsTurning);
        if (expected != TurnType.None)
        {
            Assert.AreEqual(signedAngle < 0f ? -1 : 1, cue.Direction);
            Assert.Greater(cue.PresentationLeaseSeconds, 0f);
        }
    }

    [Test]
    public void TurnCompensationResolver_SuppressesLockOnAndDirectionalCommit()
    {
        Assert.IsFalse(TurnCompensationResolver.TryResolve(
            Vector3.forward, Vector3.right, true, true, false, 60f, 135f, 1U, 1, out _));
        Assert.IsFalse(TurnCompensationResolver.TryResolve(
            Vector3.forward, Vector3.right, true, false, true, 60f, 135f, 1U, 1, out _));
    }

    [Test]
    public void TurnCompensationLease_UsesClipLengthSpeedAndBoundedRatio()
    {
        Assert.That(
            TurnCompensationResolver.ResolveLeaseSeconds(1.2f, 1.5f, 0.75f),
            Is.EqualTo(0.6f).Within(0.0001f));
    }

    [Test]
    public void TurnCompensationCue_ExpiresBeforeItCanBecomeDelayedSecondTurn()
    {
        var cue = new TurnCompensationCue(
            3U,
            TurnType.Turn180,
            1,
            180f,
            180f,
            sourceFrame: 100,
            presentationLeaseSeconds: 0.24f);

        Assert.IsTrue(TurnCompensationResolver.IsCueFresh(in cue, 103));
        Assert.IsFalse(TurnCompensationResolver.IsCueFresh(in cue, 104));
    }

    [Test]
    public void TurnCompensationTurnInMotion_UsesTypedLeaseAndFitsPlaybackSpeed()
    {
        Assert.IsTrue(TurnCompensationResolver.TryResolve(
            Vector3.forward,
            Vector3.right,
            true,
            false,
            false,
            60f,
            135f,
            7U,
            10,
            0.25f,
            0.40f,
            out var turn90));
        Assert.That(turn90.PresentationLeaseSeconds, Is.EqualTo(0.25f).Within(0.0001f));

        Assert.IsTrue(TurnCompensationResolver.TryResolve(
            Vector3.forward,
            Vector3.back,
            true,
            false,
            false,
            60f,
            135f,
            8U,
            11,
            0.25f,
            0.40f,
            out var turn180));
        Assert.That(turn180.PresentationLeaseSeconds, Is.EqualTo(0.40f).Within(0.0001f));
        Assert.That(
            TurnCompensationResolver.ResolvePlaybackSpeedForLease(1.0f, 1f, 0.7f, 0.35f),
            Is.EqualTo(2f).Within(0.0001f));
    }

    [Test]
    public void MovingTurnCompensationDoesNotRestoreLegacyTurnResolverOrRootRotation()
    {
        var state = Read("3_Gameplay/Characters/Player/States/PlayerLocomotionState.cs");
        var player = Read("3_Gameplay/Characters/Player/Core/Player.cs");
        var anim = Read("5_Presentation/Animation/Controllers/PlayerAnimController.cs");

        StringAssert.Contains("补偿动画改走一次性 TurnCompensationCue", state);
        StringAssert.Contains("SubmitTurnCompensationCommand", player);
        StringAssert.Contains("TryGetTurnCompensationCueAfter", anim);
        Assert.IsFalse(player.Contains("Vector3.RotateTowards("));
    }

    [Test]
    public void TurnCompensation_ReplaysSameClipAndKeepsLibraryFallback()
    {
        var anim = Read("5_Presentation/Animation/Controllers/PlayerAnimController.cs");

        StringAssert.Contains(
            "restartIfSameClip: true, requestSource: \"Locomotion.ProfileTurn\"",
            anim,
            "新的 Cue generation 即使选中同一 Turn Clip 也必须从头重播");
        StringAssert.Contains(
            "restartIfSameClip: IsTurnSub(target)",
            anim,
            "AnimLibrary Turn fallback 同样必须允许同 Clip 重播");
        StringAssert.Contains(
            "&& turnBinding.TryGetContinuousPresentation(",
            anim,
            "Profile 绑定无有效表现时必须落到 AnimLibrary，而不是提前 return");
    }

    [Test]
    public void TurnInMotion_DoesNotGateMovementAndUsesAtomicSpatialHandoff()
    {
        var player = Read("3_Gameplay/Characters/Player/Core/Player.cs");
        var locomotion = Read("3_Gameplay/Characters/Player/States/PlayerLocomotionState.cs");
        var action = Read("3_Gameplay/Characters/Player/States/PlayerActionState.cs");
        var anim = Read("5_Presentation/Animation/Controllers/PlayerAnimController.cs");
        var visual = Read("3_Gameplay/Characters/Player/Presentation/VisualFacingDriver.cs");

        StringAssert.Contains("actionState.CurrentAction.IsLocomotionRecovery", player);
        StringAssert.Contains("TurnCompensation.ImmediateCommit", player);
        Assert.IsFalse(player.Contains("TickTurnPreMoveGate"));
        Assert.IsFalse(locomotion.Contains("TickTurnPreMoveGate"));
        Assert.IsFalse(action.Contains("TickTurnPreMoveGate"));

        StringAssert.Contains("_player.CurrentTurnCompensationCue.IsTurning", anim);
        StringAssert.Contains("_playedTurnCompensationGeneration", anim);
        StringAssert.Contains("ResolvePlaybackSpeedForLease", anim);
        StringAssert.Contains("return 0f;", anim,
            "Turn→Locomotion 的空间朝向交接必须原子完成，普通 CrossFade 会制造第二次转向");
        Assert.IsFalse(visual.Contains("IsTurnPreMoveGateActive"));
    }
}

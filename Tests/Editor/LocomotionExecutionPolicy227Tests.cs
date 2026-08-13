using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>227.5.1 — State 决定执行拓扑，Action.IsContinuousLocomotion 决定连续槽接管。</summary>
public sealed class LocomotionExecutionPolicy227Tests
{
    [Test]
    public void Idle_FlagOff_DoesNotTakeOverContinuousPresentation()
    {
        var profile = CreateProfile(LocomotionStateFlag.Idle);
        var clip = new AnimationClip { name = "IdleClip" };
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.MainClip = clip;
        action.IsContinuousLocomotion = false;
        action.CrossfadeTime = 0.12f;
        action.AnimSpeed = 1.1f;
        Bind(profile, LocomotionStateId.Idle, action);

        var decision = Resolve(profile, LocomotionStateId.Idle);

        Assert.AreEqual(LocomotionExecutionPolicy.ContinuousPresentation, decision.ExecutionPolicy);
        Assert.IsNull(decision.LocomotionAction);
        Assert.IsNull(decision.ContinuousClip);
        Assert.AreEqual("IsContinuousNotOptedIn", decision.FallbackReason);
        Assert.IsNull(decision.DiscreteAction);

        Cleanup(profile, action, clip);
    }

    [Test]
    public void Idle_FlagOn_TakesOverContinuousPresentation()
    {
        var profile = CreateProfile(LocomotionStateFlag.Idle);
        var clip = new AnimationClip { name = "IdleClipOn" };
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.MainClip = clip;
        action.IsContinuousLocomotion = true;
        action.CrossfadeTime = 0.2f;
        Bind(profile, LocomotionStateId.Idle, action);

        var decision = Resolve(profile, LocomotionStateId.Idle);

        Assert.AreEqual(LocomotionExecutionPolicy.ContinuousPresentation, decision.ExecutionPolicy);
        Assert.AreEqual(action, decision.LocomotionAction);
        Assert.AreEqual(clip, decision.ContinuousClip);
        Assert.AreEqual(0.2f, decision.TransitionDuration, 0.0001f);

        Cleanup(profile, action, clip);
    }

    [Test]
    public void WalkStart_FlagOn_StillDiscreteActionTimeline()
    {
        var profile = CreateProfile(LocomotionStateFlag.Idle | LocomotionStateFlag.WalkStart);
        var clip = new AnimationClip { name = "WalkStartClip" };
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.MainClip = clip;
        action.IsContinuousLocomotion = true;
        Bind(profile, LocomotionStateId.WalkStart, action);

        var decision = Resolve(profile, LocomotionStateId.WalkStart);

        Assert.AreEqual(LocomotionExecutionPolicy.DiscreteActionTimeline, decision.ExecutionPolicy);
        Assert.AreEqual(action, decision.DiscreteAction);
        Assert.IsNull(decision.ContinuousClip);

        Cleanup(profile, action, clip);
    }

    [Test]
    public void WalkEnd_FlagOff_DiscreteActionTimeline()
    {
        var profile = CreateProfile(LocomotionStateFlag.Idle | LocomotionStateFlag.WalkEnd);
        var clip = new AnimationClip { name = "WalkEndClip" };
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.MainClip = clip;
        action.IsContinuousLocomotion = false;
        Bind(profile, LocomotionStateId.WalkEnd, action);

        var decision = Resolve(profile, LocomotionStateId.WalkEnd);

        Assert.AreEqual(LocomotionExecutionPolicy.DiscreteActionTimeline, decision.ExecutionPolicy);
        Assert.AreEqual(action, decision.DiscreteAction);

        Cleanup(profile, action, clip);
    }

    [Test]
    public void ContinuousState_NoMainClip_WithLegacyClip_FallsBack()
    {
        var profile = CreateProfile(LocomotionStateFlag.Idle);
        var legacyClip = new AnimationClip { name = "LegacyIdle" };
        var binding = new LocomotionStateBinding
        {
            State = LocomotionStateId.Idle,
            FallbackState = LocomotionStateId.Idle,
        };
#pragma warning disable CS0618
        binding.ContinuousClip = legacyClip;
        binding.TransitionDuration = 0.09f;
        binding.Speed = 1.25f;
#pragma warning restore CS0618
        profile.EditorSetBindings(new[] { binding });

        var decision = Resolve(profile, LocomotionStateId.Idle);

        Assert.AreEqual(LocomotionExecutionPolicy.ContinuousPresentation, decision.ExecutionPolicy);
        Assert.AreEqual(legacyClip, decision.ContinuousClip);
        Assert.IsNull(decision.LocomotionAction);
        Assert.AreEqual("LegacyContinuousClip", decision.FallbackReason);

        Object.DestroyImmediate(profile);
        Object.DestroyImmediate(legacyClip);
    }

    [Test]
    public void Validate_ContinuousState_MissingClip_ReportsContentError()
    {
        var profile = CreateProfile(LocomotionStateFlag.Idle);
        Bind(profile, LocomotionStateId.Idle, null);

        var report = LocomotionProfileSyncAdapter.Validate(profile);
        Assert.IsTrue(report.ContentErrors.Exists(e => e.Contains("continuous State 无可用接管 Action")));

        Object.DestroyImmediate(profile);
    }

    [Test]
    public void Validate_ContinuousAction_FlagOff_ReportsTakeoverError()
    {
        var profile = CreateProfile(LocomotionStateFlag.Idle);
        var clip = new AnimationClip { name = "IdleFlagOff" };
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.MainClip = clip;
        action.IsContinuousLocomotion = false;
        Bind(profile, LocomotionStateId.Idle, action);

        var report = LocomotionProfileSyncAdapter.Validate(profile);
        Assert.IsTrue(report.ContentErrors.Exists(e => e.Contains("未勾选 Is Continuous")));

        Cleanup(profile, action, clip);
    }

    [Test]
    public void Validate_DiscreteAction_FlagOn_ReportsContradiction()
    {
        var profile = CreateProfile(LocomotionStateFlag.Idle | LocomotionStateFlag.JumpStart);
        var clip = new AnimationClip { name = "JumpStart" };
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.MainClip = clip;
        action.IsContinuousLocomotion = true;
        Bind(profile, LocomotionStateId.JumpStart, action);

        var report = LocomotionProfileSyncAdapter.Validate(profile);
        Assert.IsTrue(report.ContentErrors.Exists(e => e.Contains("discrete State 不得勾选 Is Continuous")));

        Cleanup(profile, action, clip);
    }

    [Test]
    public void TurnInPlace_FlagOff_AllowsFinitePresentationWithoutLoopContract()
    {
        var clip = new AnimationClip { name = "TurnLeft90" };
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.MainClip = clip;
        action.IsContinuousLocomotion = false;
        var binding = new LocomotionStateBinding
        {
            State = LocomotionStateId.TurnInPlaceDirected,
            LocomotionAction = action,
        };

        var resolved = binding.TryGetContinuousPresentation(
            out var resolvedClip,
            out _,
            out _,
            out _,
            out var resolvedAction);

        Assert.IsTrue(resolved);
        Assert.AreEqual(clip, resolvedClip);
        Assert.AreEqual(action, resolvedAction);
        Assert.IsFalse(resolvedAction.IsContinuousLocomotion);

        Object.DestroyImmediate(action);
        Object.DestroyImmediate(clip);
    }

    [Test]
    public void FromState_MapsContinuousAndDiscrete()
    {
        Assert.AreEqual(
            LocomotionExecutionPolicy.ContinuousPresentation,
            LocomotionExecutionPolicyUtil.FromState(LocomotionStateId.Idle));
        Assert.AreEqual(
            LocomotionExecutionPolicy.ContinuousPresentation,
            LocomotionExecutionPolicyUtil.FromState(LocomotionStateId.Walk));
        Assert.AreEqual(
            LocomotionExecutionPolicy.DiscreteActionTimeline,
            LocomotionExecutionPolicyUtil.FromState(LocomotionStateId.WalkStart));
        Assert.AreEqual(
            LocomotionExecutionPolicy.DiscreteActionTimeline,
            LocomotionExecutionPolicyUtil.FromState(LocomotionStateId.RunEnd));
    }

    static LocomotionProfile CreateProfile(LocomotionStateFlag enabled)
    {
        var profile = ScriptableObject.CreateInstance<LocomotionProfile>();
        var so = new SerializedObject(profile);
        so.FindProperty("enabledStates").intValue = (int)enabled;
        so.ApplyModifiedPropertiesWithoutUndo();
        return profile;
    }

    static void Bind(LocomotionProfile profile, LocomotionStateId state, ActionDataSO action)
    {
        profile.EditorSetBindings(new[]
        {
            new LocomotionStateBinding
            {
                State = state,
                FallbackState = LocomotionStateId.Idle,
                LocomotionAction = action,
            },
        });
    }

    static LocomotionDecision Resolve(LocomotionProfile profile, LocomotionStateId state)
    {
        return LocomotionResolver.Resolve(
            new LocomotionIntent(state, Vector3.zero, wantsRun: false, turnAngleDeg: 0f),
            new LocomotionContext(isGrounded: true, isLockedOn: false, planarSpeed: 0f),
            profile);
    }

    static void Cleanup(LocomotionProfile profile, ActionDataSO action, AnimationClip clip)
    {
        Object.DestroyImmediate(profile);
        Object.DestroyImmediate(action);
        Object.DestroyImmediate(clip);
    }
}

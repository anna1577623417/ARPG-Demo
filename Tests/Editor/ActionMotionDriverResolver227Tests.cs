using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>227.4 — 显式 Motion Driver 解析矩阵与 Locomotion Validator。</summary>
public sealed class ActionMotionDriverResolver227Tests
{
    [Test]
    public void LegacyAuto_RootMotionWinsOverAssignedProfile()
    {
        var action = CreateAction();
        var profile = ScriptableObject.CreateInstance<MotionProfileSO>();
        action.UseClipRootMotion = true;
        action.MotionProfile = profile;

        var plan = ActionMotionDriverResolver.Resolve(action);

        Assert.AreEqual(ActionMotionDriverMode.ClipRootMotion, plan.EffectiveMode);
        Assert.IsTrue(plan.UsesClipRootMotion);
        Assert.IsFalse(plan.UsesMotionExecutor);
        Cleanup(action, profile);
    }

    [Test]
    public void LegacyAuto_ProfileAssigned_UsesExecutor()
    {
        var action = CreateAction();
        var profile = ScriptableObject.CreateInstance<MotionProfileSO>();
        action.MotionProfile = profile;

        var plan = ActionMotionDriverResolver.Resolve(action);

        Assert.AreEqual(ActionMotionDriverMode.MotionProfile, plan.EffectiveMode);
        Assert.IsTrue(plan.UsesMotionExecutor);
        Cleanup(action, profile);
    }

    [Test]
    public void LegacyAuto_WithoutProfile_PreservesNoExecutorPath()
    {
        var action = CreateAction();

        var plan = ActionMotionDriverResolver.Resolve(action);

        Assert.AreEqual(ActionMotionDriverMode.LegacyAuto, plan.EffectiveMode);
        Assert.IsFalse(plan.UsesMotionExecutor);
        Assert.IsFalse(plan.RequiresBaseMotorTick);
        Cleanup(action);
    }

    [Test]
    public void ExplicitInherit_IgnoresResidualProfileAndRequiresBaseMotor()
    {
        var action = CreateAction();
        var profile = ScriptableObject.CreateInstance<MotionProfileSO>();
        action.MotionProfile = profile;
        action.MotionDriverMode = ActionMotionDriverMode.InheritStateMotor;

        var plan = ActionMotionDriverResolver.Resolve(action);

        Assert.AreEqual(ActionMotionDriverMode.InheritStateMotor, plan.EffectiveMode);
        Assert.IsTrue(plan.RequiresBaseMotorTick);
        Assert.IsTrue(plan.AllowsPlanarIntent);
        Assert.IsFalse(plan.UsesMotionExecutor);
        Cleanup(action, profile);
    }

    [Test]
    public void ExplicitMotionProfile_WithoutProfile_IsInvalid()
    {
        var action = CreateAction();
        action.MotionDriverMode = ActionMotionDriverMode.MotionProfile;

        var plan = ActionMotionDriverResolver.Resolve(action);

        Assert.IsFalse(plan.IsValid);
        Assert.IsFalse(plan.UsesMotionExecutor);
        StringAssert.Contains("requires", plan.ResolutionReason);
        Cleanup(action);
    }

    [Test]
    public void Stationary_MaintainsVerticalAndGroundingWithoutPlanarIntent()
    {
        var action = CreateAction();
        action.MotionDriverMode = ActionMotionDriverMode.Stationary;

        var plan = ActionMotionDriverResolver.Resolve(action);

        Assert.IsTrue(plan.RequiresBaseMotorTick);
        Assert.IsFalse(plan.AllowsPlanarIntent);
        Assert.IsTrue(plan.MaintainsVerticalPhysics);
        Assert.IsTrue(plan.MaintainsGrounding);
        Cleanup(action);
    }

    [Test]
    public void Validator_RunStartZeroOutputProfile_IsError()
    {
        var action = CreateAction();
        var motion = ScriptableObject.CreateInstance<MotionProfileSO>();
        motion.AxisCurves = new MotionAxisCurves
        {
            ZCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f),
            ZScale = 0f,
        };
        action.MotionDriverMode = ActionMotionDriverMode.MotionProfile;
        action.MotionProfile = motion;
        var profile = CreateLocomotionProfile(LocomotionStateFlag.RunStart);
        profile.EditorSetBindings(new[]
        {
            new LocomotionStateBinding
            {
                State = LocomotionStateId.RunStart,
                LocomotionAction = action,
            },
        });

        var report = LocomotionProfileSyncAdapter.Validate(profile);

        Assert.IsTrue(report.ContentErrors.Exists(e => e.Contains("有效平面输出为 0")));
        Cleanup(action, motion, profile);
    }

    [Test]
    public void Validator_JumpStartInheritWithoutProfile_IsValidMotionContract()
    {
        var action = CreateAction();
        action.MotionDriverMode = ActionMotionDriverMode.InheritStateMotor;
        var profile = CreateLocomotionProfile(LocomotionStateFlag.JumpStart);
        profile.EditorSetBindings(new[]
        {
            new LocomotionStateBinding
            {
                State = LocomotionStateId.JumpStart,
                LocomotionAction = action,
            },
        });

        var report = LocomotionProfileSyncAdapter.Validate(profile);

        Assert.IsFalse(report.ContentErrors.Exists(e => e.Contains("Motion Driver") || e.Contains("JumpStart: Stationary")));
        Cleanup(action, profile);
    }

    static ActionDataSO CreateAction()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.IntentCategory = ActionIntentCategory.Locomotion;
        return action;
    }

    static LocomotionProfile CreateLocomotionProfile(LocomotionStateFlag enabled)
    {
        var profile = ScriptableObject.CreateInstance<LocomotionProfile>();
        var serialized = new SerializedObject(profile);
        serialized.FindProperty("enabledStates").intValue = (int)enabled;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return profile;
    }

    static void Cleanup(params Object[] objects)
    {
        for (var i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
            {
                Object.DestroyImmediate(objects[i]);
            }
        }
    }
}

#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

public sealed class MultiStageRouteRuntimeTests
{
    [Test]
    public void LeeSinQ1Hit_ArmsPendingStage1()
    {
        var routeDef = ScriptableObject.CreateInstance<MultiStageRouteDefinition>();
        var s0 = ScriptableObject.CreateInstance<SkillStageDefinition>();
        var s1 = ScriptableObject.CreateInstance<SkillStageDefinition>();
        SetStages(routeDef, s0, s1);
        SetChainMode(routeDef, MultiStageChainMode.HitToAdvance);
        SetArmSeconds(routeDef, 5f);

        var rt = new MultiStageRouteRuntime();
        rt.Bind(routeDef);

        var tally = new StageHitTally();
        tally.Record(TransitionTargetRule.HeroOnly);
        var ctx = new SkillRouteContext { Now = 100f, HitTally = tally };

        rt.OnEnter(in ctx);
        rt.OnExit(in ctx, wasInterrupted: false);

        Assert.IsTrue(rt.TryPeekPendingEntryStage(101f, out var stage, out var idx));
        Assert.AreEqual(1, idx);
        Assert.AreEqual(s1, stage);
    }

    [Test]
    public void LuxE_FirstStageEnd_ArmsPendingWithoutHit()
    {
        var routeDef = ScriptableObject.CreateInstance<MultiStageRouteDefinition>();
        var s0 = ScriptableObject.CreateInstance<SkillStageDefinition>();
        var s1 = ScriptableObject.CreateInstance<SkillStageDefinition>();
        SetStages(routeDef, s0, s1);
        SetChainMode(routeDef, MultiStageChainMode.PressToAdvance);
        SetArmSeconds(routeDef, 4f);

        var rt = new MultiStageRouteRuntime();
        rt.Bind(routeDef);
        var ctx = new SkillRouteContext { Now = 10f, HitTally = new StageHitTally() };

        rt.OnEnter(in ctx);
        rt.OnExit(in ctx, wasInterrupted: false);

        Assert.IsTrue(rt.HasPendingNextStage);
        Assert.IsTrue(rt.TryPeekPendingEntryStage(11f, out _, out var idx));
        Assert.AreEqual(1, idx);
    }

    [Test]
    public void LuxE_PendingTimeout_StartsCooldown()
    {
        var routeDef = ScriptableObject.CreateInstance<MultiStageRouteDefinition>();
        var s0 = ScriptableObject.CreateInstance<SkillStageDefinition>();
        var s1 = ScriptableObject.CreateInstance<SkillStageDefinition>();
        SetStages(routeDef, s0, s1);
        SetChainMode(routeDef, MultiStageChainMode.PressToAdvance);
        SetArmSeconds(routeDef, 1f);
        SetCooldownPolicy(routeDef, RouteCooldownPolicy.OnRouteEnd);
        SetBaseCooldown(routeDef, 3f);

        var rt = new MultiStageRouteRuntime();
        rt.Bind(routeDef);
        var ctx = new SkillRouteContext { Now = 0f, HitTally = new StageHitTally() };

        rt.OnEnter(in ctx);
        rt.OnExit(in ctx, wasInterrupted: false);

        rt.TickPendingWindow(new SkillRouteContext { Now = 2f, HitTally = new StageHitTally() });

        Assert.IsFalse(rt.HasPendingNextStage);
        Assert.Greater(rt.CdRemainingSeconds, 0f);
    }

    [Test]
    public void KaynQ_AutoChain_UsesTwoStagesInOneRoute()
    {
        var routeDef = ScriptableObject.CreateInstance<MultiStageRouteDefinition>();
        var s0 = ScriptableObject.CreateInstance<SkillStageDefinition>();
        var s1 = ScriptableObject.CreateInstance<SkillStageDefinition>();
        SetStages(routeDef, s0, s1);
        SetArmSeconds(routeDef, 0f);

        var rt = new MultiStageRouteRuntime();
        rt.Bind(routeDef);
        var ctx = new SkillRouteContext { Now = 0f, DeltaTime = 0.5f, HitTally = new StageHitTally() };
        rt.OnEnter(in ctx);
        Assert.AreEqual(0, rt.CurrentStageIndex);
    }

    static void SetStages(MultiStageRouteDefinition def, params SkillStageDefinition[] stages)
    {
        var so = new UnityEditor.SerializedObject(def);
        so.FindProperty("stages").arraySize = stages.Length;
        for (var i = 0; i < stages.Length; i++)
        {
            so.FindProperty("stages").GetArrayElementAtIndex(i).objectReferenceValue = stages[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetArmSeconds(MultiStageRouteDefinition def, float sec)
    {
        var so = new UnityEditor.SerializedObject(def);
        so.FindProperty("stageChainArmSeconds").floatValue = sec;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetChainMode(MultiStageRouteDefinition def, MultiStageChainMode mode)
    {
        var so = new UnityEditor.SerializedObject(def);
        so.FindProperty("chainMode").enumValueIndex = (int)mode;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetCooldownPolicy(MultiStageRouteDefinition def, RouteCooldownPolicy policy)
    {
        var so = new UnityEditor.SerializedObject(def);
        so.FindProperty("cooldownPolicy").enumValueIndex = (int)policy;
        so.FindProperty("startCooldownOnArmTimeout").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetBaseCooldown(MultiStageRouteDefinition def, float seconds)
    {
        var so = new UnityEditor.SerializedObject(def);
        so.FindProperty("baseCooldownSeconds").floatValue = seconds;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif

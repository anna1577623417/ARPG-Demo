#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Entry -> Route 解析回归：
/// 1) LM 对应 Skill_Entry_01
/// 2) Key0 槽位解析；孤立下标 2 仍归并 LM
/// 3) Release 事件（hold>0）触发时可命中 Combo 窗口
/// </summary>
public sealed class SkillEntryRouteResolveTests
{
    [Test]
    public void IntentKind01_MapsToLmSlot()
    {
        var ok = GameplayIntent.TryIntentKindToSlot(GameplayIntentKind.Skill_Entry_01, out var slot);
        Assert.IsTrue(ok);
        Assert.AreEqual(SkillEntrySlot.LM, slot);
    }

    [Test]
    public void Key0_ResolvesBoundEntry()
    {
        var loadout = ScriptableObject.CreateInstance<SkillEntryLoadoutSO>();
        var entry = ScriptableObject.CreateInstance<SkillEntryDefinition>();
        var normal = ScriptableObject.CreateInstance<NormalRouteDefinition>();
        var stage = ScriptableObject.CreateInstance<SkillStageDefinition>();
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.Duration = 0.2f;

        SetPrivateField(stage, "action", action);
        SetPrivateField(normal, "stages", new[] { stage });
        SetPrivateField(entry, "normalRoute", normal);
        SetPrivateField(entry, "slot", SkillEntrySlot.Key0);

        var bindings = new SkillEntryLoadoutSO.EntryBinding[1];
        SetPrivateField(ref bindings[0], "slot", SkillEntrySlot.Key0);
        SetPrivateField(ref bindings[0], "entry", entry);
        SetPrivateField(ref bindings[0], "hudKeyLabel", "0");
        SetPrivateField(loadout, "bindings", bindings);

        var service = new SkillEntryService(owner: null);
        service.Rebuild(loadout);

        var intent = GameplayIntent.ForEntry(
            SkillEntrySlot.Key0,
            time: 1f,
            bufferSeconds: 0.18f,
            requiredAll: 0UL,
            requiredAny: 0UL,
            forbidden: 0UL);

        InputSnapshot snap = default;
        snap.TriggerSlot = SkillEntrySlot.Key0;
        var rt = service.TryResolveForIntent(in intent, in snap, now: 1f);

        Assert.IsNotNull(rt);
        Assert.AreEqual(RouteKind.Normal, rt.Kind);
    }

    [Test]
    public void OrphanSlotIndex2_ResolvesLmEntry()
    {
        AssertResolveLmForOrphanSlotIndex(2);
    }

    static void AssertResolveLmForOrphanSlotIndex(int orphanIndex)
    {
        var loadout = ScriptableObject.CreateInstance<SkillEntryLoadoutSO>();
        var entry = ScriptableObject.CreateInstance<SkillEntryDefinition>();
        var normal = ScriptableObject.CreateInstance<NormalRouteDefinition>();
        var stage = ScriptableObject.CreateInstance<SkillStageDefinition>();
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.Duration = 0.2f;

        SetPrivateField(stage, "action", action);
        SetPrivateField(normal, "stages", new[] { stage });
        SetPrivateField(entry, "normalRoute", normal);
        SetPrivateField(entry, "slot", SkillEntrySlot.LM);

        var bindings = new SkillEntryLoadoutSO.EntryBinding[1];
        SetPrivateField(ref bindings[0], "slot", SkillEntrySlot.LM);
        SetPrivateField(ref bindings[0], "entry", entry);
        SetPrivateField(ref bindings[0], "hudKeyLabel", "LMB");
        SetPrivateField(loadout, "bindings", bindings);

        var service = new SkillEntryService(owner: null);
        service.Rebuild(loadout);

        var orphanSlot = (SkillEntrySlot)orphanIndex;
        var intent = GameplayIntent.ForEntry(
            orphanSlot,
            time: 1f,
            bufferSeconds: 0.18f,
            requiredAll: 0UL,
            requiredAny: 0UL,
            forbidden: 0UL);

        InputSnapshot snap = default;
        snap.TriggerSlot = orphanSlot;
        var rt = service.TryResolveForIntent(in intent, in snap, now: 1f);

        Assert.IsNotNull(rt);
        Assert.AreEqual(RouteKind.Normal, rt.Kind);
    }

    [Test]
    public void ComboWindowOpen_OnReleaseIntent_ResolvesCombo()
    {
        var entry = ScriptableObject.CreateInstance<SkillEntryDefinition>();
        var combo = ScriptableObject.CreateInstance<ComboRouteDefinition>();
        var child = ScriptableObject.CreateInstance<NormalRouteDefinition>();

        var stage = ScriptableObject.CreateInstance<SkillStageDefinition>();
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.Duration = 0.2f;
        SetPrivateField(stage, "action", action);
        SetPrivateField(child, "stages", new[] { stage });
        SetPrivateField(combo, "comboChain", new SkillRouteDefinition[] { child });
        SetPrivateField(combo, "comboResetTime", 0.35f);
        SetPrivateField(entry, "comboRoute", combo);

        var comboRt = new ComboRouteRuntime();
        comboRt.Bind(combo);
        // 模拟首招已提交：打开 Combo 窗（CommitAdvance 写入 _lastInputTime）。
        comboRt.CommitAdvance(comboRt.PeekNextIndex(2f), 2f);

        InputSnapshot snap = default;
        snap.TriggerSlot = SkillEntrySlot.LM;
        snap.TriggerReleasedEdge = true;
        snap.TriggerHoldSeconds = 0.22f;

        var rr = RouteResolver.Resolve(entry, in snap, comboRt, now: 2.1f);
        Assert.IsNotNull(rr.Chosen);
        Assert.AreEqual(RouteKind.Combo, rr.ChosenKind);
    }

    static void SetPrivateField<TObj, TValue>(TObj obj, string fieldName, TValue value)
    {
        var f = FindField(typeof(TObj), fieldName);
        Assert.IsNotNull(f, $"Field not found: {typeof(TObj).Name}.{fieldName}");
        f.SetValue(obj, value);
    }

    static void SetPrivateField<TValue>(ref SkillEntryLoadoutSO.EntryBinding binding, string fieldName, TValue value)
    {
        var boxed = (object)binding;
        var f = typeof(SkillEntryLoadoutSO.EntryBinding).GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(f, $"Field not found: EntryBinding.{fieldName}");
        f.SetValue(boxed, value);
        binding = (SkillEntryLoadoutSO.EntryBinding)boxed;
    }

    static System.Reflection.FieldInfo FindField(System.Type t, string fieldName)
    {
        while (t != null)
        {
            var f = t.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (f != null)
            {
                return f;
            }

            t = t.BaseType;
        }

        return null;
    }
}
#endif

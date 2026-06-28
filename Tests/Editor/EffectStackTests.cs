using NUnit.Framework;
using UnityEngine;

/// <summary>180.1 W2+W5 — EffectStack 5 策略 + while 消化验收。</summary>
public sealed class EffectStackTests
{
    EffectDefinitionSO MakeDef(
        StackPolicy policy,
        int maxStack = 1,
        float duration = 5f,
        float period = 0f,
        float power = 1f)
    {
        var d = ScriptableObject.CreateInstance<EffectDefinitionSO>();
        d.StackPolicy = policy;
        d.MaxStack = maxStack;
        d.Duration = DurationPolicy.Timed;
        d.DurationSeconds = duration;
        d.PeriodSeconds = period;
        d.Power = power;
        return d;
    }

    // ─── §11.2 Stack 策略 ──────────────────────────────────

    [Test]
    public void Refresh_Reapply_DoesNotCreateNew()
    {
        var def = MakeDef(StackPolicy.Refresh);
        var stack = new EffectStack();
        try
        {
            var a = stack.Apply(def, "src", EffectSourceKind.Skill);
            var b = stack.Apply(def, "src", EffectSourceKind.Skill);
            Assert.AreEqual(1, stack.Count);
            Assert.AreSame(a, b);
        }
        finally { Object.DestroyImmediate(def); }
    }

    [Test]
    public void Independent_EachApply_CreatesNewInstance()
    {
        var def = MakeDef(StackPolicy.Independent);
        var stack = new EffectStack();
        try
        {
            stack.Apply(def, "p1", EffectSourceKind.Skill);
            stack.Apply(def, "p2", EffectSourceKind.Skill);
            stack.Apply(def, "p3", EffectSourceKind.Skill);
            Assert.AreEqual(3, stack.Count);
        }
        finally { Object.DestroyImmediate(def); }
    }

    [Test]
    public void AddStack_IncreasesStackCount_UpToMax()
    {
        var def = MakeDef(StackPolicy.AddStack, maxStack: 3);
        var stack = new EffectStack();
        try
        {
            var a = stack.Apply(def, "i", EffectSourceKind.Item);
            stack.Apply(def, "i", EffectSourceKind.Item);
            stack.Apply(def, "i", EffectSourceKind.Item);
            stack.Apply(def, "i", EffectSourceKind.Item); // 满 → 仅刷时长
            Assert.AreEqual(1, stack.Count);
            Assert.AreEqual(3, a.StackCount);
        }
        finally { Object.DestroyImmediate(def); }
    }

    [Test]
    public void RefreshIndependent_FullThenRefreshOldest()
    {
        var def = MakeDef(StackPolicy.RefreshIndependent, maxStack: 3, duration: 10f);
        var stack = new EffectStack();
        try
        {
            stack.Apply(def, "p1", EffectSourceKind.Skill);
            stack.Tick(1f);
            stack.Apply(def, "p2", EffectSourceKind.Skill);
            stack.Tick(1f);
            stack.Apply(def, "p3", EffectSourceKind.Skill);
            Assert.AreEqual(3, stack.Count);

            // 满 3 → 不新增；刷最早（仍 3 实例）
            stack.Apply(def, "p4", EffectSourceKind.Skill);
            Assert.AreEqual(3, stack.Count);
        }
        finally { Object.DestroyImmediate(def); }
    }

    [Test]
    public void ReplaceLower_OnlyHighestPowerSurvives()
    {
        var lo = MakeDef(StackPolicy.ReplaceLower, power: 1f);
        var hi = MakeDef(StackPolicy.ReplaceLower, power: 5f);
        // 同 SelfTag（用同一非零 mask 模拟）
        lo.SelfTags = new GameplayTagMask { Value = 0x01 };
        hi.SelfTags = new GameplayTagMask { Value = 0x01 };
        var stack = new EffectStack();
        try
        {
            stack.Apply(lo, "s", EffectSourceKind.Skill);
            stack.Apply(hi, "s", EffectSourceKind.Skill);
            Assert.AreEqual(1, stack.Count);
            Assert.AreSame(hi, stack.Instances[0].Definition);

            // 再次尝试低级 → 拒绝
            stack.Apply(lo, "s", EffectSourceKind.Skill);
            Assert.AreEqual(1, stack.Count);
            Assert.AreSame(hi, stack.Instances[0].Definition);
        }
        finally
        {
            Object.DestroyImmediate(lo);
            Object.DestroyImmediate(hi);
        }
    }

    // ─── §11.5 防 Bug 七律 ─────────────────────────────────

    [Test]
    public void Tick_LargeDt_FiresMultiplePeriods_WhileLoop()
    {
        var def = MakeDef(StackPolicy.Refresh, duration: 10f, period: 0.3f);
        var stack = new EffectStack();
        var periodCount = 0;
        stack.PeriodElapsed += _ => periodCount++;
        try
        {
            stack.Apply(def, "s", EffectSourceKind.Skill);
            stack.Tick(1.0f); // 1.0s / 0.3s = 3 次
            Assert.AreEqual(3, periodCount, "while 消化超额 dt 应触发 3 次");
        }
        finally { Object.DestroyImmediate(def); }
    }

    [Test]
    public void Tick_TimedExpires_FiresRemoved()
    {
        var def = MakeDef(StackPolicy.Refresh, duration: 1f);
        var stack = new EffectStack();
        EffectRemoveReason capturedReason = default;
        stack.Removed += (_, r) => capturedReason = r;
        try
        {
            stack.Apply(def, "s", EffectSourceKind.Skill);
            stack.Tick(0.5f);
            Assert.AreEqual(1, stack.Count);
            stack.Tick(0.6f);
            Assert.AreEqual(0, stack.Count);
            Assert.AreEqual(EffectRemoveReason.Expired, capturedReason);
        }
        finally { Object.DestroyImmediate(def); }
    }

    [Test]
    public void RemoveAllBySource_ExactRecycling()
    {
        var def = MakeDef(StackPolicy.Independent, maxStack: 99);
        var stack = new EffectStack();
        try
        {
            var equip = new object();
            var skill = new object();
            stack.Apply(def, equip, EffectSourceKind.Equipment);
            stack.Apply(def, equip, EffectSourceKind.Equipment);
            stack.Apply(def, skill, EffectSourceKind.Skill);
            Assert.AreEqual(3, stack.Count);

            var removed = stack.RemoveAllBySource(equip);
            Assert.AreEqual(2, removed);
            Assert.AreEqual(1, stack.Count);
        }
        finally { Object.DestroyImmediate(def); }
    }

    [Test]
    public void RemoveAllBySourceKind_CleansAllOfKind()
    {
        var def = MakeDef(StackPolicy.Independent, maxStack: 99);
        var stack = new EffectStack();
        try
        {
            stack.Apply(def, "i1", EffectSourceKind.Item);
            stack.Apply(def, "i2", EffectSourceKind.Item);
            stack.Apply(def, "r1", EffectSourceKind.Rune);
            stack.RemoveAllBySourceKind(EffectSourceKind.Item);
            Assert.AreEqual(1, stack.Count);
            Assert.AreEqual(EffectSourceKind.Rune, stack.Instances[0].SourceKind);
        }
        finally { Object.DestroyImmediate(def); }
    }

    [Test]
    public void RuntimeId_IsUniquePerInstance()
    {
        var def = MakeDef(StackPolicy.Independent);
        var stack = new EffectStack();
        try
        {
            var a = stack.Apply(def, "s", EffectSourceKind.Skill);
            var b = stack.Apply(def, "s", EffectSourceKind.Skill);
            Assert.AreNotEqual(a.RuntimeId, b.RuntimeId);
        }
        finally { Object.DestroyImmediate(def); }
    }

    // ─── StackMergeMode 公式 ──────────────────────────────

    [Test]
    public void ResolveStackedValue_Multiply()
    {
        Assert.AreEqual(20f, EffectStack.ResolveStackedValue(10f, 2, StackMergeMode.Multiply), 0.001f);
    }

    [Test]
    public void ResolveStackedValue_Logarithmic_DecreasingReturns()
    {
        var v1 = EffectStack.ResolveStackedValue(10f, 1, StackMergeMode.Logarithmic);
        var v2 = EffectStack.ResolveStackedValue(10f, 2, StackMergeMode.Logarithmic);
        var v4 = EffectStack.ResolveStackedValue(10f, 4, StackMergeMode.Logarithmic);
        Assert.Less(v1, v2);
        Assert.Less(v2, v4);
        Assert.Less(v4 - v2, v2 - v1 + 0.5f, "log 段递减回报"); // 容差宽
    }

    [Test]
    public void ResolveStackedValue_NoEffect_BaseUnchanged()
    {
        Assert.AreEqual(10f, EffectStack.ResolveStackedValue(10f, 10, StackMergeMode.NoEffect), 0.001f);
    }

    // ─── ClearAll ─────────────────────────────────────────

    [Test]
    public void ClearAll_RemovesEverythingWithReason()
    {
        var def = MakeDef(StackPolicy.Independent);
        var stack = new EffectStack();
        var reasons = new System.Collections.Generic.List<EffectRemoveReason>();
        stack.Removed += (_, r) => reasons.Add(r);
        try
        {
            stack.Apply(def, "s", EffectSourceKind.Skill);
            stack.Apply(def, "s", EffectSourceKind.Skill);
            stack.ClearAll(EffectRemoveReason.SceneUnload);
            Assert.AreEqual(0, stack.Count);
            Assert.AreEqual(2, reasons.Count);
            Assert.AreEqual(EffectRemoveReason.SceneUnload, reasons[0]);
        }
        finally { Object.DestroyImmediate(def); }
    }
}

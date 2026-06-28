using NUnit.Framework;
using UnityEngine;

/// <summary>175.2 — Jump 子系统 EditMode 验收测试。</summary>
public sealed class JumpSystemTests
{
    // ─── Variant Resolver ───────────────────────────────────────

    [Test]
    public void Resolver_Default_FallsBack_WhenNoMatch()
    {
        var resolver = new JumpVariantResolver();
        var fallback = ScriptableObject.CreateInstance<JumpVariantSO>();
        fallback.Id = JumpVariantId.Normal;
        try
        {
            resolver.SetVariants(System.Array.Empty<JumpVariantSO>(), fallback);
            var ctx = new JumpContext { Grounded = true };
            Assert.AreEqual(fallback, resolver.Resolve(in ctx));
        }
        finally
        {
            Object.DestroyImmediate(fallback);
        }
    }

    [Test]
    public void Resolver_Priority_PicksLowerPriorityFirst()
    {
        var normal = MakeVariant(JumpVariantId.Normal, priority: 100, (ref JumpTriggerCondition c) =>
        {
            c.RequireGroundedTri = 1;
        });
        var triple = MakeVariant(JumpVariantId.Triple, priority: 20, (ref JumpTriggerCondition c) =>
        {
            c.RequireGroundedTri = 1;
            c.RequireComboIndex = 2;
            c.RequireLandWithinSec = 0.2f;
        });

        var resolver = new JumpVariantResolver();
        resolver.SetVariants(new[] { normal, triple }, normal);

        var triCtx = new JumpContext
        {
            Grounded = true,
            RecentLandComboIndex = 2,
            TimeSinceLastLand = 0.1f,
        };
        Assert.AreEqual(triple, resolver.Resolve(in triCtx), "Combo=2 应触发 Triple");

        var plainCtx = new JumpContext { Grounded = true, RecentLandComboIndex = 0 };
        Assert.AreEqual(normal, resolver.Resolve(in plainCtx), "无 Combo 应降级 Normal");

        Object.DestroyImmediate(normal);
        Object.DestroyImmediate(triple);
    }

    [Test]
    public void Resolver_ForcedVariant_BypassesConditions()
    {
        var normal = MakeVariant(JumpVariantId.Normal, 100, (ref JumpTriggerCondition c) => c.RequireGroundedTri = 1);
        var back = MakeVariant(JumpVariantId.Back, 50, (ref JumpTriggerCondition c) =>
        {
            c.RequireGroundedTri = 1;
            c.RequirePivotWithinSec = 0.15f;
        });
        var resolver = new JumpVariantResolver();
        resolver.SetVariants(new[] { normal, back }, normal);

        var ctx = new JumpContext
        {
            Grounded = true,
            TimeSinceLastPivot = -1f,        // 未发生 pivot
            ForcedVariant = JumpVariantId.Back,
        };
        Assert.AreEqual(back, resolver.Resolve(in ctx), "BackflipDevice 强制覆盖");

        Object.DestroyImmediate(normal);
        Object.DestroyImmediate(back);
    }

    // ─── Variant Conditions ────────────────────────────────────

    [Test]
    public void Match_LongJump_RequiresCrouchAndSprint()
    {
        var cond = JumpTriggerCondition.Any;
        cond.RequireGroundedTri = 1;
        cond.RequireCrouchHeld = true;
        cond.RequireSprintHeld = true;

        var ctxFail = new JumpContext { Grounded = true, CrouchHeld = true, SprintHeld = false };
        Assert.IsFalse(JumpVariantResolver.Matches(in cond, in ctxFail));

        var ctxOk = new JumpContext { Grounded = true, CrouchHeld = true, SprintHeld = true };
        Assert.IsTrue(JumpVariantResolver.Matches(in cond, in ctxOk));
    }

    [Test]
    public void Match_Backflip_RequiresRecentPivot()
    {
        var cond = JumpTriggerCondition.Any;
        cond.RequireGroundedTri = 1;
        cond.RequirePivotWithinSec = 0.15f;

        var ctxNoPivot = new JumpContext { Grounded = true, TimeSinceLastPivot = -1f };
        Assert.IsFalse(JumpVariantResolver.Matches(in cond, in ctxNoPivot));

        var ctxOld = new JumpContext { Grounded = true, TimeSinceLastPivot = 0.5f };
        Assert.IsFalse(JumpVariantResolver.Matches(in cond, in ctxOld));

        var ctxFresh = new JumpContext { Grounded = true, TimeSinceLastPivot = 0.10f };
        Assert.IsTrue(JumpVariantResolver.Matches(in cond, in ctxFresh));
    }

    // ─── Modifier Stack ────────────────────────────────────────

    [Test]
    public void Stack_HopooFeather_StackIncreasesMaxJumpCount()
    {
        var stack = new JumpModifierStack();
        stack.Reset(JumpAbilityConfig.Default);
        Assert.AreEqual(1f, stack.Effective.MaxJumpCount, 0.001f, "基线 MaxJumpCount=1");

        var hopoo = ScriptableObject.CreateInstance<JumpModifierSO>();
        hopoo.AddOneAirJumpPerStack = true;
        try
        {
            stack.Add(hopoo);
            Assert.AreEqual(2f, stack.Effective.MaxJumpCount, 0.001f);
            stack.Add(hopoo);
            stack.Add(hopoo);
            Assert.AreEqual(4f, stack.Effective.MaxJumpCount, 0.001f, "三层蓝羽=4");
        }
        finally
        {
            Object.DestroyImmediate(hopoo);
        }
    }

    [Test]
    public void Stack_MulCurrent_AppliesPercentage()
    {
        var stack = new JumpModifierStack();
        stack.Reset(JumpAbilityConfig.Default);

        var hellfire = ScriptableObject.CreateInstance<JumpModifierSO>();
        hellfire.FieldMods = new[]
        {
            new JumpFieldModifier { Field = JumpField.JumpForce, Op = ModifierOp.MulCurrent, Value = 0.05f },
        };
        try
        {
            stack.Add(hellfire);
            Assert.AreEqual(12f * 1.05f, stack.Effective.JumpForce, 0.001f, "+5%");
            stack.Add(hellfire);
            // Recompute 按 n 一次性应用：12 × (1 + 0.05×2) = 13.2
            Assert.AreEqual(12f * 1.10f, stack.Effective.JumpForce, 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(hellfire);
        }
    }

    [Test]
    public void Stack_MaxStack_RejectsBeyondLimit()
    {
        var stack = new JumpModifierStack();
        stack.Reset(JumpAbilityConfig.Default);
        var capped = ScriptableObject.CreateInstance<JumpModifierSO>();
        capped.MaxStack = 2;
        try
        {
            Assert.AreEqual(1, stack.Add(capped));
            Assert.AreEqual(2, stack.Add(capped));
            Assert.AreEqual(-1, stack.Add(capped), "超过 MaxStack 应返回 -1");
            Assert.AreEqual(2, stack.GetStack(capped));
        }
        finally
        {
            Object.DestroyImmediate(capped);
        }
    }

    [Test]
    public void Stack_Remove_DecrementsCorrectly()
    {
        var stack = new JumpModifierStack();
        stack.Reset(JumpAbilityConfig.Default);
        var m = ScriptableObject.CreateInstance<JumpModifierSO>();
        m.AddOneAirJumpPerStack = true;
        try
        {
            stack.Add(m); stack.Add(m); stack.Add(m);
            Assert.AreEqual(4f, stack.Effective.MaxJumpCount, 0.001f);
            stack.Remove(m);
            Assert.AreEqual(3f, stack.Effective.MaxJumpCount, 0.001f);
            stack.Remove(m); stack.Remove(m);
            Assert.AreEqual(1f, stack.Effective.MaxJumpCount, 0.001f);
            Assert.IsFalse(stack.Remove(m), "已清零再 Remove 应返回 false");
        }
        finally
        {
            Object.DestroyImmediate(m);
        }
    }

    // ─── LandComboTracker ──────────────────────────────────────

    [Test]
    public void Combo_FirstJump_HasIndexZero()
    {
        var tracker = new LandComboTracker();
        tracker.OnJump(0f);
        Assert.AreEqual(0, tracker.CurrentIndex);
    }

    [Test]
    public void Combo_LandAndJumpWithinWindow_IncrementsIndex()
    {
        var tracker = new LandComboTracker { ComboWindowSec = 0.20f };
        tracker.OnLand(1.0f);
        tracker.OnJump(1.1f);
        Assert.AreEqual(1, tracker.CurrentIndex);
        tracker.OnLand(1.3f);
        tracker.OnJump(1.35f);
        Assert.AreEqual(2, tracker.CurrentIndex, "Triple Jump 准备就绪");
        tracker.OnLand(1.5f);
        tracker.OnJump(1.55f);
        Assert.AreEqual(2, tracker.CurrentIndex, "应封顶 2");
    }

    [Test]
    public void Combo_BeyondWindow_Resets()
    {
        var tracker = new LandComboTracker { ComboWindowSec = 0.20f };
        tracker.OnLand(1.0f);
        tracker.OnJump(1.5f);
        Assert.AreEqual(0, tracker.CurrentIndex);
    }

    // ─── LandingTierResolver ───────────────────────────────────

    [Test]
    public void Landing_Heights_MapToCorrectTier()
    {
        var profile = ScriptableObject.CreateInstance<LandingTierProfileSO>();
        try
        {
            Assert.AreEqual(LandingTier.Light, LandingTierResolver.Resolve(1f, null, null, null, profile));
            Assert.AreEqual(LandingTier.Soft, LandingTierResolver.Resolve(2.5f, null, null, null, profile));
            Assert.AreEqual(LandingTier.Normal, LandingTierResolver.Resolve(4f, null, null, null, profile));
            Assert.AreEqual(LandingTier.Heavy, LandingTierResolver.Resolve(6f, null, null, null, profile));
            Assert.AreEqual(LandingTier.Roll, LandingTierResolver.Resolve(10f, null, null, null, profile));
        }
        finally
        {
            Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void Landing_GroundPound_AlwaysReturnsPound()
    {
        var variant = ScriptableObject.CreateInstance<JumpVariantSO>();
        variant.Id = JumpVariantId.GroundPound;
        var profile = ScriptableObject.CreateInstance<LandingTierProfileSO>();
        try
        {
            Assert.AreEqual(LandingTier.Pound,
                LandingTierResolver.Resolve(0.5f, variant, null, null, profile));
            Assert.AreEqual(LandingTier.Pound,
                LandingTierResolver.Resolve(100f, variant, null, null, profile));
        }
        finally
        {
            Object.DestroyImmediate(variant);
            Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void Landing_HopooFeather_ImmuneToRoll()
    {
        var profile = ScriptableObject.CreateInstance<LandingTierProfileSO>();
        var hopoo = ScriptableObject.CreateInstance<JumpModifierSO>();
        hopoo.AddOneAirJumpPerStack = true;
        var stack = new JumpModifierStack();
        stack.Reset(JumpAbilityConfig.Default);
        stack.Add(hopoo);
        try
        {
            var tier = LandingTierResolver.Resolve(12f, null, stack, hopoo, profile);
            Assert.AreEqual(LandingTier.Light, tier, "蓝羽免摔伤");
        }
        finally
        {
            Object.DestroyImmediate(profile);
            Object.DestroyImmediate(hopoo);
        }
    }

    // ─── Utility ───────────────────────────────────────────────

    delegate void CondMutator(ref JumpTriggerCondition c);

    static JumpVariantSO MakeVariant(JumpVariantId id, int priority, CondMutator mutator)
    {
        var v = ScriptableObject.CreateInstance<JumpVariantSO>();
        v.Id = id;
        v.Priority = priority;
        var cond = JumpTriggerCondition.Any;
        mutator(ref cond);
        v.Condition = cond;
        return v;
    }
}

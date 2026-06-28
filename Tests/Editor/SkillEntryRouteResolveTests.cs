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

    [Test]
    public void DirectionalIntent_PicksGroupChildByChord()
    {
        var fwd = CreateNormalRoute("fwd");
        var back = CreateNormalRoute("back");
        var left = CreateNormalRoute("left");
        var right = CreateNormalRoute("right");

        var group = ScriptableObject.CreateInstance<SkillGroupDefinition>();
        SetPrivateField(group, "forward", fwd);
        SetPrivateField(group, "backward", back);
        SetPrivateField(group, "left", left);
        SetPrivateField(group, "right", right);
        SetPrivateField(group, "fallbackRoute", fwd);
        SetPrivateField(group, "useFallbackOnNeutral", false);
        SetPrivateField(group, "routes", new[] { fwd, back, left, right });

        var entry = ScriptableObject.CreateInstance<SkillEntryDefinition>();
        SetPrivateField(entry, "slot", SkillEntrySlot.Space);
        SetPrivateField(entry, "primaryUnit", group);

        var loadout = ScriptableObject.CreateInstance<SkillEntryLoadoutSO>();
        var bindings = new SkillEntryLoadoutSO.EntryBinding[1];
        SetPrivateField(ref bindings[0], "slot", SkillEntrySlot.Space);
        SetPrivateField(ref bindings[0], "entry", entry);
        SetPrivateField(loadout, "bindings", bindings);

        var service = new SkillEntryService(owner: null);
        service.Rebuild(loadout);

        Assert.AreEqual(fwd, ResolveDirectional(service, Vector2.up));
        Assert.AreEqual(back, ResolveDirectional(service, Vector2.down));
        Assert.AreEqual(left, ResolveDirectional(service, Vector2.left));
        Assert.AreEqual(right, ResolveDirectional(service, Vector2.right));
    }

    [Test]
    public void Rebuild_RegistersMotionForwardRoute_WhenRoutesArrayIsStale()
    {
        var fwd = CreateNormalRoute("fwd");
        var motion = CreateNormalRoute("motion");

        var group = ScriptableObject.CreateInstance<SkillGroupDefinition>();
        SetPrivateField(group, "forward", fwd);
        SetPrivateField(group, "fallbackRoute", fwd);
        SetPrivateField(group, "motionForwardRoute", motion);
        SetPrivateField(group, "routes", new[] { fwd });

        var entry = ScriptableObject.CreateInstance<SkillEntryDefinition>();
        SetPrivateField(entry, "slot", SkillEntrySlot.Space);
        SetPrivateField(entry, "primaryUnit", group);

        var loadout = ScriptableObject.CreateInstance<SkillEntryLoadoutSO>();
        var bindings = new SkillEntryLoadoutSO.EntryBinding[1];
        SetPrivateField(ref bindings[0], "slot", SkillEntrySlot.Space);
        SetPrivateField(ref bindings[0], "entry", entry);
        SetPrivateField(loadout, "bindings", bindings);

        var service = new SkillEntryService(owner: null);
        service.Rebuild(loadout);

        var runtimesField = typeof(SkillEntryService).GetField(
            "_routeRuntimes",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(runtimesField);

        var runtimes = runtimesField.GetValue(service) as System.Collections.IDictionary;
        Assert.IsNotNull(runtimes);
        Assert.IsTrue(runtimes.Contains(motion));
    }

    [Test]
    public void NeutralIntent_UseFallbackOnNeutral_PicksFallbackRoute()
    {
        var fwd = CreateNormalRoute("fwd");
        var fallback = CreateNormalRoute("step-back");

        var group = ScriptableObject.CreateInstance<SkillGroupDefinition>();
        SetPrivateField(group, "forward", fwd);
        SetPrivateField(group, "fallbackRoute", fallback);
        SetPrivateField(group, "useFallbackOnNeutral", true);
        SetPrivateField(group, "routes", new[] { fwd, fallback });

        var entry = ScriptableObject.CreateInstance<SkillEntryDefinition>();
        SetPrivateField(entry, "slot", SkillEntrySlot.Space);
        SetPrivateField(entry, "primaryUnit", group);

        var loadout = ScriptableObject.CreateInstance<SkillEntryLoadoutSO>();
        var bindings = new SkillEntryLoadoutSO.EntryBinding[1];
        SetPrivateField(ref bindings[0], "slot", SkillEntrySlot.Space);
        SetPrivateField(ref bindings[0], "entry", entry);
        SetPrivateField(loadout, "bindings", bindings);

        var service = new SkillEntryService(owner: null);
        service.Rebuild(loadout);

        var intent = BuildDirectionalIntent(SkillEntrySlot.Space, Vector2.zero);
        InputSnapshot snap = default;
        snap.TriggerSlot = SkillEntrySlot.Space;
        snap.MoveBuffered = Vector2.zero;
        var rt = service.TryResolveForIntent(in intent, in snap, now: 1f);

        Assert.IsNotNull(rt);
        Assert.AreEqual(fallback, rt.Definition);
    }

    [Test]
    public void DirectionalIntent_DoesNotFallbackToNormalRoute()
    {
        var group = ScriptableObject.CreateInstance<SkillGroupDefinition>();
        var fwd = CreateNormalRoute("fwd");
        SetPrivateField(group, "forward", fwd);
        SetPrivateField(group, "baseCooldownSeconds", 1f);
        SetPrivateField(group, "routes", new[] { fwd });
        SetPrivateField(fwd, "ownerGroup", group);

        var normal = CreateNormalRoute("normal-fallback");
        var entry = ScriptableObject.CreateInstance<SkillEntryDefinition>();
        SetPrivateField(entry, "slot", SkillEntrySlot.Space);
        SetPrivateField(entry, "primaryUnit", group);
        SetPrivateField(entry, "normalRoute", normal);

        var loadout = ScriptableObject.CreateInstance<SkillEntryLoadoutSO>();
        var bindings = new SkillEntryLoadoutSO.EntryBinding[1];
        SetPrivateField(ref bindings[0], "slot", SkillEntrySlot.Space);
        SetPrivateField(ref bindings[0], "entry", entry);
        SetPrivateField(loadout, "bindings", bindings);

        var service = new SkillEntryService(owner: null);
        service.Rebuild(loadout);

        var ctx = default(SkillRouteContext);
        ctx.EntryService = service;
        Assert.IsTrue(service.TryApplyGroupCooldown(fwd, in ctx));

        var intent = BuildDirectionalIntent(SkillEntrySlot.Space, Vector2.up);
        InputSnapshot snap = default;
        snap.TriggerSlot = SkillEntrySlot.Space;
        snap.MoveBuffered = Vector2.up;
        var resolved = service.TryResolveForIntent(in intent, in snap, now: 1f);

        Assert.IsNull(resolved, "Directional 语义禁止回落 NormalRoute");
    }

    [Test]
    public void FlowGate_DeniesMobilityWhenGroundedBlocked()
    {
        var rule = ScriptableObject.CreateInstance<AbilityGateRuleSO>();
        SetPrivateField(rule, "ability", AbilitySemantic.Mobility);
        SetPrivateField(rule, "allowWhenGrounded", false);
        SetPrivateField(rule, "allowWhenAirborne", true);

        var map = ScriptableObject.CreateInstance<AbilityMapSO>();
        var bindings = new AbilityMapSO.Binding[1];
        bindings[0] = new AbilityMapSO.Binding
        {
            slot = SkillEntrySlot.Space,
            ability = AbilitySemantic.Mobility,
            rule = rule,
            enabled = true,
        };
        SetPrivateField(map, "bindings", bindings);

        var loadout = ScriptableObject.CreateInstance<SkillEntryLoadoutSO>();
        SetPrivateField(loadout, "abilityMap", map);

        var ctx = new CombatContextSnapshot { IsAirborne = false };
        Assert.IsFalse(AbilityGateService.CanActivateFlow(
            loadout,
            SkillEntrySlot.Space,
            AbilitySemantic.Mobility,
            in ctx,
            out var reason));
        Assert.AreEqual("state gate", reason);
    }

    [Test]
    public void RouteAbilityGate_DeniesAirborneWhenRequireGrounded()
    {
        var rule = ScriptableObject.CreateInstance<AbilityGateRuleSO>();
        SetPrivateField(rule, "requireGrounded", true);

        var route = CreateNormalRoute("fwd");
        SetPrivateField(route, "abilityGateRules", new[] { rule });

        var airborne = new CombatContextSnapshot { IsAirborne = true };
        Assert.IsFalse(AbilityGateService.CanActivateRoute(route, in airborne, out var reason));
        Assert.AreEqual("require grounded", reason);

        var grounded = new CombatContextSnapshot { IsAirborne = false };
        Assert.IsTrue(AbilityGateService.CanActivateRoute(route, in grounded, out _));
    }

    [Test]
    public void MultiStageRouteAbilityGate_UsesSameRouteGatePathAsNormal()
    {
        var rule = ScriptableObject.CreateInstance<AbilityGateRuleSO>();
        SetPrivateField(rule, "allowWhenGrounded", true);
        SetPrivateField(rule, "allowWhenAirborne", false);

        var route = ScriptableObject.CreateInstance<MultiStageRouteDefinition>();
        var stage = ScriptableObject.CreateInstance<SkillStageDefinition>();
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.Duration = 0.2f;
        SetPrivateField(stage, "action", action);
        SetPrivateField(route, "stages", new[] { stage });
        SetPrivateField(route, "abilityGateRules", new[] { rule });

        var airborne = new CombatContextSnapshot { IsAirborne = true };
        Assert.IsFalse(AbilityGateService.CanActivateRoute(route, in airborne, out _));

        var grounded = new CombatContextSnapshot { IsAirborne = false };
        Assert.IsTrue(AbilityGateService.CanActivateRoute(route, in grounded, out _));
    }

    [Test]
    public void ContextGroup_MatchesTapSemanticForGroundedChord()
    {
        var group = ScriptableObject.CreateInstance<SkillGroupDefinition>();
        var ctxDef = ScriptableObject.CreateInstance<SkillContextGroupDefinition>();
        SetPrivateField(ctxDef, "targetGroup", group);
        SetPrivateField(ctxDef, "requiredSlot", SkillEntrySlot.Space);
        SetPrivateField(ctxDef, "requireDirectional", false);
        SetPrivateField(ctxDef, "requireGrounded", true);

        var grounded = new CombatContextSnapshot { IsAirborne = false };
        Assert.IsTrue(ctxDef.Matches(SkillEntrySlot.Space, InputSemanticType.Tap, in grounded));

        var airborne = new CombatContextSnapshot { IsAirborne = true };
        Assert.IsFalse(ctxDef.Matches(SkillEntrySlot.Space, InputSemanticType.Tap, in airborne));
    }

    [Test]
    public void InterruptWindow_AllowsWhenCategoryMatches()
    {
        var current = ScriptableObject.CreateInstance<ActionDataSO>();
        current.InterruptStability = 5;
        current.Windows = new System.Collections.Generic.List<ActionWindow>
        {
            new ActionWindow
            {
                NormalizedStart = 0f,
                NormalizedEnd = 1f,
                InterruptibleByCategories = ActionCategory.Movement,
            },
        };

        var incoming = ScriptableObject.CreateInstance<ActionDataSO>();
        incoming.Category = ActionCategory.Movement;
        incoming.InterruptPriority = 10;

        var intent = BuildDirectionalIntent(SkillEntrySlot.Space, Vector2.up);
        Assert.IsTrue(ActionInterruptResolver.CanInterrupt(
            current, 0.5f, in intent, incoming));
    }

    [Test]
    public void InterruptWindow_DeniesWhenCategoryMismatch()
    {
        var current = ScriptableObject.CreateInstance<ActionDataSO>();
        current.Windows = new System.Collections.Generic.List<ActionWindow>
        {
            new ActionWindow
            {
                NormalizedStart = 0f,
                NormalizedEnd = 1f,
                InterruptibleByCategories = ActionCategory.Offense,
            },
        };

        var incoming = ScriptableObject.CreateInstance<ActionDataSO>();
        incoming.Category = ActionCategory.Movement;

        var intent = BuildDirectionalIntent(SkillEntrySlot.Space, Vector2.up);
        Assert.IsFalse(ActionInterruptResolver.CanInterrupt(
            current, 0.5f, in intent, incoming));
    }

    static NormalRouteDefinition CreateNormalRoute(string suffix)
    {
        var route = ScriptableObject.CreateInstance<NormalRouteDefinition>();
        route.name = suffix;
        var stage = ScriptableObject.CreateInstance<SkillStageDefinition>();
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        action.Duration = 0.2f;
        SetPrivateField(stage, "action", action);
        SetPrivateField(route, "stages", new[] { stage });
        return route;
    }

    static SkillRouteDefinition ResolveDirectional(SkillEntryService service, Vector2 axis)
    {
        var intent = BuildDirectionalIntent(SkillEntrySlot.Space, axis);
        InputSnapshot snap = default;
        snap.TriggerSlot = SkillEntrySlot.Space;
        snap.MoveBuffered = axis;
        var rt = service.TryResolveForIntent(in intent, in snap, now: 1f);
        Assert.IsNotNull(rt, $"axis={axis}");
        return rt.Definition;
    }

    static GameplayIntent BuildDirectionalIntent(SkillEntrySlot slot, Vector2 axis)
    {
        var intent = GameplayIntent.ForEntry(
            slot,
            time: 1f,
            bufferSeconds: 0.18f,
            requiredAll: 0UL,
            requiredAny: 0UL,
            forbidden: 0UL,
            moveBuffered: axis,
            moveBufferValid: true);
        intent.Semantic = InputSemanticType.Directional;
        intent.DirectionAxis = axis;
        return intent;
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

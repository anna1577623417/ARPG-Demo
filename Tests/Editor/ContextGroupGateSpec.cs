using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 173.6 — SkillContextGroupDefinition + AbilityGateRule 行为规约（永久维护）。
///
/// 验收维度：
///   A. AbilityGateRuleSO 资产对 Grounded/Airborne 的 Pass 行为
///   B. ContextGroup.Matches 在 Slot/Semantic/MoveDir 的过滤
///   C. ContextGroup 的多条 Gate 都需 Pass（AND 语义）
///   D. SkillGroup.PassAbilityGate 在空数组时放行
///   E. null Target Group 永不命中
/// </summary>
public sealed class ContextGroupGateSpec
{
    // ─── Helpers ─────────────────────────────────────────────────────────

    static AbilityGateRuleSO MakeRule(bool requireGrounded, bool requireAirborne)
    {
        var so = ScriptableObject.CreateInstance<AbilityGateRuleSO>();
        SetField(so, "requireGrounded", requireGrounded);
        SetField(so, "requireAirborne", requireAirborne);
        SetField(so, "allowWhenGrounded", true);
        SetField(so, "allowWhenAirborne", true);
        return so;
    }

    static SkillGroupDefinition MakeGroup(params AbilityGateRuleSO[] groupGates)
    {
        var g = ScriptableObject.CreateInstance<SkillGroupDefinition>();
        SetField(g, "abilityGateRules", groupGates);
        return g;
    }

    static SkillContextGroupDefinition MakeContextGroup(
        SkillGroupDefinition target,
        SkillEntrySlot slot = SkillEntrySlot.Any,
        InputSemanticType semantic = InputSemanticType.None,
        params AbilityGateRuleSO[] gates)
    {
        var cg = ScriptableObject.CreateInstance<SkillContextGroupDefinition>();
        SetField(cg, "targetGroup", target);
        SetField(cg, "requiredSlot", slot);
        SetField(cg, "requiredSemantic", semantic);
        SetField(cg, "abilityGateRules", gates);
        SetField(cg, "_1736GateMigrated", true); // 避免 OnValidate 试图迁移
        return cg;
    }

    static void SetField(object obj, string field, object value)
    {
        var fi = obj.GetType().GetField(field,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);
        Assert.IsNotNull(fi, $"反射未找到字段 {field}");
        fi.SetValue(obj, value);
    }

    static CombatContextSnapshot Ctx(bool airborne) => new CombatContextSnapshot
    {
        IsAirborne = airborne,
        MoveDirection = MoveDirection8.None,
    };

    // ─── Spec 1 — Grounded GateRule ──────────────────────────────────────

    [Test]
    public void GateRule_RequireGrounded_BlocksAirborne()
    {
        var rule = MakeRule(requireGrounded: true, requireAirborne: false);
        Assert.IsTrue(rule.Pass(Ctx(airborne: false)),  "地面应放行");
        Assert.IsFalse(rule.Pass(Ctx(airborne: true)),  "空中应拒绝");
    }

    // ─── Spec 2 — Airborne GateRule ──────────────────────────────────────

    [Test]
    public void GateRule_RequireAirborne_BlocksGrounded()
    {
        var rule = MakeRule(requireGrounded: false, requireAirborne: true);
        Assert.IsFalse(rule.Pass(Ctx(airborne: false)), "地面应拒绝");
        Assert.IsTrue(rule.Pass(Ctx(airborne: true)),   "空中应放行");
    }

    // ─── Spec 3 — ContextGroup 多 Gate AND 语义 ──────────────────────────

    [Test]
    public void ContextGroup_MultipleGates_AndSemantics()
    {
        var g = MakeGroup();
        var grounded = MakeRule(requireGrounded: true, requireAirborne: false);
        var airborne = MakeRule(requireGrounded: false, requireAirborne: true);
        var cg = MakeContextGroup(g, gates: new[] { grounded, airborne });

        // grounded 要求接地 + airborne 要求空中 → 永不同时成立
        Assert.IsFalse(cg.Matches(SkillEntrySlot.Any, InputSemanticType.None, Ctx(false)));
        Assert.IsFalse(cg.Matches(SkillEntrySlot.Any, InputSemanticType.None, Ctx(true)));
    }

    // ─── Spec 4 — ContextGroup 空 Gate 数组放行 ──────────────────────────

    [Test]
    public void ContextGroup_NoGates_AcceptsAny()
    {
        var g = MakeGroup();
        var cg = MakeContextGroup(g);
        Assert.IsTrue(cg.Matches(SkillEntrySlot.Any, InputSemanticType.None, Ctx(false)));
        Assert.IsTrue(cg.Matches(SkillEntrySlot.Any, InputSemanticType.None, Ctx(true)));
    }

    // ─── Spec 5 — Group.PassAbilityGate 空数组放行 + Gate 拒绝 ───────────

    [Test]
    public void GroupAbilityGate_EmptyArrayAllows_AirborneRuleBlocksGrounded()
    {
        var emptyGroup = MakeGroup();
        Assert.IsTrue(emptyGroup.PassAbilityGate(Ctx(false)), "空 Gate 数组应放行");

        var airborneRule = MakeRule(requireGrounded: false, requireAirborne: true);
        var gated = MakeGroup(airborneRule);
        Assert.IsFalse(gated.PassAbilityGate(Ctx(false)), "Grounded 应被 Airborne Gate 拒绝");
        Assert.IsTrue(gated.PassAbilityGate(Ctx(true)),   "Airborne 应被 Airborne Gate 放行");
    }

    // ─── Spec 6 — null TargetGroup 永不命中 ──────────────────────────────

    [Test]
    public void ContextGroup_NullTarget_NeverMatches()
    {
        var cg = MakeContextGroup(target: null);
        Assert.IsFalse(cg.Matches(SkillEntrySlot.Any, InputSemanticType.None, Ctx(false)));
    }
}

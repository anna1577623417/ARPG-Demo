using NUnit.Framework;
using UnityEngine;

/// <summary>177.1 — Stance 系统 EditMode 验收测试。</summary>
public sealed class StanceSystemTests
{
    GameObject _go;
    StanceController _ctrl;
    StanceDefinitionSO _normal;
    StanceDefinitionSO _crouch;

    [SetUp]
    public void Setup()
    {
        _normal = ScriptableObject.CreateInstance<StanceDefinitionSO>();
        _normal.Id = StanceId.Normal;
        _normal.DisplayName = "Normal";
        _normal.HeightScale = 1f;
        _normal.JumpContextStanceByte = (byte)StanceId.Normal;

        _crouch = ScriptableObject.CreateInstance<StanceDefinitionSO>();
        _crouch.Id = StanceId.Crouch;
        _crouch.DisplayName = "Crouch";
        _crouch.HeightScale = 0.65f;
        _crouch.WalkMultiplier = 0.55f;
        _crouch.RunMultiplier = 0.85f;
        _crouch.BlockedAbilityNames = new[] { "Dodge.Roll" };
        _crouch.GrantedAbilityNames = new[] { "Stealth.Step" };
        _crouch.JumpContextStanceByte = (byte)StanceId.Crouch;

        _go = new GameObject("StanceTest");
        _ctrl = _go.AddComponent<StanceController>();
        // 通过反射注入 m_normalStance / m_stances（私有字段）
        InjectPrivate(_ctrl, "m_normalStance", _normal);
        InjectPrivate(_ctrl, "m_stances", new[] { _normal, _crouch });
        _ctrl.EditorRegister(_normal);
        _ctrl.EditorRegister(_crouch);
        // 强制重新 Awake 状态
        InvokePrivate(_ctrl, "Awake");
    }

    [TearDown]
    public void TearDown()
    {
        if (_go != null) Object.DestroyImmediate(_go);
        if (_normal != null) Object.DestroyImmediate(_normal);
        if (_crouch != null) Object.DestroyImmediate(_crouch);
    }

    // ─── 基本切换 ──────────────────────────────────────────

    [Test]
    public void Initial_State_IsNormal()
    {
        Assert.AreEqual(StanceId.Normal, _ctrl.CurrentId);
        Assert.AreEqual(1f, _ctrl.HeightScale, 0.001f);
    }

    [Test]
    public void Request_Crouch_StartsTransition()
    {
        var ok = _ctrl.TryRequestStance(StanceId.Crouch, StanceTransitionReason.PlayerInput, out var reason);
        Assert.IsTrue(ok, $"切换被拒绝: {reason}");
        Assert.IsTrue(_ctrl.IsTransitioning);
        Assert.AreEqual(StanceId.Crouch, _ctrl.Pending.Id);
    }

    [Test]
    public void Tick_AdvancesTransition_AndCompletes()
    {
        _ctrl.TryRequestStance(StanceId.Crouch, StanceTransitionReason.PlayerInput, out _);
        // 推进足够长时间一次性走完过渡
        _ctrl.Tick(10f);
        Assert.IsFalse(_ctrl.IsTransitioning);
        Assert.AreEqual(StanceId.Crouch, _ctrl.CurrentId);
        Assert.AreEqual(0.65f, _ctrl.HeightScale, 0.001f, "完成后高度应为目标 Stance 的 HeightScale");
    }

    [Test]
    public void Request_SameStance_Rejected()
    {
        var ok = _ctrl.TryRequestStance(StanceId.Normal, StanceTransitionReason.PlayerInput, out var reason);
        Assert.IsFalse(ok);
        Assert.AreEqual("already-in-target", reason);
    }

    [Test]
    public void Switch_Cooldown_BlocksRapidToggling()
    {
        _ctrl.TryRequestStance(StanceId.Crouch, StanceTransitionReason.PlayerInput, out _);
        _ctrl.Tick(10f);
        // 立刻再请求切换 → 冷却内拒绝
        var ok = _ctrl.TryRequestStance(StanceId.Normal, StanceTransitionReason.PlayerInput, out var reason);
        Assert.IsFalse(ok);
        Assert.AreEqual("cooldown", reason);
    }

    // ─── BlockedAbilities ─────────────────────────────────

    [Test]
    public void Crouch_Blocks_DodgeRoll()
    {
        _ctrl.TryRequestStance(StanceId.Crouch, StanceTransitionReason.PlayerInput, out _);
        _ctrl.Tick(10f);
        Assert.IsTrue(_ctrl.IsAbilityBlocked("Dodge.Roll"));
        Assert.IsFalse(_ctrl.IsAbilityBlocked("Jump"), "Crouch 不应屏蔽 Jump（Crouch+Sprint+Space → LongJump）");
    }

    [Test]
    public void Normal_DoesNotBlock_Anything()
    {
        Assert.IsFalse(_ctrl.IsAbilityBlocked("Dodge.Roll"));
        Assert.IsFalse(_ctrl.IsAbilityBlocked("Jump"));
    }

    // ─── 175.2 兼容桥 ──────────────────────────────────────

    [Test]
    public void Bridge_FillsJumpContext_WithStanceByte()
    {
        _ctrl.TryRequestStance(StanceId.Crouch, StanceTransitionReason.PlayerInput, out _);
        _ctrl.Tick(10f);

        var ctx = new JumpContext();
        StanceJumpBridge.Fill(ref ctx, _ctrl);
        Assert.AreEqual((byte)StanceId.Crouch, ctx.CurrentStance);
        Assert.IsTrue(ctx.CrouchHeld, "Crouch Stance 应自动置 CrouchHeld=true");
    }

    [Test]
    public void Bridge_LongJumpVariant_MatchesWhenCrouchAndSprintHeld()
    {
        _ctrl.TryRequestStance(StanceId.Crouch, StanceTransitionReason.PlayerInput, out _);
        _ctrl.Tick(10f);

        var ctx = new JumpContext
        {
            Grounded = true,
            SprintHeld = true,  // 玩家持续按 Sprint
        };
        StanceJumpBridge.Fill(ref ctx, _ctrl);

        var longJumpCondition = JumpTriggerCondition.Any;
        longJumpCondition.RequireGroundedTri = 1;
        longJumpCondition.RequireCrouchHeld = true;
        longJumpCondition.RequireSprintHeld = true;

        Assert.IsTrue(JumpVariantResolver.Matches(in longJumpCondition, in ctx),
            "Stance=Crouch + Sprint 按住 → 175.2 LongJump Variant 条件应命中");
    }

    [Test]
    public void Bridge_NullStance_DoesNotCrash()
    {
        var ctx = new JumpContext();
        StanceJumpBridge.Fill(ref ctx, null);
        Assert.AreEqual((byte)0, ctx.CurrentStance);
        Assert.IsFalse(StanceJumpBridge.IsBlocked(null, "Anything"));
    }

    // ─── Utility ──────────────────────────────────────────

    static void InjectPrivate(object obj, string fieldName, object value)
    {
        var f = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(f, $"field {fieldName} not found");
        f.SetValue(obj, value);
    }

    static void InvokePrivate(object obj, string methodName)
    {
        var m = obj.GetType().GetMethod(methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        m?.Invoke(obj, null);
    }
}

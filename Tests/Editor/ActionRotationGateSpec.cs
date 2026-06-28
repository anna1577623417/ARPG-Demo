using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 198.3 — ActionRotationGate 永久行为规约（非临时测试，永久维护）。
///
/// 命名约定：Spec = Specification（规约），与 RSpec / Jasmine 业界 BDD 风格对齐。
/// 内容契约：当前规约描述"动作期间玩家输入触发转向"的二态性行为，
///           任何修改 ActionRotationGate / ActionWindow.AllowFacingInput|AllowMoveInput
///           的人必须保证本套规约 100% 通过。
///
/// 验收维度：
///   A. ActionWindow.AllowFacingInput / AllowMoveInput 默认值 = false（核心默认拒绝契约）
///   B. 数据结构：Allow Facing / Move 两个维度独立
///   C. 时间窗口语义：NormalizedStart / End 切片
///   D. null 容错
/// </summary>
public sealed class ActionRotationGateSpec
{
    // ─────────────────────────────────────────────────────────────────
    //  Spec 1：null player → 编辑器场景容错（放行）
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void NullPlayer_ShouldBeAllowed()
    {
        var allowed = ActionRotationGate.IsAllowed(null, ActionRotationGate.Kind.Facing);
        Assert.IsTrue(allowed, "null Player 应放行，避免编辑器场景误判");
    }

    // ─────────────────────────────────────────────────────────────────
    //  Spec 2：ActionWindow 默认值 = 双 false（核心默认拒绝契约）
    //  ★ 这条规约直接对应 198.2 log 的根因修复 —— 新建窗口默认不允许玩家转向
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void ActionWindow_DefaultValues_BlockBothFacingAndMove()
    {
        var w = new ActionWindow();
        Assert.IsFalse(w.AllowFacingInput, "默认应屏蔽玩家逻辑转向（修复 198.2）");
        Assert.IsFalse(w.AllowMoveInput, "默认应屏蔽玩家移动叠加（修复 198.2）");
    }

    // ─────────────────────────────────────────────────────────────────
    //  Spec 3：Facing / Move 维度独立（维度正交契约）
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void ActionWindow_FacingAndMoveDimensions_AreIndependent()
    {
        var wFacingOnly = new ActionWindow { AllowFacingInput = true, AllowMoveInput = false };
        Assert.IsTrue(wFacingOnly.AllowFacingInput);
        Assert.IsFalse(wFacingOnly.AllowMoveInput);

        var wMoveOnly = new ActionWindow { AllowFacingInput = false, AllowMoveInput = true };
        Assert.IsFalse(wMoveOnly.AllowFacingInput);
        Assert.IsTrue(wMoveOnly.AllowMoveInput);

        var wBoth = new ActionWindow { AllowFacingInput = true, AllowMoveInput = true };
        Assert.IsTrue(wBoth.AllowFacingInput);
        Assert.IsTrue(wBoth.AllowMoveInput);
    }

    // ─────────────────────────────────────────────────────────────────
    //  Spec 4：窗口边界 NormalizedStart / End 时间切片语义
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void ActionWindow_NormalizedRange_ClampsToZeroOne()
    {
        var w = new ActionWindow { NormalizedStart = 0.3f, NormalizedEnd = 0.7f };
        Assert.AreEqual(0.3f, w.NormalizedStart, 0.001f);
        Assert.AreEqual(0.7f, w.NormalizedEnd, 0.001f);
        Assert.Less(w.NormalizedStart, w.NormalizedEnd, "Start < End");
    }

    // ─────────────────────────────────────────────────────────────────
    //  Spec 5：Kind 枚举语义稳定（防止重命名意外）
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void Kind_EnumValues_AreStable()
    {
        Assert.AreEqual(0, (int)ActionRotationGate.Kind.Facing, "Facing 应稳定为 0");
        Assert.AreEqual(1, (int)ActionRotationGate.Kind.Move, "Move 应稳定为 1");
    }

    // ─────────────────────────────────────────────────────────────────
    //  Spec 6：EnableRotationInput 总开关默认 = false（双保险契约）
    //  ★ 关键不变量：即使 Window 配了 AllowFacing/Move=true，总开关关闭时仍屏蔽
    // ─────────────────────────────────────────────────────────────────

    [Test]
    public void ActionData_EnableRotationInput_DefaultsToFalse()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        try
        {
            Assert.IsFalse(action.EnableRotationInput,
                "总开关默认必须为 false，确保新建 Action 不会意外允许玩家转向（修复 198.2 默认行为）");
        }
        finally
        {
            Object.DestroyImmediate(action);
        }
    }
}

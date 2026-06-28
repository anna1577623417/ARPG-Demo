/// <summary>
/// 177.1 + 175.2 兼容桥 —— 把 StanceController 状态映射进 JumpContext。
/// <para>Player.cs 接入路径：</para>
/// <para>  var ctx = new JumpContext { ... };</para>
/// <para>  StanceJumpBridge.Fill(ref ctx, stanceController);</para>
/// </summary>
public static class StanceJumpBridge
{
    /// <summary>把 Stance 信息填入 JumpContext（不覆盖调用方已设置的非 Stance 字段）。</summary>
    public static void Fill(ref JumpContext ctx, StanceController stance)
    {
        if (stance == null)
        {
            ctx.CurrentStance = 0;
            return;
        }

        ctx.CurrentStance = stance.JumpContextStanceByte;

        // Stance=Crouch 时自动置 CrouchHeld，让 175.2 LongJump Variant 条件 RequireCrouchHeld=true 命中
        if (stance.IsCrouchHeld)
        {
            ctx.CrouchHeld = true;
        }
    }

    /// <summary>检查当前 Stance 是否屏蔽指定 Ability（W3 引入 AbilityId 前用字符串）。</summary>
    public static bool IsBlocked(StanceController stance, string abilityName)
        => stance != null && stance.IsAbilityBlocked(abilityName);
}

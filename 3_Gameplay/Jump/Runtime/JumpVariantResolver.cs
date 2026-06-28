using System.Collections.Generic;

/// <summary>
/// 175.2 W1 — Variant 优先级匹配。按 Priority 升序扫描，第一个 Match 即返回。
/// <para>不存在多 Variant 投票；失配自动降级到 Default（Normal）。</para>
/// </summary>
public sealed class JumpVariantResolver
{
    readonly List<JumpVariantSO> _sorted = new(16);
    JumpVariantSO _default;

    /// <summary>设置 Variant 池（自动按 Priority 升序排序）。</summary>
    public void SetVariants(IReadOnlyList<JumpVariantSO> variants, JumpVariantSO defaultVariant)
    {
        _sorted.Clear();
        if (variants != null)
        {
            for (var i = 0; i < variants.Count; i++)
            {
                if (variants[i] != null) _sorted.Add(variants[i]);
            }
            _sorted.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }
        _default = defaultVariant;
    }

    public IReadOnlyList<JumpVariantSO> Sorted => _sorted;
    public JumpVariantSO DefaultVariant => _default;

    /// <summary>按上下文解析；永不返回 null（失配返回 _default）。</summary>
    public JumpVariantSO Resolve(in JumpContext ctx)
    {
        // 强制 Override（BackflipDevice 等）
        if (ctx.ForcedVariant != JumpVariantId.Normal)
        {
            for (var i = 0; i < _sorted.Count; i++)
            {
                if (_sorted[i].Id == ctx.ForcedVariant) return _sorted[i];
            }
        }

        for (var i = 0; i < _sorted.Count; i++)
        {
            var v = _sorted[i];
            if (Matches(v.Condition, in ctx)) return v;
        }
        return _default;
    }

    /// <summary>暴露给单元测试。</summary>
    public static bool Matches(in JumpTriggerCondition c, in JumpContext ctx)
    {
        if (c.RequireGroundedTri == 1 && !ctx.Grounded) return false;
        if (c.RequireGroundedTri == 0 && ctx.Grounded) return false;

        if (c.RequireSprintHeld && !ctx.SprintHeld) return false;
        if (c.RequireCrouchHeld && !ctx.CrouchHeld) return false;
        if (c.RequireWallContact && !ctx.WallContact) return false;
        if (c.RequireDownInput && !ctx.DownInputHeld) return false;
        if (c.RequireAttackPressed && !ctx.AttackPressed) return false;
        if (c.RequireSpinPressed && !ctx.SpinPressed) return false;

        if (c.RequirePivotWithinSec > 0f)
        {
            if (ctx.TimeSinceLastPivot < 0f || ctx.TimeSinceLastPivot > c.RequirePivotWithinSec)
            {
                return false;
            }
        }

        if (c.MinHorizontalSpeed >= 0f && ctx.HorizontalSpeed < c.MinHorizontalSpeed) return false;
        if (c.MaxHorizontalSpeed >= 0f && ctx.HorizontalSpeed > c.MaxHorizontalSpeed) return false;

        if (c.RequireComboIndex >= 0 && ctx.RecentLandComboIndex != c.RequireComboIndex) return false;

        if (c.RequireLandWithinSec > 0f)
        {
            if (ctx.TimeSinceLastLand < 0f || ctx.TimeSinceLastLand > c.RequireLandWithinSec)
            {
                return false;
            }
        }

        if (c.RequireRemainingJumpsAtLeast >= 0
            && ctx.RemainingJumps < c.RequireRemainingJumpsAtLeast)
        {
            return false;
        }

        return true;
    }
}

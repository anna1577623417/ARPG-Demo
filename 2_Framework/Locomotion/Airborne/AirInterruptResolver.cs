using UnityEngine;

/// <summary>
/// 168.3 — 空中状态可中断闸门 L1 解析器。
///
/// ═══ 职责 ═══
///   · 唯一入口：<see cref="PlayerAirborneState.TryConsumeGameplayIntent"/>
///   · 输入：Player + incoming ActionCategory + 系统硬下限 mask
///   · 输出：Phase + Verdict（Allow / HardFloorBlock / LoadoutBlock / LoadoutMissing）
///
/// ═══ 边界 ═══
///   · 不查"具体技能能不能释放"（那是 AbilityGateService / L2）
///   · 不动 Edge / LateWindow / Route / airComboRoute（保留现有语义）
///   · L1 不通则 L2 不查（节流，避免误导探针）
/// </summary>
public static class AirInterruptResolver
{
    public enum Verdict : byte
    {
        Allow,
        HardFloorBlock,
        LoadoutBlock,
        LoadoutMissing,
    }

    public readonly struct Result
    {
        public readonly Verdict Code;
        public readonly AirbornePhaseMask Phase;
        public readonly ActionCategory AllowedMaskForPhase;

        public Result(Verdict code, AirbornePhaseMask phase, ActionCategory allowed)
        {
            Code = code; Phase = phase; AllowedMaskForPhase = allowed;
        }
    }

    public static Result Evaluate(
        Player player,
        ActionCategory incomingCategory,
        ActionCategory hardFloorBlockMask)
    {
        var phase = ResolvePhase(player);

        // ① 系统级硬下限（Cinematic / Dead 等）
        if (incomingCategory != ActionCategory.None
            && (hardFloorBlockMask & incomingCategory) != 0)
        {
            return new Result(Verdict.HardFloorBlock, phase, ActionCategory.None);
        }

        // ② Loadout 空中可中断 mask
        var loadout = player.SkillEntryLoadout;
        if (loadout == null)
        {
            return new Result(Verdict.LoadoutMissing, phase, ActionCategory.None);
        }

        var policy = loadout.AirInterruptPolicy;
        var allowed = phase switch
        {
            AirbornePhaseMask.Ascending  => policy.AscendingAllowed,
            AirbornePhaseMask.ApexHold   => policy.ApexAllowed,
            AirbornePhaseMask.Descending => policy.DescendingAllowed,
            _ => ActionCategory.None,
        };

        // 168.3 兜底：策划未配置（全 None）时回退到 Universal 预设，避免再次触发 168.1 BUG
        if (policy.AscendingAllowed == ActionCategory.None
            && policy.ApexAllowed == ActionCategory.None
            && policy.DescendingAllowed == ActionCategory.None)
        {
            allowed = ActionCategoryPresets.AllPillar;
        }

        if (incomingCategory != ActionCategory.None && (allowed & incomingCategory) == 0)
        {
            return new Result(Verdict.LoadoutBlock, phase, allowed);
        }

        return new Result(Verdict.Allow, phase, allowed);
    }

    public static AirbornePhaseMask ResolvePhase(Player player)
    {
        var threshold = player.SkillEntryLoadout != null
            ? player.SkillEntryLoadout.AirInterruptPolicy.ApexVyThreshold
            : 1.5f;
        if (threshold < 0.01f) threshold = 1.5f;

        var vy = player.VerticalSpeed;
        if (Mathf.Abs(vy) < threshold) return AirbornePhaseMask.ApexHold;
        return vy > 0f ? AirbornePhaseMask.Ascending : AirbornePhaseMask.Descending;
    }
}

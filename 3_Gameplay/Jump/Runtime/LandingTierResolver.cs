/// <summary>
/// 175.2 W5 — 落地 Tier 解析（纯函数 / 0-GC）。
/// <para>规则：GroundPound 直接 Pound；蓝羽免摔伤优先；其余按高度分档。</para>
/// </summary>
public static class LandingTierResolver
{
    public static LandingTier Resolve(
        float fallHeightMeters,
        JumpVariantSO variant,
        JumpModifierStack mods,
        JumpModifierSO hopooFeather,
        LandingTierProfileSO profile)
    {
        if (variant != null && variant.Id == JumpVariantId.GroundPound)
        {
            return LandingTier.Pound;
        }

        var hopoo = hopooFeather != null && mods != null && mods.Has(hopooFeather);
        if (hopoo && fallHeightMeters > (profile != null ? profile.RollThreshold : 8f))
        {
            // 蓝羽免摔伤：Roll 降级为 Light
            return LandingTier.Light;
        }

        var softT = profile != null ? profile.SoftThreshold : 1.5f;
        var normalT = profile != null ? profile.NormalThreshold : 3f;
        var heavyT = profile != null ? profile.HeavyThreshold : 5f;
        var rollT = profile != null ? profile.RollThreshold : 8f;

        if (fallHeightMeters < softT) return LandingTier.Light;
        if (fallHeightMeters < normalT) return LandingTier.Soft;
        if (fallHeightMeters < heavyT) return LandingTier.Normal;
        if (fallHeightMeters < rollT) return LandingTier.Heavy;
        return LandingTier.Roll;
    }
}

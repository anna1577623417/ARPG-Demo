using System.Collections.Generic;
using NUnit.Framework;

/// <summary>180.1 W0 — StatPipeline V2 公式验收（5 公式锁死 + V1 向后兼容）。</summary>
public sealed class StatPipelineV2Tests
{
    static Modifier M(ModifierStage stage, float v, object source = null)
        => new Modifier(StatType.AttackPower, stage, v, source ?? "test");

    // ─── §11.1 数值正确性 ──────────────────────────────────

    [Test]
    public void Composite_FiveStage_Formula()
    {
        // Base=100 + Flat+30 + PctAdd+0.10 + PctMul×1.5 + FinalAdd+20
        // (100+30) × 1.10 × 1.5 + 20 = 234.5
        var mods = new List<Modifier>
        {
            M(ModifierStage.Flat, 30f),
            M(ModifierStage.PercentAdd, 0.10f),
            M(ModifierStage.PercentMul, 0.5f),  // = ×1.5
            M(ModifierStage.FinalAdd, 20f),
        };
        var result = StatPipeline.Evaluate(100f, mods);
        Assert.AreEqual(234.5f, result, 0.001f);
    }

    [Test]
    public void ThreeSetBonus_LinearStacking()
    {
        // 三件套各 +10% PctAdd → 100 × 1.30 = 130
        var mods = new List<Modifier>
        {
            M(ModifierStage.PercentAdd, 0.10f),
            M(ModifierStage.PercentAdd, 0.10f),
            M(ModifierStage.PercentAdd, 0.10f),
        };
        Assert.AreEqual(130f, StatPipeline.Evaluate(100f, mods), 0.001f);
    }

    [Test]
    public void TwoBuffs_IndependentMultiplicative()
    {
        // 狂暴 ×1.5 + 嗜血 ×1.2 = 100 × 1.5 × 1.2 = 180
        var mods = new List<Modifier>
        {
            M(ModifierStage.PercentMul, 0.5f),
            M(ModifierStage.PercentMul, 0.2f),
        };
        Assert.AreEqual(180f, StatPipeline.Evaluate(100f, mods), 0.001f);
    }

    [Test]
    public void Silence_PercentMul_ZeroValue()
    {
        // Base=100 + PctAdd+0.5 → 150；× (1 + (-1)) = 0
        var mods = new List<Modifier>
        {
            M(ModifierStage.PercentAdd, 0.5f),
            M(ModifierStage.PercentMul, -1f),
        };
        Assert.AreEqual(0f, StatPipeline.Evaluate(100f, mods), 0.001f);
    }

    [Test]
    public void Override_BeatsEverything()
    {
        var mods = new List<Modifier>
        {
            M(ModifierStage.Flat, 999f),
            M(ModifierStage.PercentMul, 5f),
            M(ModifierStage.Override, 1f),
        };
        Assert.AreEqual(1f, StatPipeline.Evaluate(100f, mods), 0.001f);
    }

    // ─── V1 向后兼容 ─────────────────────────────────────

    [Test]
    public void V1_Add_EquivalentToFlat()
    {
        var add = new List<Modifier> { M(ModifierStage.Add, 30f) };
        var flat = new List<Modifier> { M(ModifierStage.Flat, 30f) };
        Assert.AreEqual(
            StatPipeline.Evaluate(100f, add),
            StatPipeline.Evaluate(100f, flat),
            0.001f);
    }

    [Test]
    public void V1_Mul_EquivalentToPercentAdd()
    {
        var v1 = new List<Modifier>
        {
            M(ModifierStage.Mul, 0.1f),
            M(ModifierStage.Mul, 0.2f),
        };
        var v2 = new List<Modifier>
        {
            M(ModifierStage.PercentAdd, 0.1f),
            M(ModifierStage.PercentAdd, 0.2f),
        };
        Assert.AreEqual(
            StatPipeline.Evaluate(100f, v1),
            StatPipeline.Evaluate(100f, v2),
            0.001f);
    }

    [Test]
    public void Result_NonNegative_Clamp()
    {
        var mods = new List<Modifier>
        {
            M(ModifierStage.FinalAdd, -9999f),
        };
        Assert.AreEqual(0f, StatPipeline.Evaluate(100f, mods), 0.001f);
    }
}

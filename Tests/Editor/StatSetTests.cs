#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

public sealed class StatSetTests
{
    [Test]
    public void ModifierOrder_AddThenMulThenFinalMul_MatchesPipeline()
    {
        var stats = new StatSet();
        stats.SetBase(StatType.AttackPower, 100f);

        var srcA = new object();
        var srcB = new object();
        var srcC = new object();
        stats.AddModifier(new Modifier(StatType.AttackPower, ModifierStage.Add, 20f, srcA));
        stats.AddModifier(new Modifier(StatType.AttackPower, ModifierStage.Mul, 0.5f, srcB));
        stats.AddModifier(new Modifier(StatType.AttackPower, ModifierStage.FinalMul, 0.2f, srcC));

        // (100 + 20) * 1.5 * 1.2 = 216
        Assert.AreEqual(216f, stats.Get(StatType.AttackPower), 0.0001f);
    }

    [Test]
    public void RemoveAllModifiersFromSource_RollsBackToBase()
    {
        var stats = new StatSet();
        stats.SetBase(StatType.Defense, 30f);
        var source = new object();

        stats.AddModifier(new Modifier(StatType.Defense, ModifierStage.Add, 10f, source));
        stats.AddModifier(new Modifier(StatType.Defense, ModifierStage.Mul, 0.5f, source));
        Assert.Greater(stats.Get(StatType.Defense), 30f);

        var removed = stats.RemoveAllModifiersFromSource(source);
        Assert.AreEqual(2, removed);
        Assert.AreEqual(30f, stats.Get(StatType.Defense), 0.0001f);
    }

    [Test]
    public void RemoveAllModifiersFromSource_WithUnknownSource_IsNoOp()
    {
        var stats = new StatSet();
        stats.SetBase(StatType.RunSpeed, 5f);
        stats.AddModifier(new Modifier(StatType.RunSpeed, ModifierStage.Add, 1f, new object()));
        var before = stats.Get(StatType.RunSpeed);

        var removed = stats.RemoveAllModifiersFromSource(new object());

        Assert.AreEqual(0, removed);
        Assert.AreEqual(before, stats.Get(StatType.RunSpeed), 0.0001f);
    }
}
#endif

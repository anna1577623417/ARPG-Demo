#if false // Temporary: disable PlayMode tests until NUnit/Test Framework references are available.
using NUnit.Framework;
using UnityEngine;

public sealed class GameMathPlayModeTests
{
    [Test]
    public void ResourcePool_ClampsCurrent_WhenMaxDecreasesFromStats()
    {
        var stats = new StatSet();
        stats.SetBase(StatType.MaxHealth, 100f);

        var resources = new ResourcePool();
        resources.RegisterSlot(ResourceType.HP, () => stats.Get(StatType.MaxHealth), 100f);

        var buffSource = new object();
        stats.AddModifier(new Modifier(StatType.MaxHealth, ModifierStage.Add, -60f, buffSource));

        Assert.AreEqual(40f, resources.GetCurrent(ResourceType.HP), 0.0001f);
        Assert.AreEqual(40f, resources.GetMax(ResourceType.HP), 0.0001f);
    }

    [Test]
    public void BuffStack_PeriodElapsed_EventFires_ByConfiguredPeriod()
    {
        var stats = new StatSet();
        var stack = new BuffStack(stats);
        var def = ScriptableObject.CreateInstance<BuffDefinitionSO>();
        def.Duration = 1f;
        def.PeriodSeconds = 0.2f;
        def.Effects = null;

        var ticks = 0;
        stack.OnPeriodElapsed += _ => ticks++;
        stack.Apply(def, this);
        stack.Tick(0.5f);

        Assert.GreaterOrEqual(ticks, 2);
        Object.DestroyImmediate(def);
    }

    [Test]
    public void DotLikePeriodicDrain_ReducesHp_ThroughResourcePool()
    {
        var stats = new StatSet();
        stats.SetBase(StatType.MaxHealth, 100f);

        var resources = new ResourcePool();
        resources.RegisterSlot(ResourceType.HP, () => stats.Get(StatType.MaxHealth), 100f);

        var stack = new BuffStack(stats);
        var def = ScriptableObject.CreateInstance<BuffDefinitionSO>();
        def.Duration = 1f;
        def.PeriodSeconds = 0.25f;
        def.ApplyPeriodicResourceDelta = true;
        def.PeriodicResource = ResourceType.HP;
        def.PeriodicAmount = 10f;

        stack.OnPeriodElapsed += inst =>
        {
            var d = inst.Definition;
            if (d == null || !d.ApplyPeriodicResourceDelta)
            {
                return;
            }

            resources.Drain(d.PeriodicResource, d.PeriodicAmount, out _);
        };

        stack.Apply(def, this);
        stack.Tick(0.51f);

        Assert.AreEqual(80f, resources.GetCurrent(ResourceType.HP), 0.0001f);
        Object.DestroyImmediate(def);
    }

    [Test]
    public void DamagePipeline_UsesInjectedStages_WhenProvided()
    {
        var ctx = new CombatContext(
            attackerAttackPower: 50f,
            defenderDefense: 10f,
            defenderCurrentHP: 100f,
            defenderMaxHP: 100f,
            attackerTags: 0UL,
            defenderTags: 0UL);
        var hit = new HitContext(20f, false, 1.5f, Vector3.zero);

        var stages = new IDamageStage[]
        {
            new BaseDamageStage(),
            new FinalClampStage(),
        };
        var result = DamagePipeline.Compute(in ctx, in hit, stages);

        Assert.AreEqual(70f, result.FinalDamage, 0.0001f);
    }
}
#endif

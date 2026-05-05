using System.Collections.Generic;

public static class DamagePipeline
{
    static readonly List<IDamageStage> s_defaultStages = new List<IDamageStage>
    {
        new BaseDamageStage(),
        new DefenseReductionStage(),
        new CritStage(),
        new FinalClampStage(),
    };

    public static IReadOnlyList<IDamageStage> DefaultStages => s_defaultStages;

    /// <summary>替换默认 Stage 链；用于系统级注入，不需要修改 Compute 内部。</summary>
    public static void ReplaceDefaultStages(IEnumerable<IDamageStage> stages)
    {
        s_defaultStages.Clear();
        if (stages == null)
        {
            return;
        }

        foreach (var stage in stages)
        {
            if (stage != null)
            {
                s_defaultStages.Add(stage);
            }
        }
    }

    public static void AddDefaultStage(IDamageStage stage)
    {
        if (stage != null)
        {
            s_defaultStages.Add(stage);
        }
    }

    public static DamageResult Compute(
        in CombatContext ctx,
        in HitContext hit,
        IReadOnlyList<IDamageStage> stages = null)
    {
        var activeStages = stages ?? s_defaultStages;
        var damage = hit.BaseDamage;
        for (var i = 0; i < activeStages.Count; i++)
        {
            damage = activeStages[i].Apply(damage, in ctx, in hit);
        }

        return new DamageResult(damage, hit.IsCritical);
    }
}

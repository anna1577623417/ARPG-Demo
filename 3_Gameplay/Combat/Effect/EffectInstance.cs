using System;

/// <summary>
/// 180.1 — Effect 运行时实例（按 RuntimeId 唯一回收）。
/// </summary>
public sealed class EffectInstance
{
    public readonly long RuntimeId;
    public readonly EffectDefinitionSO Definition;
    public readonly object Source;          // BuffInstance 自身 / 装备 SO / Item SO / 角色
    public readonly EffectSourceKind SourceKind;

    /// <summary>当前叠层数（AddStack 用）；其它策略恒 1。</summary>
    public int StackCount;

    /// <summary>剩余秒数（Timed 用）；double 精度，避免长时间漂移。</summary>
    public double RemainingSeconds;

    /// <summary>累积周期 timer；FixedTick while 消化超额 dt。</summary>
    public double PeriodTimer;

    /// <summary>Apply 时刻。</summary>
    public double AppliedAt;

    public EffectInstance(
        long runtimeId,
        EffectDefinitionSO def,
        object source,
        EffectSourceKind sourceKind,
        double now,
        int initialStack = 1)
    {
        if (def == null) throw new ArgumentNullException(nameof(def));
        RuntimeId = runtimeId;
        Definition = def;
        Source = source ?? def;
        SourceKind = sourceKind;
        StackCount = Math.Max(1, initialStack);
        RemainingSeconds = def.DurationSeconds;
        PeriodTimer = 0;
        AppliedAt = now;
    }
}

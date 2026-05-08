using System;

public interface IBuffStack
{
    BuffInstance Apply(BuffDefinitionSO def, object source);
    bool Remove(BuffInstance instance);
    void Tick(float deltaTime);
    int Count(BuffDefinitionSO def);
    bool Has(BuffDefinitionSO def);

    /// <summary>当前活跃 Buff 实例数量（用于 HUD 枚举）。</summary>
    int ActiveInstanceCount { get; }

    /// <summary>按下标读取活跃实例快照（用于 Bind 时种子同步）。</summary>
    BuffInstance GetActiveInstanceAt(int index);

    /// <summary>按 RuntimeId 查询实例（用于 HUD 每帧刷新剩余时间）。</summary>
    bool TryGetInstance(int runtimeId, out BuffInstance instance);

    event Action<BuffInstance> OnPeriodElapsed;
    event Action<BuffInstance> OnExpired;

    /// <summary>新 Buff 实例加入栈顶后触发。</summary>
    event Action<BuffInstance> OnApplied;

    /// <summary>实例被 Remove（含到期 Tick 内移除）后触发。</summary>
    event Action<BuffInstance> OnRemoved;

    /// <summary>同一 <see cref="BuffDefinitionSO"/> 的实例数量变化。</summary>
    event Action<BuffDefinitionSO, int> OnStackChanged;
}

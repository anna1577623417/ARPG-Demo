/// <summary>
/// 175.2 W4 — 把 Jump Modifier 接入项目通用 Buff/Effect 通道。
/// <para>本类不依赖具体 Buff 实现；它暴露 4 个事件入口供调用方触发。</para>
/// <para>实际 EffectSystem / BuffStack 接入：在 Effect 的 OnApply / OnExpire 回调里调用对应方法。</para>
/// </summary>
public sealed class JumpModifierBridge
{
    readonly JumpModifierStack _stack;

    public JumpModifierBridge(JumpModifierStack stack)
    {
        _stack = stack;
    }

    /// <summary>由 EffectSystem.OnApply 回调（道具拾取 / Buff 触发）调用。</summary>
    public void OnEffectApplied(JumpModifierSO mod)
    {
        if (mod == null) return;
        _stack.Add(mod);
    }

    /// <summary>由 EffectSystem.OnExpire 回调调用。</summary>
    public void OnEffectExpired(JumpModifierSO mod)
    {
        if (mod == null) return;
        _stack.Remove(mod);
    }

    /// <summary>由场景切换 / 死亡清场时调用。</summary>
    public void OnReset() => _stack.ClearAll();

    /// <summary>由 HUD 查询当前叠层数。</summary>
    public int GetStack(JumpModifierSO mod) => _stack.GetStack(mod);
}

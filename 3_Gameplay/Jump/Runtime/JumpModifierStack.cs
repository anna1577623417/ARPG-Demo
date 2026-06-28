using System.Collections.Generic;

/// <summary>
/// 175.2 W3 — Jump Modifier 叠层管理 + Recompute 链。
/// <para>对外只暴露 <see cref="Effective"/>（运行时副本）；BaseConfig 永远 readonly。</para>
/// <para>道具拾取 / Buff 应用通过 <see cref="Add"/>；移除通过 <see cref="Remove"/>。</para>
/// </summary>
public sealed class JumpModifierStack
{
    readonly Dictionary<JumpModifierSO, int> _stacks = new();

    JumpAbilityConfig _base;
    JumpAbilityConfig _effective;

    public JumpAbilityConfig Base => _base;
    public JumpAbilityConfig Effective => _effective;
    public IReadOnlyDictionary<JumpModifierSO, int> Stacks => _stacks;

    /// <summary>初始化：设置 BaseConfig 并立刻 Recompute。</summary>
    public void Reset(in JumpAbilityConfig baseConfig)
    {
        _base = baseConfig;
        _stacks.Clear();
        Recompute();
    }

    /// <summary>添加一层。-1 = 失败（已满栈）。</summary>
    public int Add(JumpModifierSO mod)
    {
        if (mod == null) return -1;
        _stacks.TryGetValue(mod, out var n);
        if (mod.MaxStack >= 0 && n >= mod.MaxStack) return -1;
        _stacks[mod] = n + 1;
        Recompute();
        return n + 1;
    }

    /// <summary>移除一层（&gt;0 时减 1；=1 时移除条目）。</summary>
    public bool Remove(JumpModifierSO mod)
    {
        if (mod == null || !_stacks.TryGetValue(mod, out var n)) return false;
        if (n <= 1) _stacks.Remove(mod);
        else _stacks[mod] = n - 1;
        Recompute();
        return true;
    }

    public int GetStack(JumpModifierSO mod) =>
        mod != null && _stacks.TryGetValue(mod, out var n) ? n : 0;

    /// <summary>清空所有叠层（场景切换 / 死亡）。</summary>
    public void ClearAll()
    {
        if (_stacks.Count == 0) return;
        _stacks.Clear();
        Recompute();
    }

    /// <summary>175.2 §3.3 —— 按基线副本 + 所有 Modifier 链合成 Effective。</summary>
    public void Recompute()
    {
        _effective = _base;

        var extraJumps = 0f;
        var enableGlide = false;
        var enableDashJump = false;
        var dashJumpHBonus = 0f;

        foreach (var kv in _stacks)
        {
            var mod = kv.Key;
            var n = kv.Value;
            if (mod == null || n <= 0) continue;

            // 数值字段链
            if (mod.FieldMods != null)
            {
                for (var i = 0; i < mod.FieldMods.Length; i++)
                {
                    ref readonly var fm = ref mod.FieldMods[i];
                    var v = fm.Value * n;
                    switch (fm.Op)
                    {
                        case ModifierOp.Add:
                            _effective[fm.Field] = _effective[fm.Field] + v;
                            break;
                        case ModifierOp.MulCurrent:
                            _effective[fm.Field] = _effective[fm.Field] * (1f + v);
                            break;
                        case ModifierOp.MulBase:
                            _effective[fm.Field] = _effective[fm.Field] + _base[fm.Field] * v;
                            break;
                    }
                }
            }

            // 能力位
            if (mod.AddOneAirJumpPerStack) extraJumps += n;
            if (mod.EnableGlide) enableGlide = true;
            if (mod.EnableDashJump)
            {
                enableDashJump = true;
                dashJumpHBonus += mod.DashJumpHorizontalPerStack * n;
            }
        }

        _effective.MaxJumpCount += extraJumps;
        _effective.EnableGlide = enableGlide;
        _effective.EnableDashJump = enableDashJump;
        _effective.DashJumpHorizontalBonus = dashJumpHBonus;
    }

    /// <summary>检测是否含某个 Modifier（外部分支判断用，如蓝羽免摔伤）。</summary>
    public bool Has(JumpModifierSO mod) => mod != null && _stacks.ContainsKey(mod);
}

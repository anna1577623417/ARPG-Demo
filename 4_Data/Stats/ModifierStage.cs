/// <summary>
/// 属性修饰阶段。
/// <para>V1（向后兼容）：Add / Mul / FinalMul</para>
/// <para>V2（180.1 §1.1）：Override / Flat / PercentAdd / PercentMul / FinalAdd</para>
/// <para>统一公式（180.1 §1.3）：</para>
/// <para>Final = Override ?? (Base + Σ Flat) × (1 + Σ PercentAdd) × Π (1 + PercentMul) × (1 + Σ FinalMulLegacy) + Σ FinalAdd</para>
/// </summary>
public enum ModifierStage : byte
{
    // ── V1（向后兼容；与新值共存）──────────────────────────
    /// <summary>V1 Add，等价于 V2 Flat（同被加进 Base+ 累加段）。</summary>
    Add = 0,

    /// <summary>V1 Mul（mul += value 即 1+Σv），等价于 V2 PercentAdd。</summary>
    Mul = 1,

    /// <summary>V1 FinalMul（finalMul += value 即 1+Σv）。V2 不再产生新 FinalMul Modifier；旧资产仍走 Σ Final 段。</summary>
    FinalMul = 2,

    // ── V2（180.1 新增）─────────────────────────────────
    /// <summary>强制覆盖最终值（最高优先，无视一切）。</summary>
    Override = 10,

    /// <summary>平加（+30 攻），与 Add 等价。</summary>
    Flat = 11,

    /// <summary>百分比加法（线性堆叠，3 件 10% = 30%）。</summary>
    PercentAdd = 12,

    /// <summary>独立乘区（狂暴 ×1.5 + 嗜血 ×1.2 = ×1.8 而非 ×1.7）。</summary>
    PercentMul = 13,

    /// <summary>最终加（穿透防御）。</summary>
    FinalAdd = 14,
}

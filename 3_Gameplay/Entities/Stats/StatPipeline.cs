using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 180.1 §1.3 — Stat 计算流水：5 公式锁死 + V1 向后兼容。
/// <para>Final = Override ?? (Base + Σ Flat) × (1 + Σ PercentAdd) × Π (1 + PercentMul) × (1 + Σ FinalMulLegacy) + Σ FinalAdd</para>
/// </summary>
public static class StatPipeline
{
    public static float Evaluate(float baseValue, List<Modifier> modifiers)
    {
        if (modifiers == null) return Mathf.Max(0f, baseValue);

        float? overrideVal = null;
        var flat = 0f;            // V1 Add + V2 Flat
        var pctAdd = 0f;          // V1 Mul + V2 PercentAdd
        var pctMul = 1f;          // V2 PercentMul：独立乘区
        var finalMulLegacy = 0f;  // V1 FinalMul：Σ → (1 + Σ)
        var finalAdd = 0f;        // V2 FinalAdd

        for (var i = 0; i < modifiers.Count; i++)
        {
            var m = modifiers[i];
            switch (m.Stage)
            {
                case ModifierStage.Override:
                    overrideVal = m.Value;
                    break;
                case ModifierStage.Add:
                case ModifierStage.Flat:
                    flat += m.Value;
                    break;
                case ModifierStage.Mul:
                case ModifierStage.PercentAdd:
                    pctAdd += m.Value;
                    break;
                case ModifierStage.PercentMul:
                    pctMul *= (1f + m.Value);
                    break;
                case ModifierStage.FinalMul:
                    finalMulLegacy += m.Value;
                    break;
                case ModifierStage.FinalAdd:
                    finalAdd += m.Value;
                    break;
            }
        }

        if (overrideVal.HasValue) return Mathf.Max(0f, overrideVal.Value);

        var result = (baseValue + flat) * (1f + pctAdd) * pctMul * (1f + finalMulLegacy) + finalAdd;
        return Mathf.Max(0f, result);
    }
}

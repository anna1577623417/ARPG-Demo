using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stat 计算流水：Base + Add，再乘 Mul 与 FinalMul。
/// </summary>
public static class StatPipeline
{
    public static float Evaluate(float baseValue, List<Modifier> modifiers)
    {
        var add = 0f;
        var mul = 1f;
        var finalMul = 1f;

        for (var i = 0; i < modifiers.Count; i++)
        {
            var m = modifiers[i];
            switch (m.Stage)
            {
                case ModifierStage.Add:
                    add += m.Value;
                    break;
                case ModifierStage.Mul:
                    mul += m.Value;
                    break;
                case ModifierStage.FinalMul:
                    finalMul += m.Value;
                    break;
            }
        }

        var result = (baseValue + add) * mul * finalMul;
        return Mathf.Max(0f, result);
    }
}

/// <summary>185.2 — Graph Edge Conditions[] 短路 AND 求值。</summary>
public static class EdgeConditionEvaluator
{
    public static bool Evaluate(in CombatFlowCompiledEdge edge, in EdgeContext ctx, out string firstFailLabel)
    {
        firstFailLabel = null;
        var conds = edge.EdgeConditions;
        if (conds == null || conds.Length == 0)
        {
            return true;
        }

        for (var i = 0; i < conds.Length; i++)
        {
            var c = conds[i];
            if (c == null)
            {
                continue;
            }

            if (!c.Evaluate(in ctx))
            {
                firstFailLabel = c.DebugLabel;
                return false;
            }
        }

        return true;
    }
}

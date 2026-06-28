using UnityEngine;

/// <summary>
/// 185.2 — Graph Edge 语义条件基类（Input / Phase / EventWindow / CombatContext）。
/// <para>时间窗仍归 Action.EarlyWindow / LateWindow；本类不参与 NT 判定。</para>
/// </summary>
public abstract class EdgeConditionSO : ScriptableObject
{
    public abstract bool Evaluate(in EdgeContext ctx);

    public virtual string DebugLabel => name;
}

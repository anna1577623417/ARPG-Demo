using UnityEngine;

/// <summary>
/// 行为模块 Scriptable 模板；运行时由 <see cref="AbilityTask"/> 执行。
/// </summary>
public abstract class AbilityTaskSO : ScriptableObject
{
    public abstract AbilityTask CreateInstance();
}

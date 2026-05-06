using UnityEngine;

/// <summary>伤害结算完成后的飘字请求（旁路事件，不影响核心扣血）。</summary>
public readonly struct DamageTextRequestedEvent : IGameEvent
{
    public readonly float FinalDamage;
    public readonly bool IsCritical;
    public readonly Vector3 HitPoint;

    public DamageTextRequestedEvent(float finalDamage, bool isCritical, Vector3 hitPoint)
    {
        FinalDamage = finalDamage;
        IsCritical = isCritical;
        HitPoint = hitPoint;
    }
}


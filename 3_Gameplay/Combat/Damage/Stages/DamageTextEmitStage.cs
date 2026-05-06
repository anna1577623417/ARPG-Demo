/// <summary>
/// 旁路 Stage：在最终伤害计算完成后发出飘字请求事件。
/// 放在默认链尾部以获取最终伤害值。
/// </summary>
public sealed class DamageTextEmitStage : IDamageStage
{
    public float Apply(float currentDamage, in CombatContext ctx, in HitContext hit)
    {
        GlobalEventBus.Publish(new DamageTextRequestedEvent(currentDamage, hit.IsCritical, hit.HitPoint));
        return currentDamage;
    }
}


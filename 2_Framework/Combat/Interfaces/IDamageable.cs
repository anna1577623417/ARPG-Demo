/// <summary>
/// 可受击接口。兼容现有 DamageInfo 输入与新的 DamageResult 投递。
/// </summary>
public interface IDamageable
{
    void TakeDamage(DamageInfo info);
    void ReceiveDamage(in DamageResult result, in CombatContext ctx);
}

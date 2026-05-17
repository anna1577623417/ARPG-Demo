/// <summary>
/// 实体 Ability 轨<strong>唯一写入入口</strong>（约定：禁止在其它系统直接改 <see cref="GameplayTagContainer.Ability"/>）。
/// <para>当前实现 = 基础位移规则；后续在此叠加装备 / Buff / 阻断位，再一次性写入容器。</para>
/// </summary>
public static class EntityAbilitySystem
{
    /// <summary>重算并写入 <paramref name="player"/>.Tags.Ability。</summary>
    public static void Update(Player player)
    {
        if (player == null)
        {
            return;
        }

        player.Tags.ClearAbilityTrack();

        if (player.IsDead)
        {
            return;
        }

        ulong bits = (ulong)(
            EntityCapabilityTag.CanLightAttack
            | EntityCapabilityTag.CanHeavyAttack
            | EntityCapabilityTag.CanCastAbility1
            | EntityCapabilityTag.CanCastUltimate);
        if (player.IsGrounded)
        {
            bits |= (ulong)EntityCapabilityTag.CanJump;
        }

        // Ver4.6 路由化：能力轨按入口可用性统一放开，具体是否可释放由 RouteResolver + Cost/CD/Tags 决定。
        bits |= (ulong)(EntityCapabilityTag.CanDodge | EntityCapabilityTag.CanSwordDash);

        // TODO: bits |= equipmentMask | buffMask; bits &= ~blockedMask;

        player.Tags.Add(TagCategory.Ability, bits);
    }
}

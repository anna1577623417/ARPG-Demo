using System;
using System.Runtime.CompilerServices;

/// <summary>
/// 五轨标签容器（零堆分配查询）。Event 轨不持久化，由事件总线携带 <see cref="CombatEventTag"/>。
/// <para>State 轨与现有 <see cref="StateTag"/> / <see cref="GameplayTagMask"/> 完全兼容。</para>
/// <para>Ability 轨存 <see cref="EntityCapabilityTag"/>（与技能载荷枚举 <see cref="SkillPayloadTag"/> 不要混用）。</para>
/// <para>调用方应通过 <c>ref</c> 持有容器（如 Player 上的 ref 属性），避免对 struct 做值拷贝后改掩码。</para>
/// </summary>
public struct GameplayTagContainer
{
    public GameplayTagMask State;
    public GameplayTagMask Status;
    public GameplayTagMask Ability;
    public GameplayTagMask Mechanic;
    public GameplayTagMask Faction;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearAll()
    {
        State.Clear();
        Status.Clear();
        Ability.Clear();
        Mechanic.Clear();
        Faction.Clear();
    }

    /// <summary>仅清空 Ability 轨；须通过容器调用以免对 <see cref="GameplayTagMask"/> 字段副本写入。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearAbilityTrack() => Ability.Clear();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasAll(TagCategory category, ulong bits)
    {
        switch (category)
        {
            case TagCategory.State:
                return State.HasAll(bits);
            case TagCategory.Status:
                return Status.HasAll(bits);
            case TagCategory.Ability:
                return Ability.HasAll(bits);
            case TagCategory.Mechanic:
                return Mechanic.HasAll(bits);
            case TagCategory.Faction:
                return Faction.HasAll(bits);
            case TagCategory.Event:
            default:
                throw new ArgumentOutOfRangeException(nameof(category), category, "Event 轨不存入容器；使用 CombatEventTag + 事件总线。");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasAny(TagCategory category, ulong bits)
    {
        switch (category)
        {
            case TagCategory.State:
                return State.HasAny(bits);
            case TagCategory.Status:
                return Status.HasAny(bits);
            case TagCategory.Ability:
                return Ability.HasAny(bits);
            case TagCategory.Mechanic:
                return Mechanic.HasAny(bits);
            case TagCategory.Faction:
                return Faction.HasAny(bits);
            case TagCategory.Event:
            default:
                throw new ArgumentOutOfRangeException(nameof(category), category, "Event 轨不存入容器；使用 CombatEventTag + 事件总线。");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(TagCategory category, ulong bits)
    {
        switch (category)
        {
            case TagCategory.State:
                State.Add(bits);
                break;
            case TagCategory.Status:
                Status.Add(bits);
                break;
            case TagCategory.Ability:
                Ability.Add(bits);
                break;
            case TagCategory.Mechanic:
                Mechanic.Add(bits);
                break;
            case TagCategory.Faction:
                Faction.Add(bits);
                break;
            case TagCategory.Event:
            default:
                throw new ArgumentOutOfRangeException(nameof(category), category, "Event 轨不存入容器；使用 CombatEventTag + 事件总线。");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Remove(TagCategory category, ulong bits)
    {
        switch (category)
        {
            case TagCategory.State:
                State.Remove(bits);
                break;
            case TagCategory.Status:
                Status.Remove(bits);
                break;
            case TagCategory.Ability:
                Ability.Remove(bits);
                break;
            case TagCategory.Mechanic:
                Mechanic.Remove(bits);
                break;
            case TagCategory.Faction:
                Faction.Remove(bits);
                break;
            case TagCategory.Event:
            default:
                throw new ArgumentOutOfRangeException(nameof(category), category, "Event 轨不存入容器；使用 CombatEventTag + 事件总线。");
        }
    }
}

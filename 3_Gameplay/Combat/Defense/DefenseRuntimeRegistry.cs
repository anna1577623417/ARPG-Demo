using System.Collections.Generic;

/// <summary>
/// 216.3 M5 L2 — 实体当前防御态【单一真相】。
/// <para>由 <see cref="GuardVolumeProvider"/> / <c>FireDefenseClips</c> 写入；
/// <see cref="CombatResolver"/> 只读，禁止别处再猜 StateTag 兜底。</para>
/// </summary>
public static class DefenseRuntimeRegistry
{
    sealed class Slot
    {
        public GuardVolumeProvider Guard;
        public bool ParryActive;
        public bool InvincibleActive;
    }

    static readonly Dictionary<int, Slot> s_slots = new Dictionary<int, Slot>(8);

    public static void RegisterGuard(Entity owner, GuardVolumeProvider guard)
    {
        if (owner == null || guard == null)
        {
            return;
        }

        var slot = GetOrCreate(owner);
        slot.Guard = guard;
    }

    public static void UnregisterGuard(Entity owner, GuardVolumeProvider guard)
    {
        if (owner == null || !s_slots.TryGetValue(owner.GetInstanceID(), out var slot))
        {
            return;
        }

        if (slot.Guard == guard)
        {
            slot.Guard = null;
        }

        TryRemoveEmpty(owner.GetInstanceID(), slot);
    }

    /// <summary>每帧由 FireDefenseClips 写绝对态（Parry / DefenseInvincible 窗）。</summary>
    public static void SetWindowFlags(Entity owner, bool parryActive, bool invincibleActive)
    {
        if (owner == null)
        {
            return;
        }

        var slot = GetOrCreate(owner);
        slot.ParryActive = parryActive;
        slot.InvincibleActive = invincibleActive;
        TryRemoveEmpty(owner.GetInstanceID(), slot);
    }

    public static void Clear(Entity owner)
    {
        if (owner == null)
        {
            return;
        }

        s_slots.Remove(owner.GetInstanceID());
    }

    public static bool TryGetActiveGuard(Entity owner, out GuardVolumeProvider guard)
    {
        guard = null;
        if (owner == null || !s_slots.TryGetValue(owner.GetInstanceID(), out var slot))
        {
            return false;
        }

        if (slot.Guard == null || !slot.Guard.Active)
        {
            return false;
        }

        guard = slot.Guard;
        return true;
    }

    public static bool IsParryActive(Entity owner) =>
        owner != null
        && s_slots.TryGetValue(owner.GetInstanceID(), out var slot)
        && slot.ParryActive;

    public static bool IsDefenseInvincibleActive(Entity owner) =>
        owner != null
        && s_slots.TryGetValue(owner.GetInstanceID(), out var slot)
        && slot.InvincibleActive;

    static Slot GetOrCreate(Entity owner)
    {
        var id = owner.GetInstanceID();
        if (!s_slots.TryGetValue(id, out var slot))
        {
            slot = new Slot();
            s_slots[id] = slot;
        }

        return slot;
    }

    static void TryRemoveEmpty(int id, Slot slot)
    {
        if (slot.Guard == null && !slot.ParryActive && !slot.InvincibleActive)
        {
            s_slots.Remove(id);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 216.3 M5 L3 — 当前开判的 WeaponTrace 攻击登记（拼刀检测用）。
/// <para>由 <see cref="AttackInstance"/> Begin/End/Tick 写入；Clash 检测只读。</para>
/// </summary>
public static class AttackTraceRegistry
{
    const int MaxSockets = 16;

    sealed class Slot
    {
        public Entity Owner;
        public AttackInstance Attack;
        public readonly Vector3[] Positions = new Vector3[MaxSockets];
        public readonly float[] Radii = new float[MaxSockets];
        public int Count;
    }

    static readonly Dictionary<int, Slot> s_byOwner = new Dictionary<int, Slot>(8);
    static readonly List<Slot> s_active = new List<Slot>(8);

    public static void Register(Entity owner, AttackInstance attack)
    {
        if (owner == null || attack == null)
        {
            return;
        }

        var id = owner.GetInstanceID();
        if (!s_byOwner.TryGetValue(id, out var slot))
        {
            slot = new Slot();
            s_byOwner[id] = slot;
            s_active.Add(slot);
        }

        slot.Owner = owner;
        slot.Attack = attack;
        slot.Count = 0;
    }

    public static void Unregister(Entity owner, AttackInstance attack)
    {
        if (owner == null)
        {
            return;
        }

        var id = owner.GetInstanceID();
        if (!s_byOwner.TryGetValue(id, out var slot))
        {
            return;
        }

        if (slot.Attack != attack)
        {
            return;
        }

        s_byOwner.Remove(id);
        s_active.Remove(slot);
    }

    public static void UpdateSamples(
        Entity owner,
        WeaponTraceProvider.SocketSample[] samples,
        int count)
    {
        if (owner == null || samples == null || !s_byOwner.TryGetValue(owner.GetInstanceID(), out var slot))
        {
            return;
        }

            var n = 0;
        var max = Mathf.Min(count, MaxSockets);
        for (var i = 0; i < max; i++)
        {
            var s = samples[i];
            if (!s.Valid)
            {
                continue;
            }

            slot.Positions[n] = s.Position;
            slot.Radii[n] = s.Radius > 0.01f ? s.Radius : 0.05f;
            n++;
        }

        slot.Count = n;
    }

    public static bool IsWeaponTraceActive(Entity owner) =>
        owner != null && s_byOwner.ContainsKey(owner.GetInstanceID());

    /// <summary>两实体当前 Socket 球是否相交；相交点取两球心中点。</summary>
    public static bool TryGetIntersection(Entity a, Entity b, out Vector3 point)
    {
        point = default;
        if (a == null || b == null || a == b)
        {
            return false;
        }

        if (!s_byOwner.TryGetValue(a.GetInstanceID(), out var sa)
            || !s_byOwner.TryGetValue(b.GetInstanceID(), out var sb))
        {
            return false;
        }

        return TryIntersectSlots(sa, sb, out point);
    }

    /// <summary>找与 self 轨迹相交的另一开判实体（排除 self）。</summary>
    public static bool TryFindClashOpponent(Entity self, out Entity opponent, out Vector3 point)
    {
        opponent = null;
        point = default;
        if (self == null || !s_byOwner.TryGetValue(self.GetInstanceID(), out var selfSlot))
        {
            return false;
        }

        for (var i = 0; i < s_active.Count; i++)
        {
            var other = s_active[i];
            if (other == null || other.Owner == null || other.Owner == self)
            {
                continue;
            }

            if (TryIntersectSlots(selfSlot, other, out point))
            {
                opponent = other.Owner;
                return true;
            }
        }

        return false;
    }

    /// <summary>拼刀后强制关判，避免同帧继续打身体。</summary>
    public static void ForceEndAttack(Entity owner)
    {
        if (owner == null || !s_byOwner.TryGetValue(owner.GetInstanceID(), out var slot))
        {
            return;
        }

        var attack = slot.Attack;
        if (attack != null && attack.Active)
        {
            attack.End();
        }
    }

    static bool TryIntersectSlots(Slot a, Slot b, out Vector3 point)
    {
        point = default;
        for (var i = 0; i < a.Count; i++)
        {
            var pa = a.Positions[i];
            var ra = a.Radii[i];
            for (var j = 0; j < b.Count; j++)
            {
                var pb = b.Positions[j];
                var rb = b.Radii[j];
                var limit = ra + rb;
                var delta = pb - pa;
                if (delta.sqrMagnitude <= limit * limit)
                {
                    point = pa + delta * 0.5f;
                    return true;
                }
            }
        }

        return false;
    }
}

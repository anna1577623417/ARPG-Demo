using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 216.3 M5 L3 — 拼刀会话：双方进 Clash「态」（StatusTag + 关判 + 短时锁定）。
/// <para>非新支柱状态机；挂在 Status 轨，由 <see cref="Tick"/> 到期清除。
/// 完整 Clash Action / 动画 → OPEN 配表。</para>
/// </summary>
public static class ClashSession
{
    public const float DefaultDurationSec = 0.35f;

    struct Entry
    {
        public Entity Entity;
        public float EndTime;
    }

    static readonly List<Entry> s_entries = new List<Entry>(4);

    /// <summary>双方进入拼刀态（幂等：已在会话内则刷新结束时间）。</summary>
    public static void Enter(Entity a, Entity b, float durationSec = DefaultDurationSec)
    {
        var dur = durationSec > 0.01f ? durationSec : DefaultDurationSec;
        var end = Time.time + dur;

        Stamp(a, end, b);
        Stamp(b, end, a);
    }

    public static bool IsActive(Entity entity)
    {
        if (entity == null)
        {
            return false;
        }

        var id = entity.GetInstanceID();
        for (var i = 0; i < s_entries.Count; i++)
        {
            if (s_entries[i].Entity != null && s_entries[i].Entity.GetInstanceID() == id)
            {
                return Time.time < s_entries[i].EndTime;
            }
        }

        return false;
    }

    /// <summary>每帧推进：到期清 StatusTag.Clash。由 Player 主循环调用。</summary>
    public static void Tick(float now)
    {
        for (var i = s_entries.Count - 1; i >= 0; i--)
        {
            var e = s_entries[i];
            if (e.Entity == null || now >= e.EndTime)
            {
                ClearTag(e.Entity);
                s_entries.RemoveAt(i);
            }
        }
    }

    static void Stamp(Entity self, float endTime, Entity other)
    {
        if (self == null)
        {
            return;
        }

        Upsert(self, endTime);
        AddClashTag(self);
        AttackTraceRegistry.ForceEndAttack(self);

        if (self is Player player)
        {
            player.ForceEndAttackIfActive();
        }

        if (other != null && self is IImpulseReceiver receiver)
        {
            var away = self.transform.position - other.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 1e-4f)
            {
                var request = new ImpulseRequest(
                    away,
                    1.8f,
                    0f,
                    ImpulseKind.Small,
                    other as IEntity);
                receiver.TryApplyImpulse(in request);
            }
        }
    }

    static void Upsert(Entity entity, float endTime)
    {
        var id = entity.GetInstanceID();
        for (var i = 0; i < s_entries.Count; i++)
        {
            if (s_entries[i].Entity != null && s_entries[i].Entity.GetInstanceID() == id)
            {
                s_entries[i] = new Entry { Entity = entity, EndTime = endTime };
                return;
            }
        }

        s_entries.Add(new Entry { Entity = entity, EndTime = endTime });
    }

    static void AddClashTag(Entity entity)
    {
        // Tags 为 struct：必须经具体类型的 ref 属性写入，禁止经 ITagOwner 值拷贝。
        if (entity is Player player)
        {
            player.Tags.Add(TagCategory.Status, (ulong)StatusTag.Clash);
        }
        else if (entity is Enemy_Training enemy)
        {
            enemy.Tags.Add(TagCategory.Status, (ulong)StatusTag.Clash);
        }
    }

    static void ClearTag(Entity entity)
    {
        if (entity is Player player)
        {
            player.Tags.Remove(TagCategory.Status, (ulong)StatusTag.Clash);
        }
        else if (entity is Enemy_Training enemy)
        {
            enemy.Tags.Remove(TagCategory.Status, (ulong)StatusTag.Clash);
        }
    }
}

using System;
using System.Collections.Generic;

/// <summary>按槽叠加技能 Override，支持限时与按来源移除。</summary>
public sealed class SkillOverrideManager
{
    readonly Dictionary<SkillSlotType, List<SkillOverrideEntry>> _overrides = new Dictionary<SkillSlotType, List<SkillOverrideEntry>>();

    /// <summary>最高优先级条目；若没有 Override 返回 null。</summary>
    public bool TryResolveOverride(SkillSlotType slot, out SkillOverrideEntry best)
    {
        best = default;
        if (!_overrides.TryGetValue(slot, out var list) || list == null || list.Count == 0)
        {
            return false;
        }

        best = list[0];
        for (var i = 1; i < list.Count; i++)
        {
            if (list[i].Priority > best.Priority)
            {
                best = list[i];
            }
        }

        return best.Skill != null;
    }

    public SkillDataSO ResolveSkill(SkillSlotType slot, SkillLoadoutSO baseLoadout)
    {
        if (TryResolveOverride(slot, out var ov))
        {
            return ov.Skill;
        }

        return baseLoadout != null ? baseLoadout.Resolve(slot) : null;
    }

    public void ApplyOverride(SkillSlotType slot, SkillDataSO skill, int priority, object source, float duration = -1f)
    {
        if (skill == null)
        {
            return;
        }

        if (!_overrides.TryGetValue(slot, out var list))
        {
            list = new List<SkillOverrideEntry>(4);
            _overrides[slot] = list;
        }

        list.Add(new SkillOverrideEntry
        {
            Skill = skill,
            Priority = priority,
            Source = source,
            Duration = duration,
            RemainingTime = duration,
        });
    }

    public void RemoveOverride(SkillSlotType slot, object source)
    {
        if (source == null || !_overrides.TryGetValue(slot, out var list))
        {
            return;
        }

        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (Equals(list[i].Source, source))
            {
                list.RemoveAt(i);
            }
        }
    }

    public void Tick(float deltaTime)
    {
        foreach (var kv in _overrides)
        {
            var list = kv.Value;
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var e = list[i];
                if (e.Duration > 0f)
                {
                    e.RemainingTime -= deltaTime;
                    list[i] = e;
                    if (e.RemainingTime <= 0f)
                    {
                        list.RemoveAt(i);
                    }
                }
            }
        }
    }
}

/// <summary>运行时 Override 单层。</summary>
public struct SkillOverrideEntry
{
    public SkillDataSO Skill;
    public int Priority;
    public object Source;
    /// <summary>&lt;=0 视为永久。</summary>
    public float Duration;
    public float RemainingTime;
}

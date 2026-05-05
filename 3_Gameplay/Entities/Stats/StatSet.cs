using System;
using System.Collections.Generic;

public sealed class StatSet : IStatSet
{
    sealed class Entry
    {
        public float BaseValue;
        public float CachedFinal;
        public bool Dirty = true;
        public readonly List<Modifier> Modifiers = new List<Modifier>(4);
    }

    readonly Dictionary<StatType, Entry> _entries = new Dictionary<StatType, Entry>(16);

    public event Action<StatType, float> OnFinalValueChanged;

    public float Get(StatType type)
    {
        var entry = GetOrCreate(type);
        if (!entry.Dirty)
        {
            return entry.CachedFinal;
        }

        var next = StatPipeline.Evaluate(entry.BaseValue, entry.Modifiers);
        var changed = Math.Abs(next - entry.CachedFinal) > 0.0001f;
        entry.CachedFinal = next;
        entry.Dirty = false;

        if (changed)
        {
            OnFinalValueChanged?.Invoke(type, next);
        }

        return next;
    }

    public void SetBase(StatType type, float baseValue)
    {
        var entry = GetOrCreate(type);
        if (Math.Abs(entry.BaseValue - baseValue) <= 0.0001f)
        {
            return;
        }

        entry.BaseValue = baseValue;
        entry.Dirty = true;
    }

    public void AddModifier(in Modifier mod)
    {
        var entry = GetOrCreate(mod.StatType);
        entry.Modifiers.Add(mod);
        entry.Dirty = true;
    }

    public bool RemoveModifier(in Modifier mod)
    {
        if (!_entries.TryGetValue(mod.StatType, out var entry))
        {
            return false;
        }

        for (var i = 0; i < entry.Modifiers.Count; i++)
        {
            var current = entry.Modifiers[i];
            if (current.Source == mod.Source
                && current.Stage == mod.Stage
                && current.StatType == mod.StatType
                && Math.Abs(current.Value - mod.Value) <= 0.0001f)
            {
                entry.Modifiers.RemoveAt(i);
                entry.Dirty = true;
                return true;
            }
        }

        return false;
    }

    public int RemoveAllModifiersFromSource(object source)
    {
        if (source == null)
        {
            return 0;
        }

        var removed = 0;
        foreach (var kv in _entries)
        {
            var entry = kv.Value;
            for (var i = entry.Modifiers.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(entry.Modifiers[i].Source, source))
                {
                    continue;
                }

                entry.Modifiers.RemoveAt(i);
                removed++;
                entry.Dirty = true;
            }
        }

        return removed;
    }

    public bool IsDirty(StatType type)
    {
        return !_entries.TryGetValue(type, out var entry) || entry.Dirty;
    }

    Entry GetOrCreate(StatType type)
    {
        if (_entries.TryGetValue(type, out var entry))
        {
            return entry;
        }

        entry = new Entry();
        _entries.Add(type, entry);
        return entry;
    }
}

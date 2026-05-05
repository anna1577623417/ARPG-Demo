using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ResourcePool : IResourcePool
{
    sealed class ResourceSlot
    {
        public float Current;
        public Func<float> MaxProvider;
    }

    readonly Dictionary<ResourceType, ResourceSlot> _slots = new Dictionary<ResourceType, ResourceSlot>(8);

    public event Action<ResourceType, float, float> OnCurrentChanged;

    public void RegisterSlot(ResourceType type, Func<float> maxProvider, float initialCurrent)
    {
        var slot = GetOrCreate(type);
        slot.MaxProvider = maxProvider;
        slot.Current = Mathf.Clamp(initialCurrent, 0f, GetMaxInternal(slot));
    }

    public float GetCurrent(ResourceType type)
    {
        if (!_slots.TryGetValue(type, out var slot))
        {
            return 0f;
        }

        EnsureWithinMax(type, slot);
        return slot.Current;
    }

    public float GetMax(ResourceType type)
    {
        if (!_slots.TryGetValue(type, out var slot))
        {
            return 0f;
        }

        return GetMaxInternal(slot);
    }

    public float GetNormalized(ResourceType type)
    {
        var max = GetMax(type);
        if (max <= 0.0001f)
        {
            return 0f;
        }

        return Mathf.Clamp01(GetCurrent(type) / max);
    }

    public bool Drain(ResourceType type, float amount, out float actualDrained)
    {
        actualDrained = 0f;
        if (amount <= 0f || !_slots.TryGetValue(type, out var slot))
        {
            return false;
        }

        EnsureWithinMax(type, slot);
        if (slot.Current <= 0f)
        {
            return false;
        }

        var oldValue = slot.Current;
        slot.Current = Mathf.Max(0f, slot.Current - amount);
        actualDrained = oldValue - slot.Current;

        if (actualDrained > 0f)
        {
            OnCurrentChanged?.Invoke(type, oldValue, slot.Current);
            return true;
        }

        return false;
    }

    public bool Refill(ResourceType type, float amount, out float actualRefilled)
    {
        actualRefilled = 0f;
        if (amount <= 0f || !_slots.TryGetValue(type, out var slot))
        {
            return false;
        }

        EnsureWithinMax(type, slot);
        var max = GetMaxInternal(slot);
        if (max <= 0f || slot.Current >= max)
        {
            return false;
        }

        var oldValue = slot.Current;
        slot.Current = Mathf.Min(max, slot.Current + amount);
        actualRefilled = slot.Current - oldValue;

        if (actualRefilled > 0f)
        {
            OnCurrentChanged?.Invoke(type, oldValue, slot.Current);
            return true;
        }

        return false;
    }

    public void SetCurrent(ResourceType type, float value)
    {
        if (!_slots.TryGetValue(type, out var slot))
        {
            return;
        }

        var oldValue = slot.Current;
        slot.Current = Mathf.Clamp(value, 0f, GetMaxInternal(slot));
        if (Mathf.Abs(oldValue - slot.Current) > 0.0001f)
        {
            OnCurrentChanged?.Invoke(type, oldValue, slot.Current);
        }
    }

    public bool IsEmpty(ResourceType type)
    {
        return GetCurrent(type) <= 0f;
    }

    ResourceSlot GetOrCreate(ResourceType type)
    {
        if (_slots.TryGetValue(type, out var slot))
        {
            return slot;
        }

        slot = new ResourceSlot();
        _slots.Add(type, slot);
        return slot;
    }

    void EnsureWithinMax(ResourceType type, ResourceSlot slot)
    {
        var max = GetMaxInternal(slot);
        if (slot.Current <= max)
        {
            return;
        }

        var oldValue = slot.Current;
        slot.Current = max;
        OnCurrentChanged?.Invoke(type, oldValue, slot.Current);
    }

    static float GetMaxInternal(ResourceSlot slot)
    {
        if (slot.MaxProvider == null)
        {
            return 0f;
        }

        return Mathf.Max(0f, slot.MaxProvider());
    }
}

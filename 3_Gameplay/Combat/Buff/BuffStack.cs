using System;
using System.Collections.Generic;

public sealed class BuffStack : IBuffStack
{
    readonly IStatSet _stats;
    readonly List<BuffInstance> _instances = new List<BuffInstance>(8);
    int _nextId = 1;
    public event Action<BuffInstance> OnPeriodElapsed;
    public event Action<BuffInstance> OnExpired;

    public BuffStack(IStatSet stats)
    {
        _stats = stats;
    }

    public BuffInstance Apply(BuffDefinitionSO def, object source)
    {
        if (def == null)
        {
            return default;
        }

        var instance = new BuffInstance
        {
            RuntimeId = _nextId++,
            Definition = def,
            Source = source ?? def,
            RemainingSeconds = Math.Max(0f, def.Duration),
            PeriodTimer = 0f,
        };

        if (def.Effects != null)
        {
            for (var i = 0; i < def.Effects.Length; i++)
            {
                var e = def.Effects[i];
                _stats?.AddModifier(new Modifier(e.StatType, e.Stage, e.Value, instance));
            }
        }

        _instances.Add(instance);
        return instance;
    }

    public bool Remove(BuffInstance instance)
    {
        var idx = FindIndex(instance.RuntimeId);
        if (idx < 0)
        {
            return false;
        }

        var removed = _instances[idx];
        _instances.RemoveAt(idx);
        _stats?.RemoveAllModifiersFromSource(removed);
        return true;
    }

    public void Tick(float deltaTime)
    {
        if (deltaTime <= 0f || _instances.Count == 0)
        {
            return;
        }

        for (var i = _instances.Count - 1; i >= 0; i--)
        {
            var inst = _instances[i];
            if (inst.Definition == null)
            {
                _instances.RemoveAt(i);
                continue;
            }

            if (inst.RemainingSeconds <= 0f)
            {
                Remove(inst);
                continue;
            }

            var period = inst.Definition.PeriodSeconds;
            if (period > 0f)
            {
                inst.PeriodTimer += deltaTime;
                while (inst.PeriodTimer >= period)
                {
                    inst.PeriodTimer -= period;
                    OnPeriodElapsed?.Invoke(inst);
                }
            }

            inst.RemainingSeconds -= deltaTime;
            _instances[i] = inst;

            if (inst.RemainingSeconds <= 0f)
            {
                OnExpired?.Invoke(inst);
                Remove(inst);
            }
        }
    }

    public int Count(BuffDefinitionSO def)
    {
        if (def == null)
        {
            return 0;
        }

        var n = 0;
        for (var i = 0; i < _instances.Count; i++)
        {
            if (_instances[i].Definition == def)
            {
                n++;
            }
        }

        return n;
    }

    public bool Has(BuffDefinitionSO def) => Count(def) > 0;

    int FindIndex(int runtimeId)
    {
        for (var i = 0; i < _instances.Count; i++)
        {
            if (_instances[i].RuntimeId == runtimeId)
            {
                return i;
            }
        }

        return -1;
    }
}

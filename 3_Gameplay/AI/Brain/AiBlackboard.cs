using System;
using System.Collections.Generic;

public readonly struct BlackboardKey<T>
{
    public BlackboardKey(int id, string name)
    {
        Id = id;
        Name = name ?? string.Empty;
    }

    public int Id { get; }
    public string Name { get; }
}

public static class AiBlackboardKeys
{
    public static readonly BlackboardKey<Entity> CurrentTarget =
        new BlackboardKey<Entity>(1, "Perception.CurrentTarget");

    public static readonly BlackboardKey<UnityEngine.Vector3> DesiredMoveDir =
        new BlackboardKey<UnityEngine.Vector3>(2, "Navigation.DesiredMoveDir");

    public static readonly BlackboardKey<string> LastSelectorFailReason =
        new BlackboardKey<string>(3, "Combat.LastSelectorFailReason");

    public static readonly BlackboardKey<Entity[]> VisibleTargets =
        new BlackboardKey<Entity[]>(4, "Perception.VisibleTargets");

    public static readonly BlackboardKey<UnityEngine.Vector3> LastSeenPosition =
        new BlackboardKey<UnityEngine.Vector3>(5, "Perception.LastSeenPosition");

    public static readonly BlackboardKey<float> TargetDistance =
        new BlackboardKey<float>(6, "Perception.TargetDistance");

    public static readonly BlackboardKey<int> VisibleTargetCount =
        new BlackboardKey<int>(7, "Perception.VisibleTargetCount");
}

public interface IBlackboardReader
{
    bool TryGet<T>(BlackboardKey<T> key, out T value);
    bool Contains<T>(BlackboardKey<T> key);
    int Revision { get; }
    int Count { get; }
}

public interface IBlackboardWriter
{
    void Set<T>(BlackboardKey<T> key, T value);
    void Remove<T>(BlackboardKey<T> key);
    void Clear();
}

public sealed class AiBlackboard : IBlackboardReader, IBlackboardWriter
{
    readonly Dictionary<int, object> _values = new Dictionary<int, object>(8);
    readonly Dictionary<int, string> _names = new Dictionary<int, string>(8);

    public int Revision { get; private set; }
    public int Count => _values.Count;

    public bool TryGet<T>(BlackboardKey<T> key, out T value)
    {
        if (_values.TryGetValue(key.Id, out var boxed)
            && boxed is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public bool Contains<T>(BlackboardKey<T> key) => _values.ContainsKey(key.Id);

    public void Set<T>(BlackboardKey<T> key, T value)
    {
        if (value is null)
        {
            Remove(key);
            return;
        }

        _values[key.Id] = value;
        _names[key.Id] = key.Name;
        Revision++;
    }

    public void Remove<T>(BlackboardKey<T> key)
    {
        if (_values.Remove(key.Id))
        {
            _names.Remove(key.Id);
            Revision++;
        }
    }

    public void Clear()
    {
        if (_values.Count == 0)
        {
            return;
        }

        _values.Clear();
        _names.Clear();
        Revision++;
    }

    public string DescribeKeys()
    {
        if (_names.Count == 0)
        {
            return "-";
        }

        var names = new List<string>(_names.Values);
        names.Sort(StringComparer.Ordinal);
        return string.Join(",", names);
    }
}

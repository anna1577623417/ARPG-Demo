using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 世界 UI 通用对象池（血条/名字牌等高频生成对象）。
/// </summary>
public sealed class WorldUIPool<T> where T : MonoBehaviour
{
    readonly Queue<T> _pool = new();
    readonly T _prefab;
    readonly Transform _parent;

    public WorldUIPool(T prefab, Transform parent, int preload = 8)
    {
        _prefab = prefab;
        _parent = parent;

        for (var i = 0; i < preload; i++)
        {
            var inst = Create();
            inst.gameObject.SetActive(false);
            _pool.Enqueue(inst);
        }
    }

    public T Get()
    {
        var inst = _pool.Count > 0 ? _pool.Dequeue() : Create();
        inst.gameObject.SetActive(true);
        return inst;
    }

    public void Release(T instance)
    {
        if (instance == null)
        {
            return;
        }

        instance.gameObject.SetActive(false);
        _pool.Enqueue(instance);
    }

    T Create()
    {
        return Object.Instantiate(_prefab, _parent);
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>默认 CPU 路径：对象池 + 单实例 Update。</summary>
public sealed class CPUDamageTextRenderer : IDamageTextRenderer
{
    readonly DamageTextView _prefab;
    readonly Transform _parent;
    readonly Queue<DamageTextView> _pool = new();
    readonly List<DamageTextView> _active = new();

    public CPUDamageTextRenderer(DamageTextView prefab, Transform parent, int preload)
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

    public int ActiveCount => _active.Count;

    public void Spawn(in DamageTextData data)
    {
        var view = _pool.Count > 0 ? _pool.Dequeue() : Create();
        view.gameObject.SetActive(true);
        view.Bind(data);
        _active.Add(view);
    }

    public void Tick(float deltaTime)
    {
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            var v = _active[i];
            v.Tick(deltaTime);
            if (v.IsAlive)
            {
                continue;
            }

            v.gameObject.SetActive(false);
            _active.RemoveAt(i);
            _pool.Enqueue(v);
        }
    }

    public void Clear()
    {
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            var v = _active[i];
            v.gameObject.SetActive(false);
            _pool.Enqueue(v);
        }

        _active.Clear();
    }

    public void Export(List<DamageTextData> outList)
    {
        if (outList == null)
        {
            return;
        }

        for (var i = 0; i < _active.Count; i++)
        {
            outList.Add(_active[i].Snapshot);
        }
    }

    public void Import(List<DamageTextData> inList)
    {
        if (inList == null)
        {
            return;
        }

        for (var i = 0; i < inList.Count; i++)
        {
            Spawn(inList[i]);
        }
    }

    DamageTextView Create()
    {
        return Object.Instantiate(_prefab, _parent);
    }
}

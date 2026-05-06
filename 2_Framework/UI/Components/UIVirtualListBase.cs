using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 等高分项虚拟列表：请为具体数据类型派生子类并实现 <see cref="GetItemCount"/> / <see cref="BindRow"/>。
/// （Unity 无法将泛型 MonoBehaviour 挂到物体上。）
/// </summary>
public abstract class UIVirtualListBase : MonoBehaviour
{
    [SerializeField] protected ScrollRect scrollRect;
    [SerializeField] protected RectTransform viewport;
    [SerializeField] protected RectTransform content;
    [SerializeField] protected RectTransform rowPrefab;
    [SerializeField] protected float rowHeight = 48f;
    [SerializeField] protected int overscanRows = 2;

    readonly Dictionary<int, RectTransform> _visibleRowsByIndex = new();
    readonly Queue<RectTransform> _pool = new();

    protected abstract int GetItemCount();

    protected abstract void BindRow(RectTransform rowTransform, int index);

    protected virtual void OnEnable()
    {
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.AddListener(OnScrolled);
        }
    }

    protected virtual void OnDisable()
    {
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.RemoveListener(OnScrolled);
        }

        RecycleAllVisible();
    }

    protected virtual void OnDestroy()
    {
        RecycleAllVisible();
        ClearPool();
    }

    protected virtual void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        var count = GetItemCount();
        var totalH = Mathf.Max(rowHeight * count + 8f, rowHeight);
        if (content != null)
        {
            content.sizeDelta = new Vector2(content.sizeDelta.x, totalH);
        }

        FlushAndRebuildVisible();
    }

    void OnScrolled(Vector2 _)
    {
        FlushAndRebuildVisible();
    }

    void FlushAndRebuildVisible()
    {
        var count = GetItemCount();
        if (count == 0 || viewport == null || rowPrefab == null || content == null)
        {
            RecycleAllVisible();
            return;
        }

        var vt = Mathf.Abs(content.anchoredPosition.y);
        var first = Mathf.Clamp(Mathf.FloorToInt(vt / rowHeight) - overscanRows, 0, count - 1);
        var visibleCount = Mathf.CeilToInt(viewport.rect.height / rowHeight) + overscanRows * 2;
        var last = Mathf.Min(count - 1, first + visibleCount);

        // 1) 回收已不可见 row
        if (_visibleRowsByIndex.Count > 0)
        {
            var scope = ListPool<int>.Get(out var recycleKeys);
            foreach (var kv in _visibleRowsByIndex)
            {
                if (kv.Key < first || kv.Key > last)
                {
                    recycleKeys.Add(kv.Key);
                }
            }

            for (var i = 0; i < recycleKeys.Count; i++)
            {
                var idx = recycleKeys[i];
                if (_visibleRowsByIndex.TryGetValue(idx, out var rt))
                {
                    _visibleRowsByIndex.Remove(idx);
                    RecycleRow(rt);
                }
            }

            scope.Dispose();
        }

        // 2) 确保可见范围 row 存在并绑定
        for (var index = first; index <= last; index++)
        {
            if (!_visibleRowsByIndex.TryGetValue(index, out var rt) || rt == null)
            {
                rt = GetRow();
                _visibleRowsByIndex[index] = rt;
            }

            var y = -(index * rowHeight + rowHeight * 0.5f);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(0f, rowHeight);

            BindRow(rt, index);
        }
    }

    RectTransform GetRow()
    {
        if (_pool.Count > 0)
        {
            var pooled = _pool.Dequeue();
            if (pooled != null)
            {
                pooled.gameObject.SetActive(true);
                return pooled;
            }
        }

        return Instantiate(rowPrefab, content);
    }

    void RecycleRow(RectTransform rt)
    {
        if (rt == null)
        {
            return;
        }

        rt.gameObject.SetActive(false);
        _pool.Enqueue(rt);
    }

    void RecycleAllVisible()
    {
        if (_visibleRowsByIndex.Count == 0)
        {
            return;
        }

        foreach (var kv in _visibleRowsByIndex)
        {
            RecycleRow(kv.Value);
        }

        _visibleRowsByIndex.Clear();
    }

    void ClearPool()
    {
        while (_pool.Count > 0)
        {
            var row = _pool.Dequeue();
            if (row != null)
            {
                Destroy(row.gameObject);
            }
        }
    }

    // 小型 List 池，避免频繁分配临时 key 列表
    static class ListPool<T>
    {
        static readonly Stack<List<T>> Cache = new();

        public static PooledList Get(out List<T> list)
        {
            list = Cache.Count > 0 ? Cache.Pop() : new List<T>(32);
            return new PooledList(list);
        }

        public readonly struct PooledList : System.IDisposable
        {
            readonly List<T> _list;

            public PooledList(List<T> list)
            {
                _list = list;
            }

            public void Dispose()
            {
                _list.Clear();
                Cache.Push(_list);
            }
        }
    }
}

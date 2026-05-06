using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 非业务测试用虚拟列表：用于验证池化复用、滚动性能与绑定流程。
/// 用法：挂在含 ScrollRect/content/viewport 的列表节点，配置 rowPrefab（带 DemoVirtualListRow）。
/// </summary>
[DisallowMultipleComponent]
public sealed class DemoVirtualList : UIVirtualListBase
{
    [SerializeField] int sampleCount = 500;
    [SerializeField] string prefix = "Demo Row";

    readonly List<string> _items = new();

    protected override void Start()
    {
        RebuildSamples(sampleCount);
        base.Start();
    }

    [ContextMenu("Rebuild Samples")]
    public void RebuildSamplesContext()
    {
        RebuildSamples(sampleCount);
        Refresh();
    }

    public void RebuildSamples(int count)
    {
        _items.Clear();
        count = Mathf.Max(0, count);
        for (var i = 0; i < count; i++)
        {
            _items.Add($"{prefix} {i:D4}");
        }
    }

    protected override int GetItemCount() => _items.Count;

    protected override void BindRow(RectTransform rowTransform, int index)
    {
        var row = rowTransform != null ? rowTransform.GetComponent<DemoVirtualListRow>() : null;
        if (row != null)
        {
            row.Bind(index, _items[index]);
        }
    }
}

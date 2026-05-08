using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一组资源条的容器：统一显隐文本、按索引取条（便于 Theme 批量染色）。
/// </summary>
[AddComponentMenu("GameMain/UI/Resource Group Controller")]
public sealed class ResourceGroupController : MonoBehaviour
{
    [SerializeField] List<ResourceBarView> bars = new List<ResourceBarView>();

    public ResourceBarView GetBarByIndex(int index) =>
        index >= 0 && index < bars.Count ? bars[index] : null;

    public void SetAllTextVisible(bool visible)
    {
        for (var i = 0; i < bars.Count; i++)
        {
            if (bars[i] != null)
            {
                bars[i].SetTextVisible(visible);
            }
        }
    }

    public int BarCount => bars.Count;
}

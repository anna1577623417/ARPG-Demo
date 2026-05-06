using UnityEngine;

/// <summary>
/// Phase 3：多 Canvas 性能拆分时的场景约定助手 — 为多个独立 Canvas 写入 Sorting Layer / Order，
/// 避免所有 UI 共用一个 Canvas（见蓝图第 4 章）。不改业务逻辑，仅挂载配置。
/// </summary>
[DisallowMultipleComponent]
public sealed class UIMultiCanvasSortSetup : MonoBehaviour
{
    [System.Serializable]
    public struct CanvasSortEntry
    {
        public Canvas canvas;
        [Tooltip("数值越大越靠前（渲染在上层）。")]
        public int sortingOrder;
        public bool overrideSorting;
    }

    [SerializeField] CanvasSortEntry[] canvases =
    {
        new CanvasSortEntry { sortingOrder = 0, overrideSorting = true },
        new CanvasSortEntry { sortingOrder = 50, overrideSorting = true },
        new CanvasSortEntry { sortingOrder = 500, overrideSorting = true }
    };

    [SerializeField] string sortingLayerName = "Default";

    void Awake()
    {
        Apply();
    }

    [ContextMenu("Apply Sorting")]
    public void Apply()
    {
        if (canvases == null)
        {
            return;
        }

        var layerId = SortingLayer.NameToID(sortingLayerName);

        for (var i = 0; i < canvases.Length; i++)
        {
            var e = canvases[i];
            var c = e.canvas;
            if (c == null)
            {
                continue;
            }

            c.overrideSorting = e.overrideSorting;
            if (e.overrideSorting)
            {
                c.sortingLayerID = layerId;
                c.sortingOrder = e.sortingOrder;
            }
        }
    }
}

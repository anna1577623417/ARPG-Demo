using UnityEngine;

/// <summary>
/// Phase 3：将 <see cref="Screen.safeArea"/> 映射到当前 RectTransform 的 anchor（全屏根节点下使用）。
/// 挂在 Canvas_Root 下「整屏伸缩」的那一屋 RectTransform（通常 Stretch）。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class UISafeAreaFitter : MonoBehaviour
{
    [SerializeField] bool constrainToSafeArea = true;

    RectTransform Rect => (RectTransform)transform;

    void OnEnable()
    {
        Apply();
    }

    void OnRectTransformDimensionsChange()
    {
        Apply();
    }

#if UNITY_EDITOR
    void Update()
    {
        if (!Application.isPlaying)
        {
            Apply();
        }
    }
#endif

    public void Apply()
    {
        if (!constrainToSafeArea)
        {
            return;
        }

        var safe = Screen.safeArea;
        var parent = Rect.parent as RectTransform;
        if (parent == null)
        {
            return;
        }

        var pw = parent.rect.width;
        var ph = parent.rect.height;
        if (pw < 1e-3f || ph < 1e-3f)
        {
            return;
        }

        var xmin = Mathf.Clamp01(safe.xMin / Screen.width);
        var xmax = Mathf.Clamp01(safe.xMax / Screen.width);
        var ymin = Mathf.Clamp01(safe.yMin / Screen.height);
        var ymax = Mathf.Clamp01(safe.yMax / Screen.height);

        Rect.anchorMin = new Vector2(xmin, ymin);
        Rect.anchorMax = new Vector2(xmax, ymax);
        Rect.offsetMin = Vector2.zero;
        Rect.offsetMax = Vector2.zero;
    }
}

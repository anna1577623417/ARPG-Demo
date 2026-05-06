using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全屏输入拦截层（通常挂在一张透明 Image 上）。
/// 由 UIRoot 在 Screen/Modal 变化后驱动显隐。
/// </summary>
[DisallowMultipleComponent]
public sealed class UIInputBlocker : MonoBehaviour
{
    [SerializeField] Graphic raycastGraphic;
    [SerializeField] bool logStructureOnEnable = true;
    [SerializeField] bool warnIfNotFullscreen = true;
    [SerializeField] bool warnIfNoGraphic = true;

    void Awake()
    {
        if (raycastGraphic == null)
        {
            raycastGraphic = GetComponent<Graphic>();
        }
    }

    void OnEnable()
    {
        if (logStructureOnEnable)
        {
            LogExpectedStructure();
        }
    }

    public void SetBlocking(bool blocking)
    {
        if (raycastGraphic != null)
        {
            raycastGraphic.raycastTarget = blocking;
        }

        gameObject.SetActive(blocking);
    }

    [ContextMenu("Log Blocker Structure")]
    public void LogExpectedStructure()
    {
        if (warnIfNoGraphic && raycastGraphic == null)
        {
            Debug.LogWarning("[UIInputBlocker] Missing Graphic. Add transparent Image and bind raycastGraphic.", this);
        }

        var rt = transform as RectTransform;
        if (warnIfNotFullscreen && rt != null)
        {
            var fullStretch = rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one;
            if (!fullStretch)
            {
                Debug.LogWarning("[UIInputBlocker] RectTransform should be full stretch (anchorMin=0,0 anchorMax=1,1).", this);
            }
        }

        var canvas = GetComponentInParent<Canvas>();
        var mode = canvas != null ? canvas.renderMode.ToString() : "(no canvas)";
        Debug.Log(
            $"[UIInputBlocker] Expected: full-screen transparent Image, raycastTarget ON while blocking. Canvas={mode}, graphic={(raycastGraphic != null ? raycastGraphic.GetType().Name : "null")}.",
            this);
    }
}

using UnityEngine;

/// <summary>
/// 主题框架专项自检：用于最终收尾时验证主题链路完整性。
/// </summary>
[DisallowMultipleComponent]
public sealed class UIThemeHealthCheck : MonoBehaviour
{
    [SerializeField] UIRoot uiRoot;
    [SerializeField] UIThemeSO fallbackTheme;
    [SerializeField] bool runOnStart = true;
    [SerializeField] bool applyFallbackWhenMissingTheme = true;
    [SerializeField] bool verbose = true;

    void Start()
    {
        if (!runOnStart)
        {
            return;
        }

        RunCheck();
    }

    [ContextMenu("Run Theme Health Check")]
    public void RunCheck()
    {
        if (uiRoot == null && !UIRoot.TryGetPersistentInstance(out uiRoot))
        {
            Debug.LogError("[UIThemeHealthCheck] UIRoot missing.", this);
            return;
        }

        var current = uiRoot.CurrentTheme;
        if (current == null)
        {
            Debug.LogWarning("[UIThemeHealthCheck] CurrentTheme is null.", this);
            if (applyFallbackWhenMissingTheme && fallbackTheme != null)
            {
                uiRoot.SetTheme(fallbackTheme, forceReapply: true);
                current = uiRoot.CurrentTheme;
                Debug.Log($"[UIThemeHealthCheck] Applied fallback theme: {current.themeId}", this);
            }
        }

        var binders = FindObjectsByType<UIThemeBinder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var activeBinders = 0;
        var disabledBinders = 0;
        for (var i = 0; i < binders.Length; i++)
        {
            if (binders[i] == null)
            {
                continue;
            }

            if (binders[i].isActiveAndEnabled)
            {
                activeBinders++;
            }
            else
            {
                disabledBinders++;
            }
        }

        var msg = $"[UIThemeHealthCheck] theme={(current != null ? current.themeId : "(none)")}, binders active={activeBinders}, disabled={disabledBinders}";
        if (verbose)
        {
            Debug.Log(msg, this);
        }
    }
}

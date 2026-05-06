using TMPro;
using UnityEngine;

/// <summary>
/// 运行时主题预览：可按键循环主题并应用到全局 UI。
/// </summary>
[DisallowMultipleComponent]
public sealed class UIThemePreview : MonoBehaviour
{
    [SerializeField] UIRoot uiRoot;
    [SerializeField] UIThemeSO[] themes;
    [SerializeField] KeyCode nextThemeKey = KeyCode.F8;
    [SerializeField] KeyCode prevThemeKey = KeyCode.F7;
    [SerializeField] TMP_Text statusText;

    int _index;

    void Start()
    {
        if (uiRoot == null)
        {
            UIRoot.TryGetPersistentInstance(out uiRoot);
        }

        if (uiRoot == null || themes == null || themes.Length == 0)
        {
            return;
        }

        // 与当前主题对齐
        var current = uiRoot.CurrentTheme;
        if (current != null)
        {
            for (var i = 0; i < themes.Length; i++)
            {
                if (themes[i] == current)
                {
                    _index = i;
                    break;
                }
            }
        }

        UpdateStatus();
    }

    void Update()
    {
        if (uiRoot == null || themes == null || themes.Length == 0)
        {
            return;
        }

        if (Input.GetKeyDown(nextThemeKey))
        {
            _index = (_index + 1) % themes.Length;
            ApplyCurrentTheme();
        }
        else if (Input.GetKeyDown(prevThemeKey))
        {
            _index = (_index - 1 + themes.Length) % themes.Length;
            ApplyCurrentTheme();
        }
    }

    [ContextMenu("Apply Current Theme")]
    public void ApplyCurrentTheme()
    {
        if (uiRoot == null || themes == null || themes.Length == 0)
        {
            return;
        }

        var theme = themes[Mathf.Clamp(_index, 0, themes.Length - 1)];
        uiRoot.SetTheme(theme, forceReapply: true);
        UpdateStatus();
    }

    void UpdateStatus()
    {
        if (statusText == null || themes == null || themes.Length == 0)
        {
            return;
        }

        var t = themes[Mathf.Clamp(_index, 0, themes.Length - 1)];
        statusText.text = $"ThemePreview: {(t != null ? t.themeId : "(null)")}  [{_index + 1}/{themes.Length}]";
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum UIThemeRole
{
    Primary = 0,
    Secondary = 1,
    Accent = 2,
    Warning = 3,
    Danger = 4,
    Background = 5
}

/// <summary>
/// 通用主题绑定器：自动向 UIRoot.Theme 注册/反注册，支持 Image/TMP_Text。
/// </summary>
[DisallowMultipleComponent]
public sealed class UIThemeBinder : MonoBehaviour, IUIThemeable
{
    [SerializeField] UIRoot uiRoot;
    [SerializeField] Graphic targetGraphic;
    [SerializeField] TMP_Text targetText;
    [SerializeField] UIThemeRole role = UIThemeRole.Primary;
    [SerializeField] bool affectFontSize;
    [SerializeField] float fontScale = 1f;

    void Awake()
    {
        if (targetGraphic == null)
        {
            targetGraphic = GetComponent<Graphic>();
        }

        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
        }
    }

    void OnEnable()
    {
        if (uiRoot == null)
        {
            UIRoot.TryGetPersistentInstance(out uiRoot);
        }

        uiRoot?.RegisterThemeTarget(this, applyNow: true);
    }

    void OnDisable()
    {
        uiRoot?.UnregisterThemeTarget(this);
    }

    public void ApplyTheme(UIThemeSO theme)
    {
        if (theme == null)
        {
            return;
        }

        var color = ResolveColor(theme, role);

        if (targetGraphic != null)
        {
            targetGraphic.color = color;
        }

        if (targetText != null)
        {
            targetText.color = color;
            if (affectFontSize)
            {
                targetText.fontSize = Mathf.Max(8f, theme.baseFontSize * Mathf.Max(0.25f, fontScale));
            }
        }
    }

    static Color ResolveColor(UIThemeSO theme, UIThemeRole role)
    {
        switch (role)
        {
            case UIThemeRole.Secondary:
                return theme.secondary;
            case UIThemeRole.Accent:
                return theme.accent;
            case UIThemeRole.Warning:
                return theme.warning;
            case UIThemeRole.Danger:
                return theme.danger;
            case UIThemeRole.Background:
                return theme.background;
            default:
                return theme.primary;
        }
    }
}

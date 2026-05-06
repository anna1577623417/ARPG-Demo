using TMPro;
using UnityEngine;
using UnityEngine.UI;

public struct UIHealthBarData
{
    public float Current;
    public float Max;
}

/// <summary>可复用血条组件（HUD / WorldUI 通用）。</summary>
public sealed class UIHealthBar : UIComponent<UIHealthBarData>, IUIThemeable
{
    [SerializeField] Image fillImage;
    [SerializeField] TMP_Text valueText;
    [Header("Theme (optional)")]
    [SerializeField] bool useTheme;
    [SerializeField] UIRoot uiRoot;
    [SerializeField] UIThemeRole fillRole = UIThemeRole.Accent;
    [SerializeField] UIThemeRole textRole = UIThemeRole.Primary;

    UIThemeSO _theme;

    void OnEnable()
    {
        if (!useTheme)
        {
            return;
        }

        if (uiRoot == null)
        {
            UIRoot.TryGetPersistentInstance(out uiRoot);
        }

        uiRoot?.RegisterThemeTarget(this, applyNow: true);
    }

    void OnDisable()
    {
        if (!useTheme)
        {
            return;
        }

        uiRoot?.UnregisterThemeTarget(this);
    }

    public override void Bind(UIHealthBarData data)
    {
        var max = Mathf.Max(0.001f, data.Max);
        var ratio = Mathf.Clamp01(data.Current / max);
        if (fillImage != null)
        {
            fillImage.fillAmount = ratio;
        }

        if (valueText != null)
        {
            valueText.text = $"{Mathf.RoundToInt(data.Current)}/{Mathf.RoundToInt(max)}";
        }

        ApplyTheme(_theme);
    }

    public void ApplyTheme(UIThemeSO theme)
    {
        _theme = theme;
        if (!useTheme || theme == null)
        {
            return;
        }

        if (fillImage != null)
        {
            fillImage.color = ResolveColor(theme, fillRole);
        }

        if (valueText != null)
        {
            valueText.color = ResolveColor(theme, textRole);
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

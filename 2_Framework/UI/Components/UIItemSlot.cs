using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public struct UIItemSlotData
{
    public int ItemId;
    public Sprite Icon;
    public int Count;
    public bool IsSelected;
    public Action<int> OnClicked;
}

/// <summary>可复用物品格组件（背包/商店通用）。</summary>
public sealed class UIItemSlot : UIComponent<UIItemSlotData>, IUIThemeable
{
    [SerializeField] Image iconImage;
    [SerializeField] TMP_Text countText;
    [SerializeField] GameObject selectedHighlight;
    [SerializeField] Button clickButton;
    [Header("Theme (optional)")]
    [SerializeField] bool useTheme;
    [SerializeField] UIRoot uiRoot;
    [SerializeField] UIThemeRole textRole = UIThemeRole.Primary;
    [SerializeField] UIThemeRole selectedRole = UIThemeRole.Accent;
    [SerializeField] UIThemeRole unselectedRole = UIThemeRole.Secondary;

    UIItemSlotData _data;
    UIThemeSO _theme;

    void Awake()
    {
        if (clickButton != null)
        {
            clickButton.onClick.AddListener(HandleClick);
        }
    }

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

    public override void Bind(UIItemSlotData data)
    {
        _data = data;
        if (iconImage != null)
        {
            iconImage.sprite = data.Icon;
            iconImage.enabled = data.Icon != null;
        }

        if (countText != null)
        {
            countText.text = data.Count > 1 ? data.Count.ToString() : string.Empty;
        }

        if (selectedHighlight != null)
        {
            selectedHighlight.SetActive(data.IsSelected);
        }

        ApplyTheme(_theme);
    }

    public override void Unbind()
    {
        _data = default;
    }

    public void ApplyTheme(UIThemeSO theme)
    {
        _theme = theme;
        if (!useTheme || theme == null)
        {
            return;
        }

        if (countText != null)
        {
            countText.color = ResolveColor(theme, textRole);
        }

        if (selectedHighlight != null)
        {
            var image = selectedHighlight.GetComponent<Image>();
            if (image != null)
            {
                image.color = _data.IsSelected
                    ? ResolveColor(theme, selectedRole)
                    : ResolveColor(theme, unselectedRole);
            }
        }
    }

    void HandleClick()
    {
        _data.OnClicked?.Invoke(_data.ItemId);
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

using TMPro;
using UnityEngine;

/// <summary>CPU 路径单个飘字视图。</summary>
[DisallowMultipleComponent]
public sealed class DamageTextView : MonoBehaviour, IUIThemeable
{
    [SerializeField] TMP_Text textLabel;
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] float lifeSeconds = 0.85f;
    [SerializeField] float riseSpeed = 40f;
    [Header("Theme (optional)")]
    [SerializeField] bool useThemeForColors;
    [SerializeField] UIRoot uiRoot;
    [SerializeField] UIThemeRole normalRole = UIThemeRole.Primary;
    [SerializeField] UIThemeRole critRole = UIThemeRole.Warning;

    float _elapsed;
    DamageTextData _data;
    RectTransform _rect;
    UIThemeSO _cachedTheme;

    public bool IsAlive => _elapsed < lifeSeconds;
    public DamageTextData Snapshot => _data;

    void Awake()
    {
        _rect = transform as RectTransform;
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    void OnEnable()
    {
        if (!useThemeForColors)
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
        if (!useThemeForColors)
        {
            return;
        }

        uiRoot?.UnregisterThemeTarget(this);
    }

    public void Bind(in DamageTextData data)
    {
        _data = data;
        _elapsed = 0f;
        if (textLabel != null)
        {
            textLabel.text = data.Value.ToString();
            textLabel.color = ResolveTextColor(data);
            textLabel.fontStyle = data.IsCrit ? FontStyles.Bold : FontStyles.Normal;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        transform.position = data.WorldPos;
    }

    public void ApplyTheme(UIThemeSO theme)
    {
        _cachedTheme = theme;
        if (!useThemeForColors || textLabel == null)
        {
            return;
        }

        textLabel.color = ResolveTextColor(_data);
    }

    public void Tick(float dt)
    {
        _elapsed += dt;
        if (_rect != null)
        {
            _rect.position += new Vector3(0f, riseSpeed * dt, 0f);
        }

        if (canvasGroup != null)
        {
            var t = Mathf.Clamp01(_elapsed / lifeSeconds);
            canvasGroup.alpha = 1f - t;
        }
    }

    Color ResolveTextColor(in DamageTextData data)
    {
        if (!useThemeForColors || _cachedTheme == null)
        {
            return data.Color;
        }

        var role = data.IsCrit ? critRole : normalRole;
        return ResolveThemeColor(_cachedTheme, role);
    }

    static Color ResolveThemeColor(UIThemeSO theme, UIThemeRole role)
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

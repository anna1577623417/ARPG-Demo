using UnityEngine;

/// <summary>
/// UI 主题数据：由框架层消费，业务只选择主题而不直接改控件色值。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/UI/UI Theme", fileName = "UITheme")]
public class UIThemeSO : ScriptableObject
{
    [Header("Identity")]
    public string themeId = "default";

    [Header("Palette")]
    public Color primary = Color.white;
    public Color secondary = new Color(0.85f, 0.85f, 0.85f, 1f);
    public Color accent = new Color(0.2f, 0.8f, 1f, 1f);
    public Color warning = new Color(1f, 0.65f, 0.1f, 1f);
    public Color danger = new Color(1f, 0.35f, 0.35f, 1f);
    public Color background = new Color(0.06f, 0.06f, 0.08f, 1f);

    [Header("Typography")]
    [Min(8f)] public float baseFontSize = 24f;
}

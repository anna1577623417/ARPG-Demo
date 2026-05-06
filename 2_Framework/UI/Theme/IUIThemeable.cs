/// <summary>
/// 主题可应用接口：组件实现后可由 UIThemeService 统一驱动。
/// </summary>
public interface IUIThemeable
{
    void ApplyTheme(UIThemeSO theme);
}

/// <summary>CombatGraph 注册表只读查询（Editor / 调试工具用）。</summary>
public interface IRouteRegistryQuery
{
    bool ContainsRoute(SkillRouteDefinition route);
}

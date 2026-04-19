/// <summary>
/// 解析由 <see cref="ServiceRegistry"/> 或组合根注册的运行时服务。
/// 核心链路依赖应由此显式解析或构造注入，而非 <c>FindObjectOfType</c> / <c>GetComponent</c>。
/// </summary>
public interface IServiceResolver
{
    /// <summary>尝试解析已注册的服务；未注册时返回 false。</summary>
    bool TryResolve<T>(out T service) where T : class;
}

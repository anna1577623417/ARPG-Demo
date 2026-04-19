using System;
using System.Collections.Generic;

/// <summary>
/// 轻量服务定位器：场景级组合根（<see cref="SystemRoot"/>）在启动时注册单例式系统，
/// 其余代码通过接口解析，避免散落的全局查找。
/// </summary>
public sealed class ServiceRegistry : IServiceResolver
{
    private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

    /// <summary>按实现类型注册实例。</summary>
    public void Register<T>(T instance) where T : class
    {
        if (instance == null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        _services[typeof(T)] = instance;
    }

    /// <summary>将实例以接口类型 <typeparamref name="TInterface"/> 暴露（显式契约注册）。</summary>
    public void RegisterAs<TInterface>(TInterface instance) where TInterface : class
    {
        if (instance == null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        _services[typeof(TInterface)] = instance;
    }

    /// <inheritdoc />
    public bool TryResolve<T>(out T service) where T : class
    {
        if (_services.TryGetValue(typeof(T), out var obj) && obj is T typed)
        {
            service = typed;
            return true;
        }

        service = null;
        return false;
    }
}

using System;
using System.Collections.Generic;

/// <summary>
/// 场景级服务登记册：仅供 UI、调试或非核心系统在运行时<strong>可选</strong>解析全局服务。
/// <strong>禁止</strong>在组合根内部用 <see cref="TryResolve{T}"/> 拼自己刚注册的类型；核心依赖应直接传参或构造注入。
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

using System;
using System.Collections.Generic;

/// <summary>
/// Lightweight scene-scope service locator built at the composition root (typically a scene <c>SystemRoot</c> object).
/// Prefer resolving interfaces (e.g. <see cref="IPlayerManager"/>) instead of singletons inside entities.
/// </summary>
public sealed class ServiceRegistry : IServiceResolver
{
    private readonly Dictionary<Type, object> _singletons = new Dictionary<Type, object>(16);

    public void RegisterSingleton<TInterface>(TInterface implementation) where TInterface : class
    {
        if (implementation == null)
        {
            throw new ArgumentNullException(nameof(implementation));
        }

        _singletons[typeof(TInterface)] = implementation;
    }

    public TInterface Resolve<TInterface>() where TInterface : class
    {
        if (!TryResolve(out TInterface service))
        {
            throw new InvalidOperationException($"No service registered for {typeof(TInterface).Name}.");
        }

        return service;
    }

    public bool TryResolve<TInterface>(out TInterface service) where TInterface : class
    {
        if (_singletons.TryGetValue(typeof(TInterface), out var obj) && obj is TInterface typed)
        {
            service = typed;
            return true;
        }

        service = null;
        return false;
    }
}

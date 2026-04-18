/// <summary>
/// Minimal resolver used after composition-root registration (constructor / <c>Construct</c> injection).
/// </summary>
public interface IServiceResolver
{
    TInterface Resolve<TInterface>() where TInterface : class;

    bool TryResolve<TInterface>(out TInterface service) where TInterface : class;
}

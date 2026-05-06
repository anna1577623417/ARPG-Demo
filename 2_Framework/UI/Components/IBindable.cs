/// <summary>
/// 响应式绑定接口（可选进阶）：将组件与外部数据源生命周期解耦。
/// </summary>
public interface IBindable<T>
{
    void Bind(IObservableValue<T> source);
    void Unbind();
}

/// <summary>最小可用可观察值接口；业务层可自行适配 Rx/自研事件流。</summary>
public interface IObservableValue<T>
{
    T Value { get; }
    event System.Action<T> ValueChanged;
}

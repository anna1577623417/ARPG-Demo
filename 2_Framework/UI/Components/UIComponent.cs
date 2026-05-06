using UnityEngine;

/// <summary>
/// 可复用 UI 子控件基类：由父级 <see cref="UIScreenBase{TProps}"/> 在 <see cref="UIScreenBase{TProps}.OnPropsSet"/> 中调用 <see cref="Bind"/>。
/// </summary>
public abstract class UIComponent<TData> : MonoBehaviour
{
    public abstract void Bind(TData data);

    /// <summary>池化或切页时复位引用，避免闭包持有陈旧 Command。</summary>
    public virtual void Unbind() { }
}

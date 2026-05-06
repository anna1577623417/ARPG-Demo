/// <summary>弹窗：ModalStack 管理，不写入 Screen 历史。</summary>
public abstract class UIModal<TProps> : UIScreenBase<TProps>
{
    protected virtual void Reset()
    {
        policyConfig = ScreenPolicy.DefaultModal;
    }
}

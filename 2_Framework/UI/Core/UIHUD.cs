/// <summary>常驻 HUD：由 UIHUDRegistry 注册，不进 Screen/Modal 栈。</summary>
public abstract class UIHUD<TProps> : UIScreenBase<TProps>
{
    protected virtual void Reset()
    {
        policyConfig = ScreenPolicy.DefaultHud;
    }
}

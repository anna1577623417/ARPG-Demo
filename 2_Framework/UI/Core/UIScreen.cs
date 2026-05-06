/// <summary>全屏界面：独占 ScreenStack，默认 Hidden 覆盖下层、阻挡输入。</summary>
public abstract class UIScreen<TProps> : UIScreenBase<TProps>
{
    protected virtual void Reset()
    {
        policyConfig = ScreenPolicy.DefaultExclusiveScreen;
    }
}

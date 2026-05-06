using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 栈可管理的界面统一基类（非泛型入口）。生命周期仅由 UIScreenStack / UIModalStack / UIHUDRegistry / UIRoot 驱动。
/// 泛型子类：<see cref="UIScreenBase{TProps}"/>。
/// </summary>
public abstract class UIScreenBase : MonoBehaviour
{
    /// <summary>若为 true（默认），则在 Awake 时 SetActive(false) 保持未展示态。</summary>
    [SerializeField] bool deactivateOnAwake = true;

    [SerializeField] protected ScreenPolicy policyConfig = ScreenPolicy.DefaultExclusiveScreen;

    [Header("Presentation (optional)")]
    [SerializeField] UITransitionPresetSO enterTransitionPreset;

    [Tooltip("省略则用本物体 RectTransform")]
    [SerializeField] RectTransform enterTransitionRoot;

    [Tooltip("Fade 时使用；省略则尝试自找/运行时临时挂载")]
    [SerializeField] CanvasGroup enterTransitionCanvasGroup;

    [SerializeField] UITransitionPresetSO exitTransitionPreset;
    [SerializeField] RectTransform exitTransitionRoot;
    [SerializeField] CanvasGroup exitTransitionCanvasGroup;

    Coroutine _presentationRoutine;

    public UIScreenState State { get; protected set; } = UIScreenState.Hidden;

    public ScreenPolicy Policy { get; protected set; } = ScreenPolicy.DefaultExclusiveScreen;

    public virtual string ScreenId => name;

    internal abstract void InternalShowBoxed(object boxedProps);

    internal abstract void InternalUpdateBoxed(object boxedProps);

    /// <summary>供栈在 HideOnCovered 回收后恢复展示使用。</summary>
    internal abstract object ExportLastPropsSnapshot();

    internal void InternalPauseFromStack()
    {
        if (State != UIScreenState.Visible)
        {
            return;
        }

        State = UIScreenState.Paused;
        OnPause();
    }

    internal void InternalResumeFromStack()
    {
        if (State != UIScreenState.Paused)
        {
            return;
        }

        State = UIScreenState.Visible;
        OnResume();
    }

    internal void InternalHideFromStack()
    {
        if (State == UIScreenState.Hidden || State == UIScreenState.Destroyed)
        {
            return;
        }

        State = UIScreenState.Exiting;
        OnBeforeHide();
        StopPresentationRoutine();
        _presentationRoutine = StartCoroutine(HideRoutine());
    }

    protected virtual void Awake()
    {
        Policy = policyConfig;
        if (deactivateOnAwake)
        {
            gameObject.SetActive(false);
        }
    }

    protected virtual void OnDestroy()
    {
        StopPresentationRoutine();
        State = UIScreenState.Destroyed;
        OnDestroyed();
    }

    // --- 钩子（业务子类重写） -------------------------------------------------

    protected virtual void OnBeforeShow() { }

    protected virtual void OnEnter() { }

    protected virtual void OnPropsSet() { }

    protected virtual void OnPause() { }

    protected virtual void OnResume() { }

    protected virtual void OnBeforeHide() { }

    protected virtual void OnExit() { }

    protected virtual void OnDestroyed() { }

    // --- Phase 15：接入 UIAnimationPlayer 于 Entering ↔ Visible ---------------

    protected void FinishEnterImmediate()
    {
        if (State == UIScreenState.Entering)
        {
            State = UIScreenState.Visible;
            OnEnter();
        }
    }

    /// <summary>由泛型 InternalShowBoxed 在 OnPropsSet 之后调用。</summary>
    protected void PlayEnterPresentationOrFinish(Action onBecomeVisible)
    {
        StopPresentationRoutine();
        var root = enterTransitionRoot != null ? enterTransitionRoot : transform as RectTransform;
        _presentationRoutine = StartCoroutine(UIAnimationPlayer.PlayEnter(
            enterTransitionPreset,
            root,
            enterTransitionCanvasGroup,
            onBecomeVisible));
    }

    IEnumerator HideRoutine()
    {
        var root = exitTransitionRoot != null ? exitTransitionRoot : transform as RectTransform;
        yield return UIAnimationPlayer.PlayExit(
            exitTransitionPreset,
            root,
            exitTransitionCanvasGroup,
            null);

        gameObject.SetActive(false);
        State = UIScreenState.Hidden;
        OnExit();
        _presentationRoutine = null;
    }

    void StopPresentationRoutine()
    {
        if (_presentationRoutine == null)
        {
            return;
        }

        StopCoroutine(_presentationRoutine);
        _presentationRoutine = null;
    }
}

/// <summary>TProps：只读快照 + Command 回调；禁止在其中持有 Entity/Player。</summary>
public abstract class UIScreenBase<TProps> : UIScreenBase
{
    protected TProps Props { get; private set; }

    internal sealed override void InternalShowBoxed(object boxedProps)
    {
        Props = ConvertProps(boxedProps);
        // 仅 Hidden → 首轮展示或由栈在 HideOnCovered 后恢复唤起。
        if (State != UIScreenState.Hidden)
        {
            return;
        }

        State = UIScreenState.Entering;
        gameObject.SetActive(true);
        OnBeforeShow();
        OnPropsSet();
        PlayEnterPresentationOrFinish(FinishEnterImmediate);
    }

    internal sealed override void InternalUpdateBoxed(object boxedProps)
    {
        Props = ConvertProps(boxedProps);
        OnPropsSet();
    }

    internal sealed override object ExportLastPropsSnapshot() => Props;

    /// <summary>栈外更新数据（例如 HUD 刷新）。若界面未激活可在首次 Show 前忽略。</summary>
    public void UpdateProps(TProps props)
    {
        Props = props;
        if (State == UIScreenState.Visible || State == UIScreenState.Paused)
        {
            OnPropsSet();
        }
    }

    static TProps ConvertProps(object boxed)
    {
        if (boxed == null)
        {
            return default;
        }

        return (TProps)boxed;
    }
}

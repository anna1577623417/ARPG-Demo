using System.Collections;
using UnityEngine;

/// <summary>
/// Phase 3：把 <see cref="UIInputManager"/> 接到 <see cref="InputReader"/>。
/// - <see cref="UIInputMode.UIOnly"/> → <see cref="InputFocusMode.UI"/>
/// - <see cref="UIInputMode.GameOnly"/> → <see cref="InputFocusMode.Gameplay"/>
/// - <see cref="UIInputMode.Mixed"/> → <see cref="InputFocusMode.Mixed"/> / <see cref="InputReader.EnableGameplayAndUiMaps"/>
/// </summary>
[DisallowMultipleComponent]
public sealed class UIInputReaderBridge : MonoBehaviour
{
    [SerializeField] UIRoot uiRoot;

    /// <summary>与 Player / UI 共用同一 ScriptableObject 资产实例。</summary>
    [SerializeField] InputReader inputReader;

    void Start()
    {
        if (uiRoot == null)
        {
            UIRoot.TryGetPersistentInstance(out uiRoot);
        }

        if (inputReader == null)
        {
            Debug.LogWarning("[UIInputReaderBridge] Assign InputReader asset.", this);
            return;
        }

        if (uiRoot == null)
        {
            Debug.LogWarning("[UIInputReaderBridge] UIRoot not found; deferred bind next frame.", this);
            StartCoroutine(DeferSubscribe());
            return;
        }

        Subscribe();
        Apply(uiRoot.Input.CurrentMode);
    }

    IEnumerator DeferSubscribe()
    {
        yield return null;
        if (uiRoot == null && !UIRoot.TryGetPersistentInstance(out uiRoot))
        {
            Debug.LogWarning("[UIInputReaderBridge] UIRoot still missing; disabling bridge.", this);
            enabled = false;
            yield break;
        }

        Subscribe();
        Apply(uiRoot.Input.CurrentMode);
    }

    void Subscribe()
    {
        if (uiRoot == null || inputReader == null)
        {
            return;
        }

        uiRoot.Input.ModeChanged -= OnUiModeChanged;
        uiRoot.Input.ModeChanged += OnUiModeChanged;
    }

    void OnDestroy()
    {
        Unsubscribe();
    }

    void Unsubscribe()
    {
        if (uiRoot != null)
        {
            uiRoot.Input.ModeChanged -= OnUiModeChanged;
        }
    }

    void OnUiModeChanged(UIInputMode mode)
    {
        Apply(mode);
    }

    void Apply(UIInputMode mode)
    {
        if (inputReader == null)
        {
            return;
        }

        switch (mode)
        {
            case UIInputMode.UIOnly:
                inputReader.SetFocus(InputFocusMode.UI);
                break;
            case UIInputMode.Mixed:
                inputReader.EnableGameplayAndUiMaps();
                break;
            default:
                inputReader.SetFocus(InputFocusMode.Gameplay);
                break;
        }
    }
}

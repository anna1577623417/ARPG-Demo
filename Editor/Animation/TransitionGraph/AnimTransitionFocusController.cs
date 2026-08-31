#if UNITY_EDITOR
using UnityEngine.UIElements;

/// <summary>Session focus only. Text editing and in-progress gestures never write graph history.</summary>
public sealed class AnimTransitionFocusController
{
    public bool IsTextFieldFocused { get; private set; }
    public bool IsCanvasGestureActive { get; set; }

    public void Refresh(IEventHandler currentFocus)
    {
        IsTextFieldFocused = currentFocus is TextField;
    }

    public bool ShouldRouteGraphHistory => !IsTextFieldFocused && !IsCanvasGestureActive;
}
#endif

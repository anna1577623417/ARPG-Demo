#if UNITY_EDITOR
using UnityEditor;

/// <summary>Editor-only UI debug switch. It is intentionally separate from runtime trace settings.</summary>
public static class AnimTransitionEditorDebug244
{
    const string PrefKey = "CoreDrive.AnimTransition.EditorDebug244";

    public static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, false);
        set => EditorPrefs.SetBool(PrefKey, value);
    }
}
#endif

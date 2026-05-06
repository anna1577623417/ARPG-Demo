using UnityEngine;

/// <summary>
/// IMGUI 栈摘要；勾选组件 enabled 且在 Play 模式下显示。
/// </summary>
[DisallowMultipleComponent]
public sealed class UIDebugOverlay : MonoBehaviour
{
    [SerializeField] UIRoot uiRoot;

    [SerializeField] Vector2 guiPosition = new Vector2(8f, 8f);

    [SerializeField] int fontSize = 12;

    void OnGUI()
    {
        if (!isActiveAndEnabled || !Application.isPlaying)
        {
            return;
        }

        if (uiRoot == null)
        {
            UIRoot.TryGetPersistentInstance(out uiRoot);
        }

        if (uiRoot == null)
        {
            return;
        }

        var prev = GUI.skin.label.fontSize;
        GUI.skin.label.fontSize = fontSize;
        GUI.Label(new Rect(guiPosition.x, guiPosition.y, 960f, 200f), uiRoot.BuildDebugSnapshot());
        GUI.skin.label.fontSize = prev;
    }
}

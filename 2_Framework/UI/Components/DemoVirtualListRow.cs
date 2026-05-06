using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>用于 UIVirtualListBase 的非业务测试行组件。</summary>
[DisallowMultipleComponent]
public sealed class DemoVirtualListRow : MonoBehaviour
{
    [SerializeField] TMP_Text label;
    [SerializeField] Image background;
    [SerializeField] Color evenColor = new Color(1f, 1f, 1f, 0.06f);
    [SerializeField] Color oddColor = new Color(1f, 1f, 1f, 0.12f);

    public void Bind(int index, string text)
    {
        if (label != null)
        {
            label.text = text;
        }

        if (background != null)
        {
            background.color = (index & 1) == 0 ? evenColor : oddColor;
        }
    }
}

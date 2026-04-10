using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 按键列表项。
/// ScrollView 中的每一行，显示"功能名"和"当前按键"，点击进入换绑流程。
///
/// ═══ UI 组件结构 ═══
///
/// KeyItem (this, 挂在列表项根节点)
/// ├── ActionNameText (TMP_Text) ← actionNameText  显示 "跳跃"
/// ├── BindingNameText (TMP_Text) ← bindingNameText 显示 "Space"
/// └── InteractiveArea (Button)   ← interactiveButton  点击触发换绑
///
/// 所有 UI 组件由你在 Unity 中搭建，此脚本只负责逻辑。
/// </summary>
public class KeyItem : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private TMPro.TMP_Text actionNameText;
    [SerializeField] private TMPro.TMP_Text bindingNameText;
    [SerializeField] private Button interactiveButton;

    /// <summary>此列表项绑定的 InputAction。</summary>
    public InputAction BoundAction { get; private set; }

    /// <summary>此列表项对应的绑定索引。</summary>
    public int BindingIndex { get; private set; }

    /// <summary>点击事件，UIShortcutPanel 监听。</summary>
    public event Action<KeyItem> OnItemClicked;

    // ─── 初始化 ───

    /// <summary>
    /// 由 UIShortcutPanel 调用，设置此列表项的数据。
    /// </summary>
    public void Initialize(InputAction action, int bindingIndex,
                           string displayName, string bindingDisplayText)
    {
        BoundAction = action;
        BindingIndex = bindingIndex;

        if (actionNameText != null)
            actionNameText.text = displayName;

        if (bindingNameText != null)
            bindingNameText.text = bindingDisplayText;

        if (interactiveButton != null)
        {
            interactiveButton.onClick.RemoveAllListeners();
            interactiveButton.onClick.AddListener(OnClick);
        }
    }

    /// <summary>更新按键显示文本（换绑完成后由 UIShortcutPanel 调用）。</summary>
    public void UpdateBindingDisplay(string newDisplayString)
    {
        if (bindingNameText != null)
            bindingNameText.text = newDisplayString;
    }

    private void OnClick()
    {
        OnItemClicked?.Invoke(this);
    }

    private void OnDestroy()
    {
        if (interactiveButton != null)
            interactiveButton.onClick.RemoveAllListeners();
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 按键设置面板。
/// 负责管理 ScrollView 中的 KeyItem 列表，以及换绑弹窗流程。
///
/// ═══ 换绑 UI 流程 ═══
///
/// 1. 面板打开时，自动为 InputReader 中每个 GamePlay Action 生成一个 KeyItem
/// 2. 玩家点击某个 KeyItem → 弹出遮罩 + "请按下一个按键" 弹窗
/// 3. 玩家按下新按键 → 弹窗更新为新按键名 → 1 秒后关闭弹窗
/// 4. ESC 取消换绑 → 直接关闭弹窗
/// 5. 弹窗期间遮罩屏蔽所有其他交互
///
/// ═══ 组件搭建指南 ═══
///
/// UIShortcutPanel (this)
/// ├── ScrollView
/// │   └── Content (Vertical Layout Group) ← contentParent
/// │       ├── KeyItem (Prefab 实例)
/// │       ├── KeyItem ...
/// │       └── ...
/// ├── OverlayMask (全屏半透明黑色 Image) ← overlayMask
/// └── RebindPopup (居中弹窗) ← rebindPopup
///     └── PromptText (TMP_Text) ← promptText
/// </summary>
public class UIShortcutPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private RebindManager rebindManager;

    [Header("UI Components")]
    [SerializeField] private Transform contentParent;   // ScrollView 的 Content
    [SerializeField] private GameObject keyItemPrefab;   // KeyItem 预制体
    [SerializeField] private GameObject overlayMask;     // 黑遮罩（全屏）
    [SerializeField] private GameObject rebindPopup;     // 换绑弹窗
    [SerializeField] private TMPro.TMP_Text promptText;  // 弹窗提示文本

    [Header("Settings")]
    [SerializeField] private float popupDismissDelay = 1f; // 换绑成功后弹窗关闭延迟

    private readonly List<KeyItem> _keyItems = new List<KeyItem>();

    // ─── 生命周期 ───

    private void OnEnable()
    {
        // 切换到 UI 输入模式
        if (inputReader != null)
            inputReader.SetFocus(InputFocusMode.UI);

        // 监听 RebindManager 回调
        if (rebindManager != null)
        {
            rebindManager.OnRebindComplete += HandleRebindComplete;
            rebindManager.OnRebindCanceled += HandleRebindCanceled;
            rebindManager.OnRebindStarted += HandleRebindStarted;
        }

        DismissPopup();
        PopulateKeyItems();
    }

    private void OnDisable()
    {
        if (rebindManager != null)
        {
            rebindManager.OnRebindComplete -= HandleRebindComplete;
            rebindManager.OnRebindCanceled -= HandleRebindCanceled;
            rebindManager.OnRebindStarted -= HandleRebindStarted;
        }

        // 恢复 Gameplay 输入模式
        if (inputReader != null)
            inputReader.SetFocus(InputFocusMode.Gameplay);
    }

    // ─── 列表填充 ───

    /// <summary>
    /// 遍历 InputReader 的 ActionAsset，为每个 GamePlay Action 创建一个 KeyItem。
    /// </summary>
    private void PopulateKeyItems()
    {
        if (contentParent == null)
        {
            Debug.LogWarning("[UIShortcutPanel] contentParent is null, skip PopulateKeyItems.", this);
            return;
        }

        // 清空旧的
        foreach (var item in _keyItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        _keyItems.Clear();

        if (inputReader == null || inputReader.ActionAsset == null || keyItemPrefab == null) return;

        // 遍历 GamePlay ActionMap 下的所有 Action
        var actionMap = inputReader.ActionAsset.FindActionMap("GamePlay");
        if (actionMap == null) return;

        foreach (var action in actionMap.actions)
        {
            // 跳过 Pause（ESC 是系统保留键，不允许换绑）
            if (action.name == "Pause") continue;

            // 找到键鼠方案的绑定索引
            int bindingIndex = FindKeyboardBindingIndex(action);
            if (bindingIndex < 0) continue;

            var go = Instantiate(keyItemPrefab, contentParent);
            var keyItem = go.GetComponent<KeyItem>();
            if (keyItem == null) continue;

            var displayName = GetActionDisplayName(action.name);
            var bindingText = action.GetBindingDisplayString(bindingIndex);

            keyItem.Initialize(action, bindingIndex, displayName, bindingText);
            keyItem.OnItemClicked += HandleKeyItemClicked;
            _keyItems.Add(keyItem);
        }
    }

    /// <summary>找到 Action 中属于键鼠方案的第一个非组合绑定索引。</summary>
    private int FindKeyboardBindingIndex(InputAction action)
    {
        for (int i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            // 跳过组合绑定的父级（如 WASD 的 2DVector）
            if (binding.isComposite) continue;
            // 跳过组合子级（up/down/left/right 属于 2DVector 组合）
            if (binding.isPartOfComposite) continue;
            // 匹配键鼠方案
            if (string.IsNullOrEmpty(binding.groups) ||
                binding.groups.Contains("Keyboard"))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Action 名转中文显示名。</summary>
    private string GetActionDisplayName(string actionName)
    {
        switch (actionName)
        {
            case "Move": return "移动";
            case "Look": return "视角";
            case "Attack": return "攻击";
            case "Jump": return "跳跃";
            case "Dodge": return "闪避";
            case "Interact": return "交互";
            default: return actionName;
        }
    }

    // ─── 弹窗流程 ───

    private void HandleKeyItemClicked(KeyItem item)
    {
        if (rebindManager == null || rebindManager.IsRebinding) return;
        rebindManager.StartRebind(item.BoundAction, item.BindingIndex);
    }

    private void HandleRebindStarted(string actionName)
    {
        ShowPopup("请按下一个按键...");
    }

    private void HandleRebindComplete(InputAction action, int bindingIndex, string newDisplayString)
    {
        // 更新弹窗显示新按键
        ShowPopup(newDisplayString);

        // 更新对应 KeyItem 的显示
        RefreshKeyItem(action, bindingIndex, newDisplayString);

        // 延时关闭弹窗
        StartCoroutine(DismissPopupDelayed(popupDismissDelay));
    }

    private void HandleRebindCanceled()
    {
        DismissPopup();
    }

    private void ShowPopup(string text)
    {
        if (overlayMask != null) overlayMask.SetActive(true);
        if (rebindPopup != null) rebindPopup.SetActive(true);
        if (promptText != null) promptText.text = text;
    }

    private void DismissPopup()
    {
        if (overlayMask != null) overlayMask.SetActive(false);
        if (rebindPopup != null) rebindPopup.SetActive(false);
    }

    private IEnumerator DismissPopupDelayed(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        DismissPopup();
    }

    /// <summary>找到对应的 KeyItem 并更新按键显示文本。</summary>
    private void RefreshKeyItem(InputAction action, int bindingIndex, string displayString)
    {
        foreach (var item in _keyItems)
        {
            if (item.BoundAction == action && item.BindingIndex == bindingIndex)
            {
                item.UpdateBindingDisplay(displayString);
                break;
            }
        }
    }

    // ─── 公开工具 ───

    /// <summary>恢复所有按键为默认绑定。</summary>
    public void ResetAllBindings()
    {
        if (rebindManager != null)
        {
            rebindManager.ResetAllBindings();
            PopulateKeyItems(); // 重新填充列表
        }
    }
}

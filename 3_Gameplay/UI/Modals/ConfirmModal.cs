using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>示例 Modal：_props 驱动的确认框。</summary>
public sealed class ConfirmModal : UIModal<ConfirmModalProps>
{
    [SerializeField] TMP_Text titleText;

    [SerializeField] TMP_Text messageText;

    [SerializeField] Button confirmButton;

    [SerializeField] Button cancelButton;

    protected override void OnPropsSet()
    {
        if (titleText != null)
        {
            titleText.text = Props.Title ?? string.Empty;
        }

        if (messageText != null)
        {
            messageText.text = Props.Message ?? string.Empty;
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(() => Props.OnConfirm?.Invoke());
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(() => Props.OnCancel?.Invoke());
        }
    }
}

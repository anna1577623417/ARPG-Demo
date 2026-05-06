using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>示例全屏暂停：由 GameManager / 暂停流程通过 <see cref="UIRoot.ShowScreen"/> 打开。</summary>
public sealed class PauseMenuScreen : UIScreen<PauseMenuProps>
{
    [SerializeField] TMP_Text subtitle;

    [SerializeField] Button resumeButton;

    [SerializeField] Button settingsButton;

    protected override void OnPropsSet()
    {
        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveAllListeners();
            resumeButton.onClick.AddListener(() => Props.OnResume?.Invoke());
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(() => Props.OnOpenSettings?.Invoke());
        }

        if (subtitle != null)
        {
            subtitle.text = string.Empty;
        }
    }
}

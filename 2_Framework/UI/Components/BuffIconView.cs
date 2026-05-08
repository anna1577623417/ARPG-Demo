using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单个 Buff 图标：剩余时长环与叠层数由 Presenter 推送；环可在 View 内自驱动（可选）。
/// </summary>
[AddComponentMenu("GameMain/UI/Buff Icon View")]
public sealed class BuffIconView : MonoBehaviour
{
    [SerializeField] Image iconImage;

    [Tooltip("剩余时间比例（Filled）。fillAmount = remaining / duration。")]
    [SerializeField] Image durationRing;

    [SerializeField] TMP_Text stackCountText;

    [Tooltip("叠层大于 1 时显示数字。")]
    [SerializeField] bool hideStackWhenOne = true;

    /// <summary>刷新显示（高频可由 Presenter 每帧调用）。</summary>
    public void Refresh(in BuffInstance instance, int stackCountForDefinition)
    {
        var def = instance.Definition;
        if (iconImage != null)
        {
            if (def != null && def.Icon != null)
            {
                iconImage.sprite = def.Icon;
                iconImage.enabled = true;
            }
            else
            {
                iconImage.enabled = false;
            }
        }

        var duration = def != null ? Mathf.Max(0.01f, def.Duration) : 1f;
        var remaining = Mathf.Clamp(instance.RemainingSeconds, 0f, duration);
        if (durationRing != null)
        {
            durationRing.fillAmount = Mathf.Clamp01(remaining / duration);
        }

        if (stackCountText != null)
        {
            if (hideStackWhenOne && stackCountForDefinition <= 1)
            {
                stackCountText.text = string.Empty;
            }
            else
            {
                stackCountText.text = stackCountForDefinition.ToString();
            }
        }
    }
}

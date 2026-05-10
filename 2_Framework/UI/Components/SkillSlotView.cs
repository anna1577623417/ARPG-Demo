using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 技能槽 View — 纯显示终端，不知道 SkillRuntime / SkillData 存在。
///
/// ═══ 职责（v4.3 铁规）═══
///
/// · 仅接受 Presenter 调用 SetIcon / SetCooldownProgress / SetCooldownText / SetLevel / SetAvailable / SetHighlight
/// · 不订阅任何事件，不持任何数据引用
/// · 高频 CD 倒计时由配套的 SkillCooldownTicker（同 GameObject）自驱动
///
/// ═══ 视觉层级（推荐 Hierarchy）═══
///
///   SkillSlot
///     ├── BG (Image)
///     ├── Frame (Image)
///     ├── Icon (Image)
///     ├── CooldownMask (Image, Filled / Radial360 / Origin top)
///     ├── CooldownText (TMP_Text)
///     ├── HighlightFrame (Image, Filled；语义=「可释放指示」。fillAmount=1 技能就绪，=0 刚进入冷却；alpha 仍由 SetHighlight 控制)
///     ├── LevelGroup (CanvasGroup)
///     │   └── Level_1..N (Image[])
///     └── CDBlocker (Image, raycast=true, alpha=0；CD 中阻挡点击)
/// </summary>
[AddComponentMenu("GameMain/UI/Skill Slot View")]
public sealed class SkillSlotView : MonoBehaviour
{
    [Header("Layers")]
    [SerializeField] Image iconImage;

    [Tooltip(
        "冷却遮挡（如场景中的 Bloker_CD）。重要语义：Image.fillAmount 表示「遮罩覆盖比例」。" +
        "1 = 刚进入冷却（满遮罩），0 = 冷却结束（无遮罩）。与 SetCooldownProgress 一致：内部用 fillAmount=1-进度。")]
    [SerializeField] Image cooldownMask;

    [SerializeField] TMP_Text cooldownText;

    [Tooltip(
        "高光 CD 框（Image Type 须设为 Filled）— 语义=「可释放指示」。\n" +
        "fillAmount = 1 → 技能可释放（CD 走完）；\n" +
        "fillAmount = 0 → 刚进入冷却；\n" +
        "随 CD 走完线性回填到 1。alpha 由 SetHighlight 单独控制（与 fill 解耦）。")]
    [SerializeField] Image highlightFrame;

    [Tooltip("关闭后不再驱动 highlightFrame.fillAmount（仅留 SetHighlight 控制的 alpha）。")]
    [SerializeField] bool driveHighlightCooldownFill = true;

    [SerializeField] CanvasGroup levelGroup;

    [Tooltip("等级指示器（按数组顺序激活前 N 个）。")]
    [SerializeField] Image[] levelIndicators;

    [Tooltip("CD 中阻挡点击的透明 Image。CD 完成时禁用以避免 raycast overdraw。")]
    [SerializeField] GameObject cdBlocker;

    [Tooltip("按键提示（如 LMB / RMB / Shift），与 .inputactions 中实际绑定一致，仅展示用。")]
    [SerializeField] TMP_Text keyHintText;

    [Header("Presentation")]
    [Tooltip("CD 文本显示的小数位数（0 = 整数 12，1 = 12.3）。")]
    [SerializeField, Range(0, 2)] int cooldownTextDecimals = 1;

    [Tooltip("剩余 CD 低于此值时不再显示文本（默认 0.05）。")]
    [SerializeField] float cooldownTextHideBelow = 0.05f;

    [Tooltip("不可用时图标变灰强度 (0=完全灰)。")]
    [SerializeField, Range(0f, 1f)] float unavailableIconAlpha = 0.4f;

    Color m_iconColorAvailable = Color.white;

    void Awake()
    {
        if (iconImage != null)
        {
            m_iconColorAvailable = iconImage.color;
        }
    }

    // ─── 固定数据（一次性 Set） ───────────────────────────────────────────────

    public void SetIcon(Sprite sprite)
    {
        if (iconImage == null) return;
        iconImage.sprite = sprite;
        iconImage.enabled = sprite != null;
    }

    /// <summary>设置等级指示器数量。低频调用。</summary>
    public void SetLevel(int level)
    {
        if (levelIndicators == null) return;
        for (var i = 0; i < levelIndicators.Length; i++)
        {
            if (levelIndicators[i] != null)
            {
                levelIndicators[i].enabled = i < level;
            }
        }
    }

    public void SetHighlightColor(Color color)
    {
        if (highlightFrame == null) return;
        var c = color;
        c.a = highlightFrame.color.a;   // 保留原 alpha（由 SetHighlight 控制）
        highlightFrame.color = c;
    }

    // ─── 高频数据（CD Ticker 每帧调） ─────────────────────────────────────────

    /// <summary>
    /// CD 进度（冷却剩余进度）：0 = 刚进入冷却（遮挡应为满），1 = 冷却结束（遮挡清空）。
    /// 遮挡 Image.fillAmount = 1 - progress01。
    /// </summary>
    public void SetCooldownProgress(float progress01)
    {
        var p = Mathf.Clamp01(progress01);
        if (cooldownMask != null)
        {
            cooldownMask.fillAmount = 1f - p;
            cooldownMask.enabled = p < 1f;
        }

        if (highlightFrame != null && driveHighlightCooldownFill)
        {
            // 语义：fill=1 可释放，fill=0 刚冷却 → 直接等于 progress01
            highlightFrame.fillAmount = p;
        }

        if (cdBlocker != null)
        {
            // CD 完成时禁用阻挡，防止 raycast overdraw
            var shouldBlock = p < 1f;
            if (cdBlocker.activeSelf != shouldBlock)
            {
                cdBlocker.SetActive(shouldBlock);
            }
        }
    }

    /// <summary>剩余 CD 秒数。低于阈值时清空文本。</summary>
    public void SetCooldownRemaining(float seconds)
    {
        if (cooldownText == null) return;

        if (seconds < cooldownTextHideBelow)
        {
            if (cooldownText.text.Length != 0)
            {
                cooldownText.text = string.Empty;
            }
            return;
        }

        cooldownText.text = cooldownTextDecimals switch
        {
            0 => Mathf.CeilToInt(seconds).ToString(),
            1 => seconds.ToString("0.0"),
            _ => seconds.ToString("0.00"),
        };
    }

    // ─── 状态切换（低频） ─────────────────────────────────────────────────────

    /// <summary>0 = 隐藏，1 = 完全显示。Presenter 在 CD 完成 / 刚使用 时调。</summary>
    public void SetHighlight(float alpha)
    {
        if (highlightFrame == null) return;
        var c = highlightFrame.color;
        c.a = Mathf.Clamp01(alpha);
        highlightFrame.color = c;
    }

    /// <summary>资源不足 / 沉默 时变灰。</summary>
    public void SetAvailable(bool available)
    {
        if (iconImage == null) return;
        if (available)
        {
            iconImage.color = m_iconColorAvailable;
        }
        else
        {
            var c = m_iconColorAvailable;
            c.a = unavailableIconAlpha;
            iconImage.color = c;
        }
    }

    /// <summary>设置 HUD 按键提示文案（绑定展示；不影响 InputSystem）。</summary>
    public void SetKeyHint(string label)
    {
        if (keyHintText == null)
        {
            return;
        }

        keyHintText.text = label ?? string.Empty;
        keyHintText.enabled = !string.IsNullOrEmpty(label);
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 泛用资源条 View（HP / MP / Stamina / Shield 复用）。
///
/// ═══ 职责边界（v4.3 铁规）═══
///
/// · View 永远不读取 Player / Stats / Resources，只接受 Presenter 的 Set* 调用
/// · 高频动画（Buffer 缓动）在 View 内 Update 自驱动；Presenter 只在事件时 Set 一次
/// · 不区分 HP/MP/Stamina——任何归一化值都能驱动
///
/// ═══ Buffer 行为 ═══
///
/// · Drain（受伤/消耗）：Fill 立即下降，Buffer 暂留，缓慢追上 Fill
/// · Refill（治疗/回复）：Buffer 直接对齐 Fill（看不到延迟意义）
/// </summary>
[AddComponentMenu("GameMain/UI/Resource Bar View")]
public sealed class ResourceBarView : MonoBehaviour, IUIThemeable
{
    [Header("Visuals")]
    [Tooltip("当前值 fill。Image 须为 Filled / Horizontal。")]
    [SerializeField] Image fillImage;

    [Tooltip("延迟跟随 buffer。Image 须为 Filled / Horizontal。可选。")]
    [SerializeField] Image bufferImage;

    [Tooltip("数值文本（'120 / 200' 等）。可选。")]
    [SerializeField] TMP_Text valueText;

    [Tooltip("文本可见性控制（推荐用 CanvasGroup.alpha 而非 SetActive，避免 Layout Rebuild）。")]
    [SerializeField] CanvasGroup textGroup;

    [Header("Buffer Animation (View 自驱动)")]
    [Tooltip("Buffer 追上 Fill 的速度（归一化 / 秒）。0.5 慢吞吞，10 几乎瞬时。")]
    [SerializeField, Range(0.5f, 20f)] float bufferFollowSpeed = 6f;

    [Tooltip("仅 Drain 时启用 Buffer 延迟；Refill 时立即对齐。关闭则两者都缓动。")]
    [SerializeField] bool useBufferOnDrainOnly = true;

    [Tooltip("Drain 后多少秒开始缓动（视觉上让玩家先看到伤害）。")]
    [SerializeField, Range(0f, 1f)] float bufferDelaySeconds = 0.25f;

    [Header("Theme (optional)")]
    [SerializeField] bool useTheme;
    [SerializeField] UIRoot uiRoot;
    [SerializeField] UIThemeRole fillRole = UIThemeRole.Danger;
    [SerializeField] UIThemeRole bufferRole = UIThemeRole.Danger;
    [SerializeField] UIThemeRole textRole = UIThemeRole.Primary;

    float m_normalized;        // Fill 当前值 (0..1)
    float m_bufferNormalized;  // Buffer 当前值 (0..1)
    float m_bufferDelayTimer;  // Drain 后的延迟计时

    void OnEnable()
    {
        if (!useTheme)
        {
            return;
        }

        if (uiRoot == null)
        {
            UIRoot.TryGetPersistentInstance(out uiRoot);
        }

        uiRoot?.RegisterThemeTarget(this, applyNow: true);
    }

    void OnDisable()
    {
        if (!useTheme)
        {
            return;
        }

        uiRoot?.UnregisterThemeTarget(this);
    }

    // ─── Presenter 调用入口（铁规：只接受 Set，不主动查询） ───────────────────

    /// <summary>设置当前归一化值。Drain 时 Buffer 暂留，Refill 时直接对齐。</summary>
    public void SetNormalized(float current01)
    {
        var clamped = Mathf.Clamp01(current01);
        var isDrain = clamped < m_normalized - 1e-4f;
        m_normalized = clamped;

        if (fillImage != null)
        {
            fillImage.fillAmount = m_normalized;
        }

        if (bufferImage == null)
        {
            return;
        }

        if (isDrain)
        {
            // Buffer 暂留旧值，启动延迟计时
            m_bufferDelayTimer = bufferDelaySeconds;
        }
        else
        {
            // Refill 或对齐：buffer 直接对齐 fill
            m_bufferNormalized = m_normalized;
            bufferImage.fillAmount = m_bufferNormalized;
            m_bufferDelayTimer = 0f;
        }
    }

    /// <summary>显示值文本（如 "120 / 200"）。Presenter 在 Resource/Stat 变化时格式化好再传入。</summary>
    public void SetText(string text)
    {
        if (valueText != null)
        {
            valueText.text = text;
        }
    }

    /// <summary>用 alpha 切换文本可见性。SetActive 会触发 Layout Rebuild。</summary>
    public void SetTextVisible(bool visible)
    {
        if (textGroup != null)
        {
            textGroup.alpha = visible ? 1f : 0f;
        }
        else if (valueText != null)
        {
            var c = valueText.color;
            c.a = visible ? 1f : 0f;
            valueText.color = c;
        }
    }

    /// <summary>Theme 等系统覆盖颜色用。</summary>
    public void SetFillColor(Color color)
    {
        if (fillImage != null)
        {
            fillImage.color = color;
        }
    }

    public void SetBufferColor(Color color)
    {
        if (bufferImage != null)
        {
            bufferImage.color = color;
        }
    }

    // ─── View 自驱动动画（Buffer 缓动追上 Fill） ──────────────────────────────

    void Update()
    {
        if (bufferImage == null)
        {
            return;
        }

        // 已对齐：无须计算
        if (m_bufferNormalized <= m_normalized + 1e-4f)
        {
            return;
        }

        // 延迟期：Buffer 不动
        if (m_bufferDelayTimer > 0f)
        {
            m_bufferDelayTimer -= Time.deltaTime;
            return;
        }

        // 缓动追上 Fill
        m_bufferNormalized = Mathf.MoveTowards(
            m_bufferNormalized,
            m_normalized,
            bufferFollowSpeed * Time.deltaTime);
        bufferImage.fillAmount = m_bufferNormalized;
    }

    /// <summary>初始化或重置时强制对齐两条。Presenter 在 Bind 后立即调用一次。</summary>
    public void ForceSync(float current01)
    {
        m_normalized = Mathf.Clamp01(current01);
        m_bufferNormalized = m_normalized;
        m_bufferDelayTimer = 0f;
        if (fillImage != null) fillImage.fillAmount = m_normalized;
        if (bufferImage != null) bufferImage.fillAmount = m_bufferNormalized;
    }

    public void ApplyTheme(UIThemeSO theme)
    {
        if (!useTheme || theme == null)
        {
            return;
        }

        if (fillImage != null)
        {
            fillImage.color = ResolveThemeColor(theme, fillRole);
        }

        if (bufferImage != null)
        {
            var c = ResolveThemeColor(theme, bufferRole);
            c.a *= 0.6f;
            bufferImage.color = c;
        }

        if (valueText != null)
        {
            valueText.color = ResolveThemeColor(theme, textRole);
        }
    }

    static Color ResolveThemeColor(UIThemeSO theme, UIThemeRole role)
    {
        switch (role)
        {
            case UIThemeRole.Secondary:
                return theme.secondary;
            case UIThemeRole.Accent:
                return theme.accent;
            case UIThemeRole.Warning:
                return theme.warning;
            case UIThemeRole.Danger:
                return theme.danger;
            case UIThemeRole.Background:
                return theme.background;
            default:
                return theme.primary;
        }
    }
}

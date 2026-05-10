using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 泛用资源条 View（HP / MP / Stamina / Shield 复用）。
///
/// ═══ 职责边界（v4.3 铁规）═══
///
/// · View 永远不读取 Player / Stats / Resources，只接受 Presenter 的 Set* 调用
/// · Buffer 动画在 View 内自驱动（策略可选）；Presenter 只在事件时 Set 一次
///
/// ═══ 双层 Buffer ═══
///
/// · Drain：主 Fill 立即跟随数据；Ghost/Buffer 按 <see cref="ResourceBarBufferFollowStrategy"/> 追上
/// · Refill：默认 Buffer 立即对齐 Fill（可配置 useBufferOnDrainOnly）
///
/// 策略说明见 <see cref="ResourceBarBufferFollowStrategy"/>；可选 <see cref="ResourceBarBufferConfigSO"/> 数据驱动。
/// </summary>
[AddComponentMenu("GameMain/UI/Resource Bar View")]
public sealed class ResourceBarView : MonoBehaviour, IUIThemeable
{
    [Header("Data Binding (Presenter auto-discovery)")]
    [Tooltip("开启后，PlayerHUDPresenter 可自动识别此资源条并建立事件驱动数据链接。")]
    [SerializeField] bool autoBindInHud = true;

    [Tooltip("此条对应的资源类型（HP / Stamina / MP / Energy）。")]
    [SerializeField] ResourceType resourceType = ResourceType.HP;

    [Tooltip("默认文本模板。留空则由 Presenter 使用其全局默认模板。")]
    [SerializeField] string defaultTextFormat = "{cur} / {max}";

    [Header("Visuals")]
    [Tooltip("当前值 fill。Image 须为 Filled / Horizontal。")]
    [SerializeField] Image fillImage;

    [Tooltip("延迟跟随 buffer。Image 须为 Filled / Horizontal。可选。")]
    [SerializeField] Image bufferImage;

    [Tooltip("数值文本（'120 / 200' 等）。可选。")]
    [SerializeField] TMP_Text valueText;

    [Tooltip("文本可见性控制（推荐用 CanvasGroup.alpha 而非 SetActive，避免 Layout Rebuild）。")]
    [SerializeField] CanvasGroup textGroup;

    [Header("Buffer Pipeline (config)")]
    [Tooltip("若赋值则优先使用该 SO 的全套 Buffer 参数；否则使用下方 Fallback。")]
    [SerializeField] ResourceBarBufferConfigSO bufferConfig;

    [Header("Buffer Fallback（bufferConfig 为空时使用）")]
    [SerializeField] ResourceBarBufferFollowStrategy fallbackStrategy = ResourceBarBufferFollowStrategy.MoveTowardsAfterDelay;

    [Tooltip("延迟窗口（秒）。SmoothDuringDelay 下 = 平滑插值总时长。")]
    [SerializeField, Min(0f)] float bufferDelaySeconds = 0.25f;

    [Tooltip("仅 MoveTowardsAfterDelay：延迟结束后每秒归一化移动量。")]
    [SerializeField, Range(0.5f, 20f)] float bufferFollowSpeed = 6f;

    [Tooltip("仅 Drain 时启用 Buffer 延迟逻辑；Refill 时立即对齐。")]
    [SerializeField] bool useBufferOnDrainOnly = true;

    [SerializeField] ResourceBarBufferEase fallbackEase = ResourceBarBufferEase.OutQuad;

    [Tooltip("Drain 时变化量小于此值则不触发 Buffer 动画（直接对齐）。")]
    [SerializeField, Min(0f)] float minChangeThreshold = 0.001f;

    [Header("Theme (optional)")]
    [SerializeField] bool useTheme;
    [SerializeField] UIRoot uiRoot;
    [SerializeField] UIThemeRole fillRole = UIThemeRole.Danger;
    [SerializeField] UIThemeRole bufferRole = UIThemeRole.Danger;
    [SerializeField] UIThemeRole textRole = UIThemeRole.Primary;

    float m_normalized;
    float m_bufferNormalized;

    // ── SmoothDuringDelay
    bool m_smoothActive;
    float m_smoothElapsed;
    float m_smoothDuration;
    float m_smoothFrom;
    ResourceBarBufferEase m_smoothEase;

    // ── SnapAfterDelay
    float m_snapDelayRemaining;

    // ── MoveTowardsAfterDelay（legacy）
    float m_moveTowardsDelayTimer;

    public bool AutoBindInHud => autoBindInHud;
    public ResourceType BoundResourceType => resourceType;
    public string DefaultTextFormat => defaultTextFormat;

    ResourceBarBufferFollowStrategy Strategy =>
        bufferConfig != null ? bufferConfig.strategy : fallbackStrategy;

    float BufferDelay =>
        bufferConfig != null ? bufferConfig.bufferDelay : bufferDelaySeconds;

    float MoveTowardsSpeed =>
        bufferConfig != null ? bufferConfig.moveTowardsSpeed : bufferFollowSpeed;

    bool UseBufferOnDrainOnly =>
        bufferConfig != null ? bufferConfig.useBufferOnDrainOnly : useBufferOnDrainOnly;

    ResourceBarBufferEase InterpolationEase =>
        bufferConfig != null ? bufferConfig.interpolationEase : fallbackEase;

    float MinChangeThreshold =>
        bufferConfig != null ? bufferConfig.minChangeThreshold : minChangeThreshold;

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

    void CancelBufferAnimation()
    {
        m_smoothActive = false;
        m_smoothElapsed = 0f;
        m_snapDelayRemaining = 0f;
        m_moveTowardsDelayTimer = 0f;
    }

    /// <summary>设置当前归一化值。Drain 时按策略驱动 Buffer；Refill 默认立刻对齐。</summary>
    public void SetNormalized(float current01)
    {
        var oldNorm = m_normalized;
        var clamped = Mathf.Clamp01(current01);
        var isDrain = clamped < oldNorm - 1e-4f;
        m_normalized = clamped;

        if (fillImage != null)
        {
            fillImage.fillAmount = m_normalized;
        }

        if (bufferImage == null)
        {
            return;
        }

        var delta = Mathf.Abs(oldNorm - clamped);
        if (isDrain && delta < MinChangeThreshold)
        {
            m_bufferNormalized = m_normalized;
            bufferImage.fillAmount = m_bufferNormalized;
            CancelBufferAnimation();
            return;
        }

        if (!isDrain)
        {
            if (UseBufferOnDrainOnly)
            {
                m_bufferNormalized = m_normalized;
                bufferImage.fillAmount = m_bufferNormalized;
                CancelBufferAnimation();
            }
            else
            {
                // 兼容旧版：回复也走延迟 + Buffer 追上主条
                RestartBufferAnimation();
            }

            return;
        }

        // Drain：杀死上一段 Buffer 任务，按策略重启（并发重置）
        RestartBufferAnimation();
    }

    void RestartBufferAnimation()
    {
        CancelBufferAnimation();
        m_smoothEase = InterpolationEase;

        switch (Strategy)
        {
            case ResourceBarBufferFollowStrategy.SmoothDuringDelay:
                m_smoothFrom = m_bufferNormalized;
                m_smoothElapsed = 0f;
                m_smoothDuration = Mathf.Max(0.0001f, BufferDelay);
                m_smoothActive = true;
                break;

            case ResourceBarBufferFollowStrategy.SnapAfterDelay:
                m_snapDelayRemaining = BufferDelay;
                break;

            case ResourceBarBufferFollowStrategy.MoveTowardsAfterDelay:
                m_moveTowardsDelayTimer = BufferDelay;
                break;
        }
    }

    public void SetText(string text)
    {
        if (valueText != null)
        {
            valueText.text = text;
        }
    }

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

    void Update()
    {
        if (bufferImage == null)
        {
            return;
        }

        if (Mathf.Abs(m_bufferNormalized - m_normalized) <= 1e-4f
            && !m_smoothActive
            && m_snapDelayRemaining <= 0f
            && m_moveTowardsDelayTimer <= 0f)
        {
            return;
        }

        switch (Strategy)
        {
            case ResourceBarBufferFollowStrategy.SmoothDuringDelay:
                UpdateSmoothDuringDelay();
                break;

            case ResourceBarBufferFollowStrategy.SnapAfterDelay:
                UpdateSnapAfterDelay();
                break;

            case ResourceBarBufferFollowStrategy.MoveTowardsAfterDelay:
                UpdateMoveTowardsAfterDelay();
                break;
        }
    }

    void UpdateSmoothDuringDelay()
    {
        if (!m_smoothActive)
        {
            return;
        }

        m_smoothElapsed += Time.deltaTime;
        var t = Mathf.Clamp01(m_smoothElapsed / m_smoothDuration);
        var k = ResourceBarBufferEasing.Evaluate(t, m_smoothEase);
        m_bufferNormalized = Mathf.Lerp(m_smoothFrom, m_normalized, k);
        bufferImage.fillAmount = m_bufferNormalized;

        if (t >= 1f - 1e-4f)
        {
            m_bufferNormalized = m_normalized;
            bufferImage.fillAmount = m_bufferNormalized;
            m_smoothActive = false;
        }
    }

    void UpdateSnapAfterDelay()
    {
        if (m_snapDelayRemaining > 0f)
        {
            m_snapDelayRemaining -= Time.deltaTime;
            if (m_snapDelayRemaining <= 0f)
            {
                m_bufferNormalized = m_normalized;
                bufferImage.fillAmount = m_bufferNormalized;
                m_snapDelayRemaining = 0f;
            }
        }
    }

    void UpdateMoveTowardsAfterDelay()
    {
        if (Mathf.Abs(m_bufferNormalized - m_normalized) <= 1e-4f)
        {
            return;
        }

        if (m_moveTowardsDelayTimer > 0f)
        {
            m_moveTowardsDelayTimer -= Time.deltaTime;
            return;
        }

        m_bufferNormalized = Mathf.MoveTowards(
            m_bufferNormalized,
            m_normalized,
            MoveTowardsSpeed * Time.deltaTime);
        bufferImage.fillAmount = m_bufferNormalized;
    }

    /// <summary>初始化或重置时强制对齐两条。Presenter 在 Bind 后立即调用一次。</summary>
    public void ForceSync(float current01)
    {
        m_normalized = Mathf.Clamp01(current01);
        m_bufferNormalized = m_normalized;
        CancelBufferAnimation();
        if (fillImage != null)
        {
            fillImage.fillAmount = m_normalized;
        }

        if (bufferImage != null)
        {
            bufferImage.fillAmount = m_bufferNormalized;
        }
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

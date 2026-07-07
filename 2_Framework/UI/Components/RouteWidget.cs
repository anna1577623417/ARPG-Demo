using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD 单条 Route 视图 — 渲染图标 / 冷却罩 / 高光边框 / 资源不足罩 / 蓄力条 / 连段 / 段位指示。
///
/// ═══ 设计契约（与 IRouteRuntimeHandle 一一对应）═══
///   · 只读绑定：Bind(handle) 后 LateUpdate 自动拉取，无事件订阅，无 GC。
///   · CD 遮罩 fill = 1 - CdProgress01；高光 fill = CdProgress01（同帧同步，CD 中 0→1）。
///   · 高光常显，除非 IsHighlightSuppressed（资源不足 / 条件未满足，与 CD 无关）。
///   · 资源不足罩：Image 启用；仅非 CD 且缺资源时显示。
/// </summary>
[AddComponentMenu("GameMain/UI/Route Widget")]
public sealed class RouteWidget : MonoBehaviour
{
    [Header("Base")]
    [SerializeField] Image iconImage;
    [SerializeField] TMP_Text keyLabel;
    [SerializeField] TMP_Text displayNameLabel;

    [Header("Cooldown (Filled Image)")]
    [SerializeField, Tooltip("CD 旋转/线性遮罩。fillAmount = 1 - CdProgress01（满罩 = 刚进入 CD）。")]
    Image cooldownMask;

    [SerializeField, Tooltip("技能高光边框（Filled）。与 CD 同步：fill=CdProgress01（进 CD 为 0，转好为 1）。资源不足/条件未满足时整框隐藏。")]
    Image highlightBorder;

    [SerializeField, Tooltip("CD 剩余秒数字（1 位小数，如 5.0）。仅 IsOnCooldown 时显示；0-GC 更新。")]
    TMP_Text cooldownTimerText;

    [Header("Cast Block Mask (Image, LoL-style)")]
    [SerializeField, Tooltip("资源不足时启用（缺蓝/能量等）；CD 中自动隐藏。需 Image Type = Filled 或 Simple 均可。")]
    Image resourceInsufficientMask;

    [Header("Charge (ChargeRoute only)")]
    [SerializeField] Image chargeBar;

    [Header("Combo Overlay (ComboRoute only)")]
    [SerializeField] GameObject comboGroup;
    [SerializeField] TMP_Text comboStepLabel;
    [SerializeField] Image comboWindowRing;

    [Header("MultiStage Overlay")]
    [SerializeField] GameObject multiStageGroup;
    [SerializeField] TMP_Text multiStageIndexLabel;
    [SerializeField] Image stageProgressBar;
    [SerializeField, Tooltip("215 — MultiStage Pending 窗口高亮环（可选）。")]
    Image multiStagePendingRing;

    [Header("Presentation Badge (215)")]
    [SerializeField] GameObject badgePrimaryRoot;
    [SerializeField] TMP_Text badgePrimaryLabel;

    IRouteRuntimeHandle _handle;
    float _comboWindowReference = 1.2f;
    readonly char[] _cdTextBuffer = new char[UiCooldownSecondsFormatter.MaxChars];
    int _lastCdDisplayTenths = -1;
    Sprite _lastIcon;
    string _lastDisplayName;
    int _lastPresentationRevision = -1;
    SkillPresentationState _lastPresentationState;

    public void Bind(IRouteRuntimeHandle handle, float comboReferenceSeconds = 1.2f)
    {
        _handle = handle;
        _comboWindowReference = Mathf.Max(0.1f, comboReferenceSeconds);

        if (handle == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(handle.ShowOnHud);
        if (iconImage != null) iconImage.sprite = handle.Icon;
        if (keyLabel != null) keyLabel.text = handle.KeyLabel ?? string.Empty;
        if (displayNameLabel != null) displayNameLabel.text = handle.DisplayName ?? string.Empty;

        if (chargeBar != null) chargeBar.gameObject.SetActive(handle.HasChargeBar);
        if (comboGroup != null) comboGroup.SetActive(handle.HasComboOverlay);
        if (multiStageGroup != null) multiStageGroup.SetActive(handle.HasMultiStageOverlay);

        _lastIcon = null;
        _lastDisplayName = null;
        _lastPresentationRevision = -1;
        _lastPresentationState = SkillPresentationState.None;
        _lastCdDisplayTenths = -1;
        ApplyPresentationVisuals(handle, force: true);
        ApplyPresentationState(handle, force: true);
        ApplyCooldownAndMasks(handle);
    }

    public void Unbind()
    {
        HudBugProbe.NotifyWidgetUnbind(GetInstanceID());
        _handle = null;
        _lastCdDisplayTenths = -1;
        _lastIcon = null;
        _lastDisplayName = null;
        _lastPresentationRevision = -1;
        SetCooldownTimerVisible(false);
    }

    void LateUpdate()
    {
        if (_handle == null) return;

        if (_handle.PresentationRevision != _lastPresentationRevision)
        {
            _lastPresentationRevision = _handle.PresentationRevision;
            RefreshPresentation();
        }

        ApplyPresentationVisuals(_handle, force: false);
        ApplyPresentationState(_handle, force: false);
        ApplyCooldownAndMasks(_handle);

        if (_handle.HasChargeBar && chargeBar != null)
        {
            chargeBar.fillAmount = _handle.ChargeProgress01;
        }

        if (_handle.HasComboOverlay)
        {
            if (comboStepLabel != null)
                comboStepLabel.text = _handle.ComboStep >= 0 ? (_handle.ComboStep + 1).ToString() : "";
            if (comboWindowRing != null)
                comboWindowRing.fillAmount = _handle.ComboWindowRemainingSeconds / _comboWindowReference;
        }

        if (_handle.HasMultiStageOverlay)
        {
            if (multiStageIndexLabel != null)
                multiStageIndexLabel.text = _handle.MultiStageIndex >= 0 ? (_handle.MultiStageIndex + 1).ToString() : "";
            if (stageProgressBar != null)
                stageProgressBar.fillAmount = _handle.CurrentStageProgress01;
        }

        HudBugProbe.TickWidgetCd(
            GetInstanceID(),
            _handle,
            _lastCdDisplayTenths,
            cooldownTimerText != null && cooldownTimerText.gameObject.activeSelf,
            cooldownMask != null ? cooldownMask.fillAmount : -1f);
    }

    /// <summary>HudBug 扫描用 — 只读快照，不暴露 Handle 引用。</summary>
    public bool TryGetHudBugSnapshot(out HudBugProbe.WidgetSnapshot snapshot)
    {
        if (_handle == null)
        {
            snapshot = default;
            return false;
        }

        snapshot = new HudBugProbe.WidgetSnapshot(
            _handle.HudUnitKind,
            _handle.UnitId,
            _handle.PresentationId,
            _handle.HudIdentity,
            _handle.EntrySlot,
            _handle.KeyLabel,
            _handle.DisplayName,
            _handle.AssetFolder,
            _handle.AssetPath,
            true);
        return true;
    }

    void RefreshPresentation()
    {
        if (_handle == null)
        {
            return;
        }

        ApplyPresentationVisuals(_handle, force: true);
        ApplyPresentationState(_handle, force: true);
    }

    void ApplyPresentationVisuals(IRouteRuntimeHandle handle, bool force)
    {
        var icon = handle.Icon;
        if (force || icon != _lastIcon)
        {
            if (iconImage != null)
            {
                iconImage.sprite = icon;
            }

            _lastIcon = icon;
        }

        var name = handle.DisplayName;
        if (force || name != _lastDisplayName)
        {
            if (displayNameLabel != null)
            {
                displayNameLabel.text = name ?? string.Empty;
            }

            _lastDisplayName = name;
        }
    }

    void ApplyPresentationState(IRouteRuntimeHandle handle, bool force)
    {
        var state = handle.PresentationState;
        if (!force && state == _lastPresentationState)
        {
            return;
        }

        _lastPresentationState = state;

        if (multiStagePendingRing != null)
        {
            var showPending = (state & SkillPresentationState.MultiStagePending) != 0;
            if (multiStagePendingRing.enabled != showPending)
            {
                multiStagePendingRing.enabled = showPending;
            }

            if (showPending && handle.HasMultiStageOverlay)
            {
                multiStagePendingRing.fillAmount = 1f;
            }
        }

        var badge = handle.PrimaryBadge;
        if (badgePrimaryRoot != null)
        {
            var showBadge = badge.IsVisible;
            if (badgePrimaryRoot.activeSelf != showBadge)
            {
                badgePrimaryRoot.SetActive(showBadge);
            }
        }

        if (badgePrimaryLabel != null && badge.IsVisible)
        {
            badgePrimaryLabel.text = !string.IsNullOrEmpty(badge.Text)
                ? badge.Text
                : badge.Number >= 0
                    ? badge.Number.ToString()
                    : string.Empty;
        }
    }

    void ApplyCooldownAndMasks(IRouteRuntimeHandle handle)
    {
        var cdProgress = handle.CdProgress01;
        var cdMaskFill = 1f - cdProgress;
        var highlightFill = cdProgress;

        if (cooldownMask != null)
        {
            cooldownMask.fillAmount = cdMaskFill;
        }

        if (highlightBorder != null)
        {
            var suppressHighlight = handle.IsHighlightSuppressed;
            if (highlightBorder.enabled != !suppressHighlight)
            {
                highlightBorder.enabled = !suppressHighlight;
            }

            if (!suppressHighlight)
            {
                highlightBorder.fillAmount = highlightFill;
            }
        }

        if (resourceInsufficientMask != null)
        {
            var showResourceBlock = !handle.IsOnCooldown && !handle.HasEnoughResources;
            if (resourceInsufficientMask.enabled != showResourceBlock)
            {
                resourceInsufficientMask.enabled = showResourceBlock;
            }
        }

        ApplyCooldownTimerText(handle);
    }

    void ApplyCooldownTimerText(IRouteRuntimeHandle handle)
    {
        if (cooldownTimerText == null)
        {
            return;
        }

        if (!handle.IsOnCooldown)
        {
            if (_lastCdDisplayTenths != 0)
            {
                _lastCdDisplayTenths = 0;
                SetCooldownTimerVisible(false);
            }

            return;
        }

        var tenths = UiCooldownSecondsFormatter.ToDisplayTenths(handle.CdRemainingSeconds);
        if (tenths <= 0)
        {
            if (_lastCdDisplayTenths != 0)
            {
                _lastCdDisplayTenths = 0;
                SetCooldownTimerVisible(false);
            }

            return;
        }

        if (!cooldownTimerText.gameObject.activeSelf)
        {
            SetCooldownTimerVisible(true);
        }

        if (tenths == _lastCdDisplayTenths)
        {
            return;
        }

        _lastCdDisplayTenths = tenths;
        var len = UiCooldownSecondsFormatter.WriteTenths(tenths, _cdTextBuffer);
        if (len > 0)
        {
            cooldownTimerText.SetText(_cdTextBuffer, 0, len);
        }
    }

    void SetCooldownTimerVisible(bool visible)
    {
        if (cooldownTimerText == null)
        {
            return;
        }

        var go = cooldownTimerText.gameObject;
        if (go.activeSelf != visible)
        {
            go.SetActive(visible);
        }
    }
}

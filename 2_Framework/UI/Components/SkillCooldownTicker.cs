using UnityEngine;

/// <summary>
/// 单槽技能 CD 与可用性刷新：挂在带 <see cref="SkillSlotView"/> 的预制体上，
/// 高频逻辑在本组件 Update，避免 SkillBarPresenter 集中轮询。
/// </summary>
[RequireComponent(typeof(SkillSlotView))]
[AddComponentMenu("GameMain/UI/Skill Cooldown Ticker")]
public sealed class SkillCooldownTicker : MonoBehaviour
{
    [SerializeField] SkillSlotType slotType;

    [Tooltip("可用性轮询周期（秒）。沉默/资源由低频检测。")]
    [SerializeField, Range(0.05f, 1f)] float availabilityPollInterval = 0.15f;

    SkillSlotView _view;
    Player _player;
    float _nextAvailabilityPollTime;
    float _lastCdProgress = -1f;
    int _lastLevel = int.MinValue;

    void Awake()
    {
        _view = GetComponent<SkillSlotView>();
    }

    /// <summary>由 <see cref="SkillBarPresenter"/> 在 Bind／Unbind 时调用。</summary>
    public void Configure(Player player, SkillSlotType slot, SkillSlotView viewOverride = null)
    {
        _player = player;
        slotType = slot;
        if (viewOverride != null)
        {
            _view = viewOverride;
        }

        _lastCdProgress = -1f;
        _nextAvailabilityPollTime = 0f;
        _lastLevel = int.MinValue;
    }

    void Update()
    {
        if (_player == null || _view == null)
        {
            return;
        }

        if (!_player.TryGetSkillRuntime(slotType, out var runtime) || runtime == null)
        {
            return;
        }

        if (runtime.Level != _lastLevel)
        {
            _view.SetLevel(runtime.Level);
            _lastLevel = runtime.Level;
        }

        // ── 高频：CD ──
        // 分母用 SkillRuntime.CdScaledCooldown（实际启动时记录的 CD 总长，含 CDR），
        // 而不是 GetScaledCooldown()（基础 CD），否则 CDR 介入后 progress 会从 0.4 起跳。
        // RechargeTimerMax 同理，给多充能技能用。
        float progress01;
        float remaining;
        float maxCd;
        if (runtime.Data != null && runtime.Data.maxCharges > 1)
        {
            remaining = Mathf.Max(0f, runtime.RechargeTimer);
            maxCd = runtime.RechargeTimerMax;
        }
        else
        {
            remaining = Mathf.Max(0f, runtime.CdRemaining);
            maxCd = runtime.CdScaledCooldown;
        }

        if (remaining <= 0.0001f || maxCd <= 0.0001f)
        {
            progress01 = 1f;
            remaining = 0f;
        }
        else
        {
            progress01 = Mathf.Clamp01(1f - remaining / maxCd);
        }

        if (Mathf.Abs(_lastCdProgress - progress01) > 1e-4f)
        {
            _view.SetCooldownProgress(progress01);
            _lastCdProgress = progress01;
        }

        _view.SetCooldownRemaining(remaining);

        // ── 低频：可用性 ──
        var now = Time.time;
        if (now < _nextAvailabilityPollTime)
        {
            return;
        }

        _nextAvailabilityPollTime = now + availabilityPollInterval;
        var statSet = _player.Stats;
        var resources = _player.Resources;
        ref var tags = ref _player.Tags;
        var canCast = runtime.CanCast(statSet, resources, ref tags, _player);
        _view.SetAvailable(canCast);
    }

    void OnDisable()
    {
        Configure(null, slotType);
    }
}

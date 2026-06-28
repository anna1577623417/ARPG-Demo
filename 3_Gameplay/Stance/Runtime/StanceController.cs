using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 177.1 W0+W2 — Stance 运行时控制器（Player 组件）。
/// <para>核心职责：</para>
/// <para>  · 持有当前 Stance + 待切 Stance + 切换进度</para>
/// <para>  · TryRequestStance：检查 BlockedAbilities / 头顶障碍 / 切换冷却</para>
/// <para>  · Tick：推进 Enter/Exit Action 与 HeightScale 渐变</para>
/// <para>  · 暴露 LocomotionProfile / Tuning 给 PlayerLocomotionState 读取（间接化）</para>
/// </summary>
public sealed class StanceController : MonoBehaviour
{
    [SerializeField, Tooltip("初始 Stance 定义（必填）。")]
    StanceDefinitionSO m_normalStance;

    [SerializeField, Tooltip("可切换的全部 Stance 池（默认 Stance 应包含在内）。")]
    StanceDefinitionSO[] m_stances = Array.Empty<StanceDefinitionSO>();

    [SerializeField, Tooltip("最小切换间隔（防止抖动）。")]
    float m_minSwitchIntervalSec = 0.15f;

    StanceDefinitionSO _current;
    StanceDefinitionSO _pending;
    float _transitionT;
    float _transitionDuration;
    float _lastSwitchTime = -1f;
    float _heightScale = 1f;
    readonly Dictionary<StanceId, StanceDefinitionSO> _byId = new();

    /// <summary>切换事件 (from, to, reason)。</summary>
    public event Action<StanceDefinitionSO, StanceDefinitionSO, StanceTransitionReason> StanceChanged;

    public StanceDefinitionSO Current => _current;
    public StanceDefinitionSO Pending => _pending;
    public StanceId CurrentId => _current != null ? _current.Id : StanceId.Normal;
    public bool IsTransitioning => _pending != null;
    public float TransitionProgress => _transitionDuration > 0f ? Mathf.Clamp01(_transitionT / _transitionDuration) : 0f;
    public float HeightScale => _heightScale;

    /// <summary>当前 Stance 的 LocomotionProfile（间接化访问点；PlayerLocomotionState 可由此读）。</summary>
    public LocomotionProfile CurrentLocomotionProfile => _current != null ? _current.LocomotionProfile : null;

    void Awake()
    {
        RebuildIndex();
        _current = m_normalStance;
        if (_current != null) _heightScale = _current.HeightScale;
    }

    void OnValidate()
    {
        RebuildIndex();
    }

    void RebuildIndex()
    {
        _byId.Clear();
        if (m_stances == null) return;
        for (var i = 0; i < m_stances.Length; i++)
        {
            var s = m_stances[i];
            if (s == null) continue;
            _byId[s.Id] = s;
        }
        if (m_normalStance != null && !_byId.ContainsKey(m_normalStance.Id))
        {
            _byId[m_normalStance.Id] = m_normalStance;
        }
    }

    /// <summary>编辑器 Hook —— 注入额外 Stance（Editor 工厂菜单用）。</summary>
    public void EditorRegister(StanceDefinitionSO def)
    {
        if (def == null) return;
        _byId[def.Id] = def;
    }

    /// <summary>解析 Id → 定义。</summary>
    public bool TryGet(StanceId id, out StanceDefinitionSO def) => _byId.TryGetValue(id, out def);

    /// <summary>检查指定 Ability 名是否被当前 Stance 屏蔽。</summary>
    public bool IsAbilityBlocked(string abilityName)
    {
        if (_current == null || string.IsNullOrEmpty(abilityName)) return false;
        var blocked = _current.BlockedAbilityNames;
        if (blocked == null) return false;
        for (var i = 0; i < blocked.Length; i++)
        {
            if (blocked[i] == abilityName) return true;
        }
        return false;
    }

    /// <summary>请求切换 Stance。返回 true 表示请求被接受（切换可能仍在过渡中）。</summary>
    public bool TryRequestStance(StanceId target, StanceTransitionReason reason, out string rejectReason)
    {
        rejectReason = null;

        if (_current != null && _current.Id == target && _pending == null)
        {
            rejectReason = "already-in-target";
            return false;
        }

        if (Time.time - _lastSwitchTime < m_minSwitchIntervalSec)
        {
            rejectReason = "cooldown";
            return false;
        }

        if (!_byId.TryGetValue(target, out var def) || def == null)
        {
            rejectReason = "no-definition";
            return false;
        }

        // 头顶障碍检测（仅退出 Crouch 等需要时）
        if (_current != null && _current.RequireHeadClearanceOnExit && !CheckHeadClearance(_current))
        {
            rejectReason = "head-blocked";
            StanceChanged?.Invoke(_current, _current, StanceTransitionReason.RejectedHeadblock);
            return false;
        }

        BeginTransition(def, reason);
        return true;
    }

    void BeginTransition(StanceDefinitionSO target, StanceTransitionReason reason)
    {
        var from = _current;
        _pending = target;
        _transitionT = 0f;
        _transitionDuration = Mathf.Max(0.0001f,
            (target.EnterAction != null ? GetActionDuration(target.EnterAction) : 0f)
            + (from != null && from.ExitAction != null ? GetActionDuration(from.ExitAction) : 0f));
        _lastSwitchTime = Time.time;
        StanceChanged?.Invoke(from, target, reason);
    }

    /// <summary>每帧由 Player 调用。推进过渡进度 + 完成时切换 _current。</summary>
    public void Tick(float deltaTime)
    {
        if (_pending == null) return;

        _transitionT += deltaTime;

        // 高度渐变（简单 Lerp）
        var startScale = _current != null ? _current.HeightScale : 1f;
        var endScale = _pending.HeightScale;
        _heightScale = Mathf.Lerp(startScale, endScale, TransitionProgress);

        if (_transitionT >= _transitionDuration)
        {
            CompleteTransition();
        }
    }

    void CompleteTransition()
    {
        _current = _pending;
        _pending = null;
        _transitionT = 0f;
        _transitionDuration = 0f;
        if (_current != null) _heightScale = _current.HeightScale;
    }

    static float GetActionDuration(ActionDataSO action)
    {
        if (action == null) return 0f;
        // Action.Duration 在项目里通常为 baseDurationSeconds，做最小可用兜底
        return 0.25f;
    }

    /// <summary>头顶 SphereCast。返回 true = 头顶净空。</summary>
    bool CheckHeadClearance(StanceDefinitionSO from)
    {
        if (from == null) return true;
        var origin = transform.position + Vector3.up * from.HeadClearanceProbeHeight;
        var hits = Physics.SphereCast(
            origin,
            from.HeadClearanceProbeRadius,
            Vector3.up,
            out _,
            0.1f,
            from.HeadClearanceMask,
            QueryTriggerInteraction.Ignore);
        return !hits;
    }

    // ─── 175.2 跳跃系统兼容 ───────────────────────────────────────

    /// <summary>暴露给 175.2 JumpContext.CurrentStance —— Resolver 可按 Stance 字节匹配。</summary>
    public byte JumpContextStanceByte => _current != null ? _current.JumpContextStanceByte : (byte)0;

    /// <summary>Crouch + Sprint + Space → LongJump（175.2 §2.1）；StanceController 提供 CrouchHeld 判定。</summary>
    public bool IsCrouchHeld => _current != null && _current.Id == StanceId.Crouch;
}

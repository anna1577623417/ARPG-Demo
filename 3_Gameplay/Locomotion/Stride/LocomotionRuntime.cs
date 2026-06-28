using System;
using UnityEngine;

/// <summary>
/// 179.1 W1+W4+W5+W6 — Stride Matching 运行时核心（POCO，可独立测试）。
/// <para>纯逻辑：吃 Motor.HorizontalSpeed → 平滑 → 计算 ratio → 写 AnimDriver / 累积 FootPhase / 发事件。</para>
/// <para>外部 Tick：Player 每帧调用 <see cref="Tick"/>；进入/退出 Loop 调用 <see cref="OnEnterLoop"/> / <see cref="OnExitLoop"/>。</para>
/// </summary>
public sealed class LocomotionRuntime
{
    readonly LocomotionRuntimeProfileSO _config;
    readonly IStrideAnimDriver _anim;
    readonly IStrideMotor _motor;

    ActionDataSO _currentLoop;
    float _smoothedSpeed;
    float _playbackSpeed = 1f;
    float _footPhase;
    LoopTier _currentTier = LoopTier.Walk;

    /// <summary>每次计算 ratio 后广播；接 FootIK / 178.1 PhaseMatch / 音效。</summary>
    public event Action<StrideUpdatedEvent> StrideUpdated;

    /// <summary>触发 Tier 切换请求；PlayerLocomotionState 订阅后选择具体 Loop Action。</summary>
    public event Action<LocomotionTierSwitchEvent> TierSwitchRequested;

    public LocomotionRuntime(
        LocomotionRuntimeProfileSO config,
        IStrideAnimDriver anim,
        IStrideMotor motor)
    {
        _config = config;
        _anim = anim;
        _motor = motor;
    }

    public ActionDataSO CurrentLoop => _currentLoop;
    public float SmoothedSpeed => _smoothedSpeed;
    public float PlaybackSpeed => _playbackSpeed;
    public float FootPhase => _footPhase;
    public LoopTier CurrentTier => _currentTier;
    public bool IsActive => _currentLoop != null;

    /// <summary>进入 Loop 时调用。MotionDriven Action 不应进入此方法。</summary>
    public void OnEnterLoop(ActionDataSO loop, LoopTier tier)
    {
        _currentLoop = loop;
        _currentTier = tier;
        // 不重置 _smoothedSpeed —— 跨 Loop 切换保持速度连续性
    }

    /// <summary>退出 Loop（切到 Discrete / 死亡 / 空中）。</summary>
    public void OnExitLoop()
    {
        _currentLoop = null;
        _playbackSpeed = 1f;
        _anim?.SetPlaybackSpeed(1f);
    }

    /// <summary>每帧推进。</summary>
    public void Tick(float deltaTime)
    {
        if (_currentLoop == null) return;
        if (_currentLoop.MovementMode != MovementMode.MotorDriven) return;
        if (!_currentLoop.EnableStrideMatching)
        {
            // 关闭 Stride 时锁 1.0
            _playbackSpeed = 1f;
            _anim?.SetPlaybackSpeed(1f);
            return;
        }

        // ① 平滑真实速度（指数 EMA）
        var target = _motor != null ? _motor.HorizontalSpeed : 0f;
        var tau = _config != null ? Mathf.Max(0.001f, _config.SpeedSmoothingTau) : 0.08f;
        var k = 1f - Mathf.Exp(-deltaTime / tau);
        _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, target, k);

        // ② 计算 ratio
        var refSpeed = _currentLoop.ReferenceSpeed;
        if (refSpeed < 0.01f)
        {
            // 烘焙缺失 fallback
            _playbackSpeed = 1f;
            _anim?.SetPlaybackSpeed(1f);
            return;
        }
        var rawRatio = _smoothedSpeed / refSpeed;

        // ③ Tier 自动切换（179.1 §6）
        if (_currentLoop.AutoTierSwitch)
        {
            var nextTier = TierResolver.Resolve(_smoothedSpeed, _currentTier, _config);
            if (nextTier != _currentTier)
            {
                var from = _currentTier;
                _currentTier = nextTier;
                TierSwitchRequested?.Invoke(new LocomotionTierSwitchEvent
                {
                    From = from,
                    To = nextTier,
                    CurrentSpeed = _smoothedSpeed,
                });
                return;
            }

            // 超出 Min/Max 但 Tier 已是顶/底档：钳制（不再升档）
        }

        // ④ 应用到 Playable
        var ratio = Mathf.Clamp(rawRatio, _currentLoop.MinPlaybackSpeed, _currentLoop.MaxPlaybackSpeed);
        _playbackSpeed = ratio;
        _anim?.SetPlaybackSpeed(ratio);

        // ⑤ Foot Phase 累积
        if (_config == null || _config.EnableFootPhase)
        {
            var refDur = _currentLoop.ReferenceDuration;
            if (refDur > 0.001f)
            {
                var stepHz = 1f / refDur;
                _footPhase += stepHz * ratio * deltaTime;
                _footPhase -= Mathf.Floor(_footPhase);
            }
        }

        // ⑥ 广播
        StrideUpdated?.Invoke(new StrideUpdatedEvent
        {
            Loop = _currentLoop,
            SmoothedSpeed = _smoothedSpeed,
            Ratio = ratio,
            FootPhase = _footPhase,
            Tier = _currentTier,
        });
    }

    /// <summary>显式重置（场景切换 / 死亡复活）。</summary>
    public void Reset()
    {
        _currentLoop = null;
        _smoothedSpeed = 0f;
        _playbackSpeed = 1f;
        _footPhase = 0f;
        _currentTier = LoopTier.Walk;
    }
}

using UnityEngine;

/// <summary>
/// Motion 运行时执行器（时间意图 -> 期望速度）。
/// Why: 将动作时钟与位移数学聚合在独立模块，状态机只负责生命周期与窗口判定。
/// </summary>
public sealed class MotionExecutor
{
    private const float AnimSpeedLerp = 0.15f;
    private const float AnimSpeedMin = 0.7f;
    private const float AnimSpeedMax = 1.3f;

    private readonly IMotorAdapter _motor;
    private readonly IAnimSpeedControl _animSpeed;
    private readonly IStatsProvider _stats;

    private MotionProfileSO _profile;
    private float _baseDuration;
    private float _elapsed;
    private float _motionScale;
    private Vector3 _startPos;
    private Vector3 _direction;
    private Vector3 _lastPos;
    private float _smoothedAnimSpeed = 1f;
    private bool _active;

    private MotionPlaybackContext _playback;

    /// <summary>
    /// ActionData.AnimSpeed 作为整段动作的基础倍率（v4.5）：finalClipSpeed = baseAnimSpeed × profileFactor。
    /// 由 PlayerActionState.OnEnter 在 Begin 时传入；profileFactor 来自 MotionProfile 的 AnimSpeedMode。
    /// </summary>
    private float _baseAnimSpeed = 1f;

    public MotionExecutor(IMotorAdapter motor, IAnimSpeedControl animSpeed, IStatsProvider stats)
    {
        _motor = motor;
        _animSpeed = animSpeed;
        _stats = stats;
    }

    public bool IsActive => _active;

    public float NormalizedTime => _baseDuration > 0.0001f ? Mathf.Clamp01(_elapsed / _baseDuration) : 0f;

    /// <summary>本帧 Tick 前写入；蓄力等逻辑可冻结时间轴并覆盖主 Clip 速率。</summary>
    public void SetPlaybackContext(in MotionPlaybackContext ctx) => _playback = ctx;

    public void Begin(MotionProfileSO profile, float baseDuration, Vector3 direction, Vector3 startPos, float baseAnimSpeed = 1f)
    {
        _playback = default;
        _profile = profile;
        _baseDuration = Mathf.Max(0.0001f, baseDuration);
        _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        _elapsed = 0f;
        _startPos = startPos;
        _lastPos = startPos;
        // 步幅匹配的"平滑起点"用 baseAnimSpeed，而非 1.0；防止第一帧从 1.0 → ratio 跳变。
        _smoothedAnimSpeed = Mathf.Max(0.01f, baseAnimSpeed);
        _baseAnimSpeed = Mathf.Max(0.01f, baseAnimSpeed);
        _active = profile != null;
        _motionScale = _active && _stats != null ? Mathf.Max(0f, _stats.GetMotionScale(profile.ScaleType)) : 1f;
    }

    public void Tick(float deltaTime, float timeScale, Vector3 currentPosition)
    {
        if (!_active || _profile == null || deltaTime <= 0.0001f)
        {
            return;
        }

        var dtScale = _playback.FreezeNormalizedAdvance ? 0f : deltaTime * Mathf.Max(0f, timeScale);
        _elapsed += dtScale;

        // LoopWindow（蓝图 C2 / Phase 3）：t 落入循环段后，越过 end 时把 _elapsed 卷回 start，
        // 并把"等价位移"叠到 _startPos —— 这样平面位置在 wrap 边界不会回跳（_lastPos 不需要外部改写）。
        ApplyLoopWindowIfNeeded();

        var t = Mathf.Clamp01(_elapsed / _baseDuration);

        var displacementRatio = _profile.SampleDisplacement(t);
        var lateralRatio = _profile.SampleLateral(t);

        var forwardDistance = _profile.BaseDistance * _motionScale * Mathf.Max(0f, _profile.PeakSpeedMultiplier);
        var lateralDistance = _profile.LateralDistance * _motionScale;

        var forwardOffset = _direction * forwardDistance * displacementRatio;
        var lateralDir = Vector3.Cross(Vector3.up, _direction);
        var lateralOffset = lateralDir * lateralDistance * lateralRatio;
        var warpOffset = _direction * _profile.SampleWarp(t);

        var targetPos = _startPos + forwardOffset + lateralOffset + warpOffset;
        var delta = targetPos - _lastPos;

        // DefaultPhysics：模具只在水平面内展开（forward/lateral/warp 均沿水平 _direction），
        // 但差分速度会把「目标高度锁在起手 Y」与「_lastPos 因重力下落」写成 delta.y>0 → 虚假向上速度，
        // 与马达重力积分对冲（用户 VertAuthority：eatenRatio=1、[Y-driven] 与纯重力帧交替抖动）。
        // 垂直分量交给 PlayerKCCMotor 的 vy / Solver，与 v3.1.1 前「平面 Motion + 独立重力」一致。
        if (_profile.GravityBehavior == MotionGravityBehavior.DefaultPhysics)
        {
            delta.y = 0f;
        }

        var desiredVelocity = delta / deltaTime;
        _motor?.SetDesiredVelocity(desiredVelocity);

        // ═══ v4.5 三层组合公式 ═══════════════════════════════════════════════
        //   finalClipSpeed = ActionData.AnimSpeed (=_baseAnimSpeed)
        //                  × profileFactor (来自 MotionProfile.AnimSpeedMode)
        //
        //   Constant     → factor = 1.0          （ActionData.AnimSpeed 单独生效）
        //   Curve        → factor = SpeedOverTime.Evaluate(t)
        //   StrideMatch  → factor = clamp(actualSpeed / ReferenceSpeed, 0.7, 1.3)
        //                                         （平滑后；防滑步）
        // ────────────────────────────────────────────────────────────────────
        float profileFactor;
        switch (_profile.AnimSpeedMode)
        {
            case AnimSpeedMode.StrideMatch:
                if (_profile.ReferenceSpeed > 0.001f)
                {
                    var actualSpeed = _motor != null ? _motor.GetActualSpeed() : 0f;
                    var raw = actualSpeed / _profile.ReferenceSpeed;
                    // 注意：钳位 / 平滑作用在"profileFactor"上，而不是 final speed，
                    // 这样 ActionData.AnimSpeed 的基础倍率不会被 0.7~1.3 上下限误吃。
                    var smoothedFactor = Mathf.Lerp(_smoothedAnimSpeed / Mathf.Max(0.01f, _baseAnimSpeed),
                                                    raw, AnimSpeedLerp);
                    smoothedFactor = Mathf.Clamp(smoothedFactor, AnimSpeedMin, AnimSpeedMax);
                    profileFactor = smoothedFactor;
                }
                else
                {
                    profileFactor = 1f;   // ReferenceSpeed=0 视作"未配置"，退化为 Constant
                }
                break;

            case AnimSpeedMode.Curve:
                profileFactor = _profile.SampleAnimSpeed(t);
                break;

            case AnimSpeedMode.Constant:
            default:
                profileFactor = 1f;
                break;
        }

        var finalSpeed = _baseAnimSpeed * Mathf.Max(0f, profileFactor);
        if (_playback.HasAnimatorSpeedOverride)
        {
            finalSpeed = Mathf.Max(0f, _playback.AnimatorSpeedOverride);
        }

        _smoothedAnimSpeed = finalSpeed;   // 留给下一帧平滑参考
        _animSpeed?.SetSpeed(finalSpeed);

        // 注意：_lastPos 必须由外部在马达执行后回写真实位置；
        // 这里不能直接写 currentPosition（它是本帧物理前位置）。
    }

    /// <summary>
    /// 回写马达执行后的真实位置，供下一帧位移差分使用。
    /// Why: 若误用物理前位置，会让 desiredVelocity 持续偏大/抖动，破坏 MotionProfile 手感与时空一致性。
    /// </summary>
    public void SyncPostMotorPosition(Vector3 position)
    {
        _lastPos = position;
    }

    /// <summary>
    /// 外部离散瞬移后平移整条 Motion 求解坐标系，使连续位移与 Teleport 可叠加。
    /// Why: <c>targetPos = _startPos + profileOffset</c>；若只改 Transform 而不同步 <c>_startPos</c>/<c>_lastPos</c>，
    /// 下一帧 <c>(targetPos - _lastPos)/dt</c> 会得到反向速度把角色拽回。
    /// </summary>
    public void ApplyTeleportOffset(Vector3 worldOffset)
    {
        if (!_active || worldOffset.sqrMagnitude < 1e-12f)
        {
            return;
        }

        _startPos += worldOffset;
        _lastPos += worldOffset;
    }

    public void End()
    {
        _playback = default;
        _active = false;
        _motor?.SetDesiredVelocity(Vector3.zero);
        // 复位到 1.0：动作结束后下一段动画（Locomotion / 下一动作）会自行 SetSpeed，本步只确保不残留 profile 倍率。
        _animSpeed?.SetSpeed(1f);
        _baseAnimSpeed = 1f;
        _smoothedAnimSpeed = 1f;
    }

    /// <summary>
    /// 子区间循环采样实现：当 <see cref="MotionPlaybackContext.HasLoopWindow"/>=true 且 <c>_elapsed</c> 越过
    /// <see cref="MotionPlaybackContext.LoopWindowEnd"/> 时，把 <c>_elapsed</c> 卷回
    /// <see cref="MotionPlaybackContext.LoopWindowStart"/>，同时把 <c>[start, end]</c> 段的等价平面位移叠到 <c>_startPos</c>，
    /// 保证 wrap 边界处位置连续；<c>_lastPos</c> 在 wrap 帧不需要外部改写，差分速度会随之自洽。
    /// </summary>
    private void ApplyLoopWindowIfNeeded()
    {
        if (!_playback.HasLoopWindow || _playback.FreezeNormalizedAdvance || _baseDuration <= 1e-4f || _profile == null)
        {
            return;
        }

        var ws = Mathf.Clamp01(_playback.LoopWindowStart);
        var we = Mathf.Clamp01(_playback.LoopWindowEnd);
        if (we - ws <= 1e-3f)
        {
            return;
        }

        var loopEndElapsed = we * _baseDuration;
        if (_elapsed <= loopEndElapsed)
        {
            return;
        }

        var loopStartElapsed = ws * _baseDuration;
        var span = loopEndElapsed - loopStartElapsed;
        if (span <= 1e-4f)
        {
            return;
        }

        // 把 _elapsed 卷回 start（多帧 dt 超过一整圈时通过取模归并）。
        var overshoot = _elapsed - loopEndElapsed;
        var wrapCount = Mathf.Max(1, Mathf.CeilToInt(overshoot / span));
        _elapsed = loopEndElapsed - span + (overshoot - (wrapCount - 1) * span);
        if (_elapsed < loopStartElapsed)
        {
            _elapsed = loopStartElapsed;
        }

        // 同步把 [start, end] 段的等价平面位移叠到 _startPos —— 否则 targetPos 会一次性回跳一段。
        var loopOffset = ComputeWindowDisplacement(ws, we);
        _startPos += wrapCount * loopOffset;
        _lastPos += wrapCount * loopOffset;
    }

    /// <summary>
    /// 计算归一化区间 <c>[a, b]</c> 内的等价平面位移（forward + lateral + warp 三分量合成）。<br/>
    /// 与 <see cref="Tick"/> 内每帧位移公式严格对偶，仅用于 LoopWindow 越界时的 _startPos 补偿。
    /// </summary>
    private Vector3 ComputeWindowDisplacement(float a, float b)
    {
        var forwardDistance = _profile.BaseDistance * _motionScale * Mathf.Max(0f, _profile.PeakSpeedMultiplier);
        var lateralDistance = _profile.LateralDistance * _motionScale;
        var lateralDir = Vector3.Cross(Vector3.up, _direction);

        var dispDelta = _profile.SampleDisplacement(b) - _profile.SampleDisplacement(a);
        var latDelta = _profile.SampleLateral(b) - _profile.SampleLateral(a);
        var warpDelta = _profile.SampleWarp(b) - _profile.SampleWarp(a);

        var offset = _direction * (forwardDistance * dispDelta + warpDelta)
                   + lateralDir * (lateralDistance * latDelta);
        if (_profile.GravityBehavior == MotionGravityBehavior.DefaultPhysics)
        {
            offset.y = 0f;
        }
        return offset;
    }
}

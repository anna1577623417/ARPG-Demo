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

    public void Begin(MotionProfileSO profile, float baseDuration, Vector3 direction, Vector3 startPos, float baseAnimSpeed = 1f)
    {
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

        _elapsed += deltaTime * Mathf.Max(0f, timeScale);
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
        _active = false;
        _motor?.SetDesiredVelocity(Vector3.zero);
        // 复位到 1.0：动作结束后下一段动画（Locomotion / 下一动作）会自行 SetSpeed，本步只确保不残留 profile 倍率。
        _animSpeed?.SetSpeed(1f);
        _baseAnimSpeed = 1f;
        _smoothedAnimSpeed = 1f;
    }
}

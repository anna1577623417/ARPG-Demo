using UnityEngine;

/// <summary>
/// 换人：dummy 仅承担 Follow 轨道中心；<b>LookAt</b> 仍指向新角色 Look 锚点，避免 Composer 无目标或纵深错误。
/// </summary>
public sealed class SwitchBlendCameraState : GameplayCameraStateBase
{
    private readonly GameplayCameraStateMachine _machine;
    private readonly ICameraGameplayContext _ctx;
    private readonly Transform _dummyPivot;
    private readonly Transform _realFollow;
    private readonly Transform _realLookAt;
    private readonly SwitchBlendSettings _settings;

    private Vector3 _positionSmoothVelocity;
    private float _elapsed;
    private float _fovAtEnter;
    private float _fovSmoothVelocity;
    private bool _capturedFov;

    public SwitchBlendCameraState(
        GameplayCameraStateMachine machine,
        ICameraGameplayContext ctx,
        Transform dummyPivot,
        Transform realFollow,
        Transform realLookAt,
        SwitchBlendSettings settings)
    {
        _machine = machine;
        _ctx = ctx;
        _dummyPivot = dummyPivot;
        _realFollow = realFollow;
        _realLookAt = realLookAt;
        _settings = settings;
    }

    public override void Enter()
    {
        _elapsed = 0f;
        _positionSmoothVelocity = Vector3.zero;
        _fovSmoothVelocity = 0f;

        var cam = _ctx.ActionCamera;
        if (cam == null || _dummyPivot == null || _realFollow == null)
        {
            GoFreeLook();
            return;
        }

        if (!cam.TryGetFollowWorldSamplePosition(out var startWorld))
        {
            startWorld = _realFollow.position;
        }

        _dummyPivot.SetPositionAndRotation(startWorld, _realFollow.rotation);

        _capturedFov = cam.TryGetLensFieldOfView(out _fovAtEnter);

        var look = ResolveLookAt(_realFollow, _realLookAt);
        cam.SetFollowAndLookAt(_dummyPivot, look);
    }

    public override void Tick(float deltaTime)
    {
        var cam = _ctx.ActionCamera;
        if (cam == null || _dummyPivot == null || _realFollow == null)
        {
            GoFreeLook();
            return;
        }

        _elapsed += deltaTime;

        var targetPos = _realFollow.position;
        _dummyPivot.position = Vector3.SmoothDamp(
            _dummyPivot.position,
            targetPos,
            ref _positionSmoothVelocity,
            Mathf.Max(0.0001f, _settings.positionSmoothTime),
            Mathf.Infinity,
            deltaTime);

        _dummyPivot.rotation = _realFollow.rotation;

        var look = ResolveLookAt(_realFollow, _realLookAt);
        cam.SetFollowAndLookAt(_dummyPivot, look);

        if (_settings.blendFieldOfView && _capturedFov)
        {
            var fovGoal = _fovAtEnter + _settings.fieldOfViewDelta;
            if (cam.TryGetLensFieldOfView(out var fovNow))
            {
                var next = Mathf.SmoothDamp(
                    fovNow,
                    fovGoal,
                    ref _fovSmoothVelocity,
                    Mathf.Max(0.0001f, _settings.fieldOfViewSmoothTime),
                    Mathf.Infinity,
                    deltaTime);
                cam.SetLensFieldOfView(next);
            }
        }

        var sq = (_dummyPivot.position - targetPos).sqrMagnitude;
        var epsilon = Mathf.Max(0.0001f, _settings.snapEpsilon);
        var snapped = sq <= epsilon * epsilon;
        var timedOut = _elapsed >= _settings.maxBlendDuration;
        var minOk = _elapsed >= _settings.minElapsedBeforeSnapExit;

        if (timedOut || (snapped && minOk))
        {
            GoFreeLook();
        }
    }

    private void GoFreeLook()
    {
        var cam = _ctx.ActionCamera;
        if (cam != null)
        {
            if (_settings.blendFieldOfView && _capturedFov)
            {
                cam.SetLensFieldOfView(_fovAtEnter);
            }

            if (_realFollow != null)
            {
                var look = ResolveLookAt(_realFollow, _realLookAt);
                cam.SetFollowAndLookAt(_realFollow, look);
            }
        }

        _machine.Switch(new FreeLookCameraState(_ctx));
    }

    /// <summary>
    /// Dummy 的 Follow 不是角色锚点，不能交给 <see cref="ActionCameraController.SetFollowAndLookAt"/> 的 null 推断；
    /// 显式 LookAt 缺失时从 <see cref="PlayerCharacter"/> 解析（与相机控制器一致）。
    /// </summary>
    private static Transform ResolveLookAt(Transform realFollow, Transform explicitLookAt)
    {
        if (explicitLookAt != null)
        {
            return explicitLookAt;
        }

        if (realFollow == null)
        {
            return null;
        }

        var pc = realFollow.GetComponentInParent<PlayerCharacter>() ??
                 realFollow.GetComponent<PlayerCharacter>();
        return pc != null ? pc.CameraLookAtOrFollow : realFollow;
    }
}

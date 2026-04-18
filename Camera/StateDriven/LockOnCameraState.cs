using UnityEngine;

/// <summary>
/// 硬锁：Follow 保持在角色 Follow 锚点，LookAt 指向敌人；Cinemachine FreeLook 下由 Composer 负责朝向，不再写 ForceYaw。
/// </summary>
public sealed class LockOnCameraState : GameplayCameraStateBase
{
    private readonly GameplayCameraStateMachine _machine;
    private readonly ICameraGameplayContext _ctx;
    private readonly Transform _lockTarget;

    public LockOnCameraState(GameplayCameraStateMachine machine, ICameraGameplayContext ctx, Transform lockTarget)
    {
        _machine = machine;
        _ctx = ctx;
        _lockTarget = lockTarget;
    }

    public override void Enter()
    {
        var cam = _ctx.ActionCamera;
        var player = _ctx.PlayerManager?.ActiveCharacter;
        if (cam == null || player == null)
        {
            _machine.Switch(new FreeLookCameraState(_ctx));
            return;
        }

        if (_lockTarget == null)
        {
            _machine.Switch(new FreeLookCameraState(_ctx));
            return;
        }

        cam.SetFollowAndLookAt(player.CameraFollowOrRoot, _lockTarget);
        if (!cam.IsFreeLookVirtualCamera)
        {
            cam.SetOrbitInputSuppressed(true);
        }
    }

    public override void Tick(float deltaTime)
    {
        var cam = _ctx.ActionCamera;
        var player = _ctx.PlayerManager?.ActiveCharacter;
        if (cam == null || player == null || _lockTarget == null)
        {
            _machine.Switch(new FreeLookCameraState(_ctx));
            return;
        }

        if (cam.IsFreeLookVirtualCamera)
        {
            cam.SetFollowAndLookAt(player.CameraFollowOrRoot, _lockTarget);
            return;
        }

        var from = player.transform.position;
        var to = _lockTarget.position;
        var dir = to - from;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
        {
            return;
        }

        var yaw = Quaternion.LookRotation(dir.normalized).eulerAngles.y;
        cam.ForceYaw(yaw);
    }

    public override void Exit()
    {
        _ctx.ActionCamera?.SetOrbitInputSuppressed(false);
    }
}

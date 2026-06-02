using UnityEngine;

/// <summary>
/// Motion 局部 XYZ 曲线映射到世界水平面的参考前向（136.3+ MotionSpace）。
/// 与移动用的 MovementIntent / 角色转身解耦，避免闪避被 Locomotion 转向污染。
/// </summary>
public static class MotionSpaceBasis
{
    public static Vector3 ResolvePlanarForward(
        Player player,
        IGameModeMovementContext movementContext,
        MotionSpace space)
    {
        switch (space)
        {
            case MotionSpace.CharacterForward:
                return Planarize(player != null ? player.Forward : Vector3.forward);
            case MotionSpace.WorldSpace:
                return Vector3.forward;
            case MotionSpace.LockTarget:
                return TryResolveLockTargetPlanarForward(player, out var lockFwd)
                    ? lockFwd
                    : Planarize(player != null ? player.Forward : Vector3.forward);
            case MotionSpace.CameraForward:
            default:
                return ResolveCameraPlanarForward(movementContext);
        }
    }

    static Vector3 ResolveCameraPlanarForward(IGameModeMovementContext movementContext)
    {
        if (movementContext != null)
        {
            if (!movementContext.IsCameraRelativeMovement)
            {
                return Vector3.forward;
            }

            return Planarize(movementContext.GetMovementReferenceRotation() * Vector3.forward);
        }

        var mainCam = Camera.main;
        if (mainCam == null)
        {
            return Vector3.forward;
        }

        var yaw = mainCam.transform.eulerAngles.y;
        return Planarize(Quaternion.Euler(0f, yaw, 0f) * Vector3.forward);
    }

    static bool TryResolveLockTargetPlanarForward(Player player, out Vector3 forward)
    {
        forward = Vector3.forward;
        if (player == null)
        {
            return false;
        }

        // OPEN(slice-lock-on): 锁敌系统接入后改为「玩家→目标」平面单位向量。
        return false;
    }

    static Vector3 Planarize(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }
}

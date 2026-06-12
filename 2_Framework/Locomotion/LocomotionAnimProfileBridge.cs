using UnityEngine;

/// <summary>
/// 159.2：LocomotionProfile ContinuousClip → <see cref="PlayerAnimController"/> 桥接。
/// Profile 优先；未命中时 AnimController 回落 <see cref="PlayerAnimManagerSO"/> 字符串键。
/// </summary>
public static class LocomotionAnimProfileBridge
{
    /// <summary>Idle / Walk / Run 连续循环：Profile Binding 优先。</summary>
    public static bool TryGetLoopContinuousClip(
        LocomotionProfile profile,
        LocomotionStateId stateId,
        out LocomotionStateBinding binding)
    {
        binding = default;
        if (profile == null || stateId == LocomotionStateId.None) return false;
        if (!profile.HasState(stateId)) return false;

        binding = profile.GetBinding(stateId);
        return binding.TryGetContinuousPresentation(out _, out _, out _, out _, out _);
    }

    /// <summary>Turn-In-Place：按 <see cref="TurnInfo"/> 查 TurnInPlaceDirected Binding 的 ContinuousClip。</summary>
    public static bool TryGetTurnContinuousClip(
        LocomotionProfile profile,
        in TurnInfo turnInfo,
        bool wantsRun,
        out LocomotionStateBinding binding)
    {
        binding = default;
        if (profile == null || !turnInfo.IsTurning) return false;
        if (!profile.HasState(LocomotionStateId.TurnInPlaceDirected)) return false;

        var turnDir = MapTurnInfo(in turnInfo);
        if (turnDir == TurnDirection4.None) return false;

        binding = profile.GetBinding(
            LocomotionStateId.TurnInPlaceDirected,
            StrafeDirection8.None,
            turnDir,
            wantsRun);

        return binding.TryGetContinuousPresentation(out _, out _, out _, out _, out _);
    }

    public static TurnDirection4 MapTurnInfo(in TurnInfo info)
    {
        if (!info.IsTurning) return TurnDirection4.None;
        switch (info.Type)
        {
            case TurnType.Turn90:
                return info.Direction < 0 ? TurnDirection4.Left90 : TurnDirection4.Right90;
            case TurnType.Turn180:
                return info.Direction < 0 ? TurnDirection4.Left180 : TurnDirection4.Right180;
            default:
                return TurnDirection4.None;
        }
    }

    /// <summary>StrafeLocomotion：按 8 向 + WantsRun 查 ContinuousClip。</summary>
    public static bool TryGetStrafeContinuousClip(
        LocomotionProfile profile,
        StrafeDirection8 strafeDir,
        bool wantsRun,
        out LocomotionStateBinding binding)
    {
        binding = default;
        if (profile == null || strafeDir == StrafeDirection8.None) return false;
        if (!profile.HasState(LocomotionStateId.StrafeLocomotion)) return false;

        binding = profile.GetBinding(
            LocomotionStateId.StrafeLocomotion,
            strafeDir,
            TurnDirection4.None,
            wantsRun);

        return binding.TryGetContinuousPresentation(out _, out _, out _, out _, out _);
    }
}

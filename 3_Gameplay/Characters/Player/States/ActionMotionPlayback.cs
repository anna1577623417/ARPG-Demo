using UnityEngine;



/// <summary>

/// Action 期 MotionExecutor 生命周期（208.3 L5）— 从 PlayerActionState 拆出。

/// 职责：MotionProfile 驱动 / Motor 写入 / Clip RootMotion 策略；不感知 Skill/Combo/Route。

/// </summary>

public sealed class ActionMotionPlayback

{

    MotionExecutor _executor;

    PlayerMotorAdapter _motorAdapter;

    PlayerMotionStatsProvider _statsProvider;

    Vector3 _motionStartForward = Vector3.forward;



    public bool UseMotionProfile { get; private set; }

    public MotionContribution LastContribution =>

        _executor != null ? _executor.LastContribution : MotionContribution.Inactive;



    public void ResetDriverFlags() => UseMotionProfile = false;



    public void SetUseMotionProfile(bool value) => UseMotionProfile = value;



    public void EnsurePlumbing(Player player)

    {

        if (_motorAdapter == null)

        {

            _motorAdapter = new PlayerMotorAdapter(player);

        }



        if (_statsProvider == null)

        {

            _statsProvider = new PlayerMotionStatsProvider(player);

        }



        if (_executor == null)

        {

            _executor = new MotionExecutor(

                _motorAdapter,

                new EventBusAnimSpeedControl(player),

                _statsProvider,

                player);

        }

    }



    public float ResolveActionDuration(Player player, ActionDataSO action, GameplayIntentKind kind)

    {

        var duration = MotionDurationResolver.Resolve(action, _statsProvider);

        if (kind != GameplayIntentKind.Move || player?.LocomotionProfile?.Tuning == null)

        {

            return duration;

        }



        return duration * player.LocomotionProfile.Tuning.StartActionDurationScale;

    }



    public Vector3 ResolveFacingDirection(

        Player player,

        MotionProfileSO profile,

        SkillGroupDefinition ownerGroup = null)

    {

        if (player == null)

        {

            return Vector3.forward;

        }



        var space = ownerGroup != null

            ? ownerGroup.ResolveMotionCurveBasis(profile)

            : profile != null ? profile.MotionSpace : MotionSpace.CharacterForward;



        return player.ResolveMotionPlanarForward(space);

    }



    public void SetBurstFaceDir(Vector3 burstFaceDir)

    {

        _motionStartForward = burstFaceDir.sqrMagnitude > 0.0001f

            ? burstFaceDir.normalized

            : Vector3.forward;

    }



    /// <summary>210.5 — Action Yaw（表现层）；与 MotionExecutor 位移解耦。</summary>

    public void ApplyActionYaw(Player player, ActionDataSO action, float normalizedTime)

    {

        if (player == null || action?.MotionProfile == null || !action.MotionProfile.UsesActionYaw)

        {

            return;

        }



        var profile = action.MotionProfile;

        var yawDegrees = profile.SampleActionYawDegrees(normalizedTime);

        var desiredForward = ActionYawResolver.ResolveForwardFromBurstForward(

            _motionStartForward,

            yawDegrees);

        var prev = player.LogicForward;

        player.SetLogicForwardFromMotion(

            desiredForward,

            RotationMode.None,

            Vector3.zero,

            "ActionYaw");

        ActionYawProbe.LogApply(profile.YawPolicy, normalizedTime, yawDegrees, prev, player.LogicForward);

    }



    /// <summary>164.1 L6：MotionProfile 程序化位移 vs Clip RootMotion 二选一。</summary>

    public void ApplyDriverPolicy(Player player, ActionDataSO action)

    {

        if (action == null)

        {

            UseMotionProfile = false;

            SetClipRootMotionForPlayer(player, false);

            return;

        }



        if (action.UseClipRootMotion)

        {

            UseMotionProfile = false;

            SetClipRootMotionForPlayer(player, true);

            if (LocomotionDebug.IsEnabled(player))

            {

                LocomotionDebug.Log(

                    player,

                    LocomotionDebug.CatResolve,

                    $"[Motion] driver=ClipRootMotion action={action.name}");

            }



            return;

        }



        SetClipRootMotionForPlayer(player, false);

        UseMotionProfile = action.MotionProfile != null;

        if (UseMotionProfile && LocomotionDebug.IsEnabled(player))

        {

            LocomotionDebug.Log(

                player,

                LocomotionDebug.CatResolve,

                $"[Motion] driver=MotionProfile action={action.name}");

        }

    }



    public void BeginSession(

        Player player,

        ActionDataSO action,

        float normalizedStart,

        in StopRuntimeContext stopCtx,

        Vector3 burstFaceDir,

        float motionDuration)

    {

        if (!UseMotionProfile || _executor == null || action == null)

        {

            return;

        }



        player.BeginActionMotorSession();

        if (ShouldSuspendMotorGravity(action.MotionProfile))

        {

            player.SuspendGravity();

        }



        var animSpeed = stopCtx.IsActive ? stopCtx.BaseAnimSpeed : action.ResolveEffectiveAnimSpeed();

        _motionStartForward = burstFaceDir.sqrMagnitude > 0.0001f

            ? burstFaceDir.normalized

            : player.LogicForward;

        _executor.Begin(

            action.MotionProfile,

            motionDuration,

            burstFaceDir,

            player.transform.position,

            baseAnimSpeed: animSpeed,

            startNormalizedTime: normalizedStart,

            action: action,

            stopContext: in stopCtx);

    }



    public void EndSession(Player player, ActionDataSO action)

    {

        if (!UseMotionProfile || _executor == null)

        {

            return;

        }



        _executor.End();

        if (action != null && action.MotionProfile != null

            && ShouldSuspendMotorGravity(action.MotionProfile))

        {

            player.ReleaseGravity();

        }



        player.EndActionMotorSession();

    }



    public bool HasActiveExecutor => UseMotionProfile && _executor != null;



    /// <summary>196.x swap 帧跳过 Tick，返回修正后的 nt（swap 时压回 0）。</summary>

    public float TickFrame(

        Player player,

        ActionDataSO action,

        float prevNormalizedTime,

        float normalizedTime,

        bool actionSwappedThisFrame,

        in StopRuntimeContext stopCtx,

        MotionPlaybackContext chargePlayback,

        bool hasChargePlayback)

    {

        ApplyActionYaw(player, action, normalizedTime);



        if (!UseMotionProfile || _executor == null)

        {

            return normalizedTime;

        }



        if (!actionSwappedThisFrame)

        {

            if (hasChargePlayback)

            {

                _executor.SetPlaybackContext(in chargePlayback);

            }



            _executor.SetStopContext(in stopCtx);

            var lockTargetForward = Vector3.forward;

            var hasLockTargetForward = action?.MotionProfile != null

                                       && action.MotionProfile.MotionSpace == MotionSpace.LockTarget

                                       && player.TryGetLockTargetPlanarForward(out lockTargetForward);

            _executor.Tick(

                Time.deltaTime,

                1f,

                player.transform.position,

                prevNormalizedTime,

                normalizedTime,

                lockTargetForward,

                hasLockTargetForward);

            _motorAdapter.ApplyToPlayer();

            _executor.SyncPostMotorPosition(player.transform.position);

            return normalizedTime;

        }



        return 0f;

    }



    public void RestartForSwap(

        Player player,

        ActionDataSO action,

        float normalizedStart,

        in StopRuntimeContext stopCtx,

        Vector3 burstFaceDir,

        float motionDuration)

    {

        if (!UseMotionProfile || _executor == null || action == null)

        {

            return;

        }



        _executor.End();

        if (ShouldSuspendMotorGravity(action.MotionProfile))

        {

            player.SuspendGravity();

        }



        BeginExecutorOnly(player, action, normalizedStart, in stopCtx, burstFaceDir, motionDuration);

    }



    void BeginExecutorOnly(

        Player player,

        ActionDataSO action,

        float normalizedStart,

        in StopRuntimeContext stopCtx,

        Vector3 burstFaceDir,

        float motionDuration)

    {

        if (!UseMotionProfile || _executor == null || action == null)

        {

            return;

        }



        var animSpeed = stopCtx.IsActive ? stopCtx.BaseAnimSpeed : action.ResolveEffectiveAnimSpeed();

        _motionStartForward = burstFaceDir.sqrMagnitude > 0.0001f

            ? burstFaceDir.normalized

            : player.LogicForward;

        _executor.Begin(

            action.MotionProfile,

            motionDuration,

            burstFaceDir,

            player.transform.position,

            baseAnimSpeed: animSpeed,

            startNormalizedTime: normalizedStart,

            action: action,

            stopContext: in stopCtx);

    }



    public static void SetClipRootMotionForPlayer(Player player, bool enabled)

    {

        if (player == null)

        {

            return;

        }



        var anim = player.GetComponent<EntityAnimController>();

        anim?.SetClipRootMotionEnabled(enabled);

    }



    public static bool ShouldSuspendMotorGravity(MotionProfileSO profile)

    {

        if (profile == null)

        {

            return false;

        }



        return profile.GetYAxisConfig().Gravity == GravityMode.SuspendGravity;

    }

}

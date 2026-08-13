using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 227.4 — JumpStart 垂直速度未积分 / Idle→RunStart 零位移凝固专项探针。
/// 只记录流程边沿、两个归一化采样点和一次异常判定，不逐帧刷屏。
/// </summary>
public static class LocomotionTransition227BugProbe
{
    public const string LogPrefix = "[Locomotion227Bug]";

    enum FlowKind : byte
    {
        None = 0,
        Jump = 1,
        RunStart = 2,
    }

    sealed class Session
    {
        public int SessionId;
        public FlowKind Kind;
        public ActionDataSO Action;
        public int StartFrame;
        public Vector3 StartPosition;
        public int SampleMask;
        public bool StallLogged;
        public bool FirstPositiveYLogged;
        public bool FirstMotorCommitLogged;
        public int LastMotorCommitFrame = -1;
        public int MotorCommitCountThisFrame;
        public string LastMotorCommitSource;
        public Vector3 LastObservedPosition;
        public int LastObservedFrame = -1;
    }

    static readonly Dictionary<int, Session> Sessions = new Dictionary<int, Session>();
    static int s_nextSessionId = 1;

    static bool IsEnabled => GameMainDebugSettings.LocomotionTransition227BugLog;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Sessions.Clear();
        s_nextSessionId = 1;
    }

    public static void BeginJumpFlow(Player player, bool consumedJumpIntent)
    {
        if (!IsEnabled || player == null) return;

        // JumpStart Action 结束后会第二次进入 Airborne（consumed=false）。沿用原 session，
        // 才能把冲量、Action 停滞、JumpLoop 发布和首个抬升放在同一条时间线上。
        var session = !consumedJumpIntent && TryGetSession(player, FlowKind.Jump, out var active)
            ? active
            : BeginSession(player, FlowKind.Jump, ResolveBindingAction(player, LocomotionStateId.JumpStart));
        Log(
            player,
            session,
            "JUMP_AIRBORNE_ENTER",
            $"consumedIntent={consumedJumpIntent} grounded={player.IsGrounded} " +
            $"y={player.transform.position.y:F3} vy={player.VerticalSpeed:F3} " +
            $"jumpStartAction={SafeAction(session.Action)}");
    }

    public static void LogJumpImpulseApplied(Player player, float yBefore, float vyBefore)
    {
        if (!TryGetSession(player, FlowKind.Jump, out var session)) return;

        Log(
            player,
            session,
            "JUMP_IMPULSE_APPLIED",
            $"yBefore={yBefore:F3} yAfter={player.transform.position.y:F3} " +
            $"vyBefore={vyBefore:F3} vyAfter={player.VerticalSpeed:F3} groundedAfter={player.IsGrounded}");
    }

    public static void LogJumpAirPhase(Player player, string reason)
    {
        if (!TryGetSession(player, FlowKind.Jump, out var session)) return;

        Log(
            player,
            session,
            "JUMP_LOOP_PHASE_PUBLISHED",
            $"reason={Safe(reason)} grounded={player.IsGrounded} y={player.transform.position.y:F3} " +
            $"vy={player.VerticalSpeed:F3} framesFromStart={Time.frameCount - session.StartFrame}");
    }

    public static void LogJumpStartPhaseExit(
        Player player,
        ActionDataSO action,
        float normalizedTime,
        string reason)
    {
        if (!TryGetSession(player, FlowKind.Jump, out var session)) return;

        Log(
            player,
            session,
            "JUMP_START_PHASE_EXIT",
            $"action={SafeAction(action)} reason={Safe(reason)} nt={normalizedTime:F3} " +
            $"grounded={player.IsGrounded} y={player.transform.position.y:F3} " +
            $"vy={player.VerticalSpeed:F3} planarSpeed={player.PlanarVelocity.magnitude:F3}");
    }

    public static void LogLandingRoute(
        Player player,
        ActionDataSO landAction,
        float fallHeight,
        string source)
    {
        if (!TryGetSession(player, FlowKind.Jump, out var session)) return;

        Log(
            player,
            session,
            "JUMP_LANDING_ROUTE",
            $"landAction={SafeAction(landAction)} fallHeight={fallHeight:F3} source={Safe(source)} " +
            $"grounded={player.IsGrounded} vy={player.VerticalSpeed:F3}");
        Sessions.Remove(player.GetInstanceID());
    }

    public static void BeginRunStartFlow(Player player, ActionDataSO action)
    {
        if (!IsEnabled || player == null || ClassifyAction(player, action) != FlowKind.RunStart) return;

        var session = BeginSession(player, FlowKind.RunStart, action);
        Log(
            player,
            session,
            "RUN_START_FIRE",
            $"action={SafeAction(action)} input={player.HasMovementIntent} wantsRun={player.WantsRun} " +
            $"planarSpeed={player.PlanarVelocity.magnitude:F3} {DescribeMotion(action)}");
    }

    public static void LogStartActionBypassed(
        Player player,
        LocomotionStateId state,
        ActionDataSO action,
        string reason)
    {
        if (!IsEnabled || player == null) return;

        var kind = state == LocomotionStateId.RunStart ? FlowKind.RunStart : FlowKind.None;
        var session = kind != FlowKind.None
            ? GetOrBeginSession(player, kind, action)
            : BeginSession(player, FlowKind.None, action);
        Log(
            player,
            session,
            "START_ACTION_BYPASSED",
            $"locomotionState={state} action={SafeAction(action)} reason={Safe(reason)} " +
            $"input={player.HasMovementIntent} wantsRun={player.WantsRun} " +
            $"planarSpeedBefore={player.PlanarVelocity.magnitude:F3} classification=REALTIME_KCC_START");
        // Bypass 不会进入 ActionState，也就没有 TRACKED_ACTION_EXIT；必须当场关闭，
        // 否则旧 RunStart session 会污染后续 Action 的位置审计。
        Sessions.Remove(player.GetInstanceID());
    }

    public static void LogTrackedActionEnter(
        Player player,
        ActionDataSO action,
        GameplayIntentKind intentKind,
        float runtimeDuration,
        bool hasActiveExecutor,
        ActionMotionExecutionPlan driverPlan,
        uint leaseVersion)
    {
        if (!IsEnabled || player == null) return;

        var kind = ClassifyAction(player, action);
        if (kind == FlowKind.None) return;

        var session = GetOrBeginSession(player, kind, action);
        session.Action = action;
        session.StartPosition = player.transform.position;
        session.StartFrame = Time.frameCount;
        session.SampleMask = 0;
        session.StallLogged = false;
        session.FirstPositiveYLogged = false;
        session.FirstMotorCommitLogged = false;
        session.LastMotorCommitFrame = -1;
        session.MotorCommitCountThisFrame = 0;
        session.LastMotorCommitSource = "none";
        session.LastObservedPosition = player.transform.position;
        session.LastObservedFrame = Time.frameCount;

        Log(
            player,
            session,
            "TRACKED_ACTION_ENTER",
            $"intent={intentKind} action={SafeAction(action)} lease={leaseVersion} duration={runtimeDuration:F3} " +
            $"executor={hasActiveExecutor} grounded={player.IsGrounded} y={player.transform.position.y:F3} " +
             $"vy={player.VerticalSpeed:F3} planarSpeed={player.PlanarVelocity.magnitude:F3} " +
             $"recoveryMoveLock={action.RecoveryMoveLockSeconds:F3} resolvedDriver=({driverPlan}) " +
             $"{DescribeMotion(action)}");
    }

    /// <summary>每个被追踪 Action frame 的 Motor 提交审计；只记录首个提交与异常双提交。</summary>
    public static void LogMotorCommit(
        Player player,
        ActionDataSO action,
        ActionMotionExecutionPlan driverPlan,
        string source)
    {
        if (!IsEnabled || player == null || action == null) return;

        var kind = ClassifyAction(player, action);
        if (kind == FlowKind.None) return;

        var session = GetOrBeginSession(player, kind, action);
        if (session.LastMotorCommitFrame != Time.frameCount)
        {
            session.LastMotorCommitFrame = Time.frameCount;
            session.MotorCommitCountThisFrame = 0;
        }

        session.MotorCommitCountThisFrame++;
        session.LastMotorCommitSource = source;

        if (session.MotorCommitCountThisFrame == 1 && !session.FirstMotorCommitLogged)
        {
            session.FirstMotorCommitLogged = true;
            Log(
                player,
                session,
                "MOTOR_COMMIT_FIRST",
                $"action={SafeAction(action)} motorCommitSource={Safe(source)} motorCommitCount=1 " +
                $"resolvedDriver=({driverPlan})");
        }
        else if (session.MotorCommitCountThisFrame > 1)
        {
            Log(
                player,
                session,
                "MOTOR_COMMIT_DUPLICATE",
                $"action={SafeAction(action)} motorCommitSource={Safe(source)} " +
                $"motorCommitCount={session.MotorCommitCountThisFrame} resolvedDriver=({driverPlan}) " +
                "classification=CODE_SIDE_DOUBLE_MOTOR_APPLY");
        }
    }

    /// <summary>
    /// RunStart/JumpStart Action 内的位置写回审计。用于覆盖“WASD 首按先进入 RunStart Action，
    /// 尚未回到常规 Locomotion 就已经发生瞬移”的窗口。
    /// </summary>
    public static void LogTrackedMotorPositionSettlement(
        Player player,
        string entry,
        Vector3 positionBefore,
        Vector3 plannedDelta,
        Vector3 solvedDelta,
        Vector3 positionAfter)
    {
        if (!IsEnabled
            || player == null
            || !(player.States?.Current is PlayerActionState)
            || !Sessions.TryGetValue(player.GetInstanceID(), out var session)
            || session.Kind == FlowKind.None)
        {
            return;
        }

        var appliedDelta = positionAfter - positionBefore;
        var preMotorDelta = session.LastObservedFrame >= 0
            ? positionBefore - session.LastObservedPosition
            : Vector3.zero;
        var residual = appliedDelta - solvedDelta;
        var plannedPlanar = new Vector2(plannedDelta.x, plannedDelta.z).magnitude;
        var appliedPlanar = new Vector2(appliedDelta.x, appliedDelta.z).magnitude;
        var preMotorPlanar = new Vector2(preMotorDelta.x, preMotorDelta.z).magnitude;
        var residualPlanar = new Vector2(residual.x, residual.z).magnitude;
        var allowedPlanar = Mathf.Max(0.75f, plannedPlanar * 4f + 0.1f);
        var nonFinite = !IsFinite(positionBefore) || !IsFinite(positionAfter) || !IsFinite(appliedDelta);

        if (nonFinite || appliedPlanar >= allowedPlanar || preMotorPlanar >= 0.75f || residualPlanar >= 0.25f)
        {
            var classification = nonFinite
                ? "NON_FINITE_POSITION"
                : preMotorPlanar >= 0.75f
                    ? "POSITION_WRITE_BEFORE_MOTOR"
                    : appliedPlanar >= allowedPlanar
                        ? "ACTION_MOTOR_ABNORMAL_DISPLACEMENT"
                        : "ACTION_MOTOR_POST_SOLVER_CORRECTION_EXCESSIVE";
            Log(
                player,
                session,
                "TRACKED_ACTION_POSITION_ANOMALY",
                $"action={SafeAction(session.Action)} entry={Safe(entry)} " +
                $"lastObserved={Format(session.LastObservedPosition)} positionBefore={Format(positionBefore)} " +
                $"preMotorDelta={Format(preMotorDelta)} plannedDelta={Format(plannedDelta)} " +
                $"solvedDelta={Format(solvedDelta)} appliedDelta={Format(appliedDelta)} " +
                $"residual={Format(residual)} positionAfter={Format(positionAfter)} " +
                $"allowedPlanar={allowedPlanar:F4} classification={classification}");
        }

        session.LastObservedPosition = positionAfter;
        session.LastObservedFrame = Time.frameCount;
    }

    public static void ObserveTrackedAction(
        Player player,
        ActionDataSO action,
        float normalizedTime,
        bool hasActiveExecutor,
        ActionMotionExecutionPlan driverPlan)
    {
        if (!IsEnabled || player == null || action == null) return;

        var kind = ClassifyAction(player, action);
        if (kind == FlowKind.None) return;

        var session = GetOrBeginSession(player, kind, action);
        var frames = Time.frameCount - session.StartFrame;
        var delta = player.transform.position - session.StartPosition;
        var planarDelta = new Vector2(delta.x, delta.z).magnitude;
        var observedStep = player.transform.position - session.LastObservedPosition;
        var observedPlanarStep = new Vector2(observedStep.x, observedStep.z).magnitude;
        if (session.LastObservedFrame >= 0
            && Time.frameCount > session.LastObservedFrame
            && (!IsFinite(player.transform.position)
                || observedPlanarStep >= 0.75f
                || Mathf.Abs(observedStep.y) >= 0.75f))
        {
            Log(
                player,
                session,
                "TRACKED_ACTION_POSITION_ANOMALY",
                $"action={SafeAction(action)} entry=ActionObserve lastObserved={Format(session.LastObservedPosition)} " +
                $"positionAfter={Format(player.transform.position)} observedStep={Format(observedStep)} " +
                $"framesSinceObserved={Time.frameCount - session.LastObservedFrame} " +
                "classification=ACTION_POSITION_CHANGED_WITHOUT_REPORTED_MOTOR");
        }
        session.LastObservedPosition = player.transform.position;
        session.LastObservedFrame = Time.frameCount;

        if (kind == FlowKind.Jump && !session.FirstPositiveYLogged && delta.y > 0.005f)
        {
            session.FirstPositiveYLogged = true;
            Log(
                player,
                session,
                "JUMP_FIRST_POSITIVE_Y",
                $"action={SafeAction(action)} frames={frames} nt={normalizedTime:F3} deltaY={delta.y:F4} " +
                $"motorCommitSource={Safe(session.LastMotorCommitSource)} " +
                $"motorCommitCount={ResolveCommitCountThisFrame(session)} resolvedDriver=({driverPlan})");
        }

        TryLogSample(player, session, action, normalizedTime, hasActiveExecutor, driverPlan, frames, planarDelta, delta.y, 0.10f, 1);
        TryLogSample(player, session, action, normalizedTime, hasActiveExecutor, driverPlan, frames, planarDelta, delta.y, 0.50f, 2);

        if (session.StallLogged || frames < 2) return;

        if (driverPlan.RequiresBaseMotorTick && session.LastMotorCommitFrame != Time.frameCount)
        {
            session.StallLogged = true;
            Log(
                player,
                session,
                "MOTOR_COMMIT_MISSING",
                $"action={SafeAction(action)} frames={frames} nt={normalizedTime:F3} " +
                $"motorCommitSource={Safe(session.LastMotorCommitSource)} motorCommitCount=0 " +
                $"resolvedDriver=({driverPlan}) classification=CODE_SIDE_NO_MOTOR_APPLY");
            return;
        }

        if (kind == FlowKind.Jump
            && !hasActiveExecutor
            && player.VerticalSpeed > 0.05f
            && Mathf.Abs(delta.y) < 0.005f)
        {
            session.StallLogged = true;
            Log(
                player,
                session,
                "JUMP_VERTICAL_PENDING_WITHOUT_MOTOR",
                $"action={SafeAction(action)} frames={frames} nt={normalizedTime:F3} " +
                $"vy={player.VerticalSpeed:F3} deltaY={delta.y:F4} executor={hasActiveExecutor} " +
                "classification=CODE_SIDE_NO_MOTOR_APPLY");
        }
        else if (kind == FlowKind.RunStart
                 && player.HasMovementIntent
                 && planarDelta < 0.005f
                 && player.PlanarVelocity.magnitude < 0.05f)
        {
            session.StallLogged = true;
            Log(
                player,
                session,
                "RUN_START_ZERO_DISPLACEMENT",
                $"action={SafeAction(action)} frames={frames} nt={normalizedTime:F3} " +
                $"planarDelta={planarDelta:F4} planarSpeed={player.PlanarVelocity.magnitude:F3} " +
                $"executor={hasActiveExecutor} {DescribeMotion(action)} " +
                "classification=CONFIG_OR_MOTION_OUTPUT_ZERO");
        }
    }

    public static void LogTrackedActionExit(Player player, ActionDataSO action, float elapsed, float duration)
    {
        if (!IsEnabled || player == null || action == null) return;

        var kind = ClassifyAction(player, action);
        if (kind == FlowKind.None || !TryGetSession(player, kind, out var session)) return;

        var delta = player.transform.position - session.StartPosition;
        Log(
            player,
            session,
            "TRACKED_ACTION_EXIT",
            $"action={SafeAction(action)} elapsed={elapsed:F3} duration={duration:F3} " +
            $"delta=({delta.x:F3},{delta.y:F3},{delta.z:F3}) grounded={player.IsGrounded} " +
            $"vy={player.VerticalSpeed:F3} planarSpeed={player.PlanarVelocity.magnitude:F3}");

        if (kind == FlowKind.RunStart)
        {
            Sessions.Remove(player.GetInstanceID());
        }
    }

    public static void LogPresentationRequest(
        Player player,
        string source,
        ActionDataSO action,
        AnimationClip toClip,
        string currentClip)
    {
        if (!IsEnabled || player == null) return;

        var kind = ClassifyAction(player, action);
        var sourceIsTracked = Contains(source, "Jump");
        if (kind == FlowKind.None && !sourceIsTracked) return;

        if (kind == FlowKind.None)
        {
            kind = FlowKind.Jump;
        }

        var session = GetOrBeginSession(player, kind, action);
        Log(
            player,
            session,
            "PRESENTATION_REQUEST",
            $"source={Safe(source)} action={SafeAction(action)} fromClip={Safe(currentClip)} " +
            $"toClip={SafeClip(toClip)} state={SafeState(player)} y={player.transform.position.y:F3} " +
            $"vy={player.VerticalSpeed:F3} planarSpeed={player.PlanarVelocity.magnitude:F3}");
    }

    static void TryLogSample(
        Player player,
        Session session,
        ActionDataSO action,
        float normalizedTime,
         bool hasActiveExecutor,
         ActionMotionExecutionPlan driverPlan,
         int frames,
        float planarDelta,
        float deltaY,
        float threshold,
        int bit)
    {
        if ((session.SampleMask & bit) != 0 || normalizedTime < threshold) return;
        session.SampleMask |= bit;

        Log(
            player,
            session,
            "TRACKED_ACTION_SAMPLE",
            $"action={SafeAction(action)} mark={threshold:F2} nt={normalizedTime:F3} frames={frames} " +
             $"executor={hasActiveExecutor} deltaY={deltaY:F4} planarDelta={planarDelta:F4} " +
             $"vy={player.VerticalSpeed:F3} planarSpeed={player.PlanarVelocity.magnitude:F3} " +
             $"motorCommitSource={Safe(session.LastMotorCommitSource)} " +
             $"motorCommitCount={ResolveCommitCountThisFrame(session)} resolvedDriver=({driverPlan})");
    }

    static Session BeginSession(Player player, FlowKind kind, ActionDataSO action)
    {
        var session = new Session
        {
            SessionId = s_nextSessionId++,
            Kind = kind,
            Action = action,
            StartFrame = Time.frameCount,
            StartPosition = player.transform.position,
            LastObservedPosition = player.transform.position,
            LastObservedFrame = Time.frameCount,
        };
        Sessions[player.GetInstanceID()] = session;
        return session;
    }

    static Session GetOrBeginSession(Player player, FlowKind kind, ActionDataSO action)
    {
        if (TryGetSession(player, kind, out var session)) return session;
        return BeginSession(player, kind, action);
    }

    static bool TryGetSession(Player player, FlowKind kind, out Session session)
    {
        session = null;
        return IsEnabled
               && player != null
               && Sessions.TryGetValue(player.GetInstanceID(), out session)
               && session.Kind == kind;
    }

    static FlowKind ClassifyAction(Player player, ActionDataSO action)
    {
        if (player == null || action == null) return FlowKind.None;
        if (ResolveBindingAction(player, LocomotionStateId.JumpStart) == action) return FlowKind.Jump;
        if (ResolveBindingAction(player, LocomotionStateId.RunStart) == action) return FlowKind.RunStart;
        return FlowKind.None;
    }

    static ActionDataSO ResolveBindingAction(Player player, LocomotionStateId state)
    {
        var profile = player != null ? player.LocomotionProfile : null;
        if (profile == null || !profile.HasState(state)) return null;
        return profile.GetBinding(state).ResolveLocomotionAction();
    }

    static string DescribeMotion(ActionDataSO action)
    {
        var profile = action != null ? action.MotionProfile : null;
        if (profile == null) return "motionProfile=null usesAxis=False sourceClip=null scales=(0.000,0.000,0.000)";

        var y = profile.GetYAxisConfig();
        return $"motionProfile={profile.name} usesAxis={profile.UsesAxisCurves} " +
               $"sourceClip={(profile.SourceClip != null ? profile.SourceClip.name : "null")} " +
               $"scales=({profile.AxisCurves.XScale:F3},{profile.AxisCurves.YScale:F3},{profile.AxisCurves.ZScale:F3}) " +
               $"yMotion={y.YMotion} gravity={y.Gravity} groundConstraint={y.GroundConstraint}";
    }

    static void Log(Player player, Session session, string eventName, string payload)
    {
        Debug.Log(
            $"{LogPrefix} {eventName} sourceDesign=227.4 sessionId={session.SessionId} " +
            $"instanceId={player.GetInstanceID()} frame={Time.frameCount} time={Time.time:F3} " +
            $"flow={session.Kind} {payload}",
            player);
    }

    static string SafeAction(ActionDataSO action) => action != null ? action.name : "null";
    static string SafeClip(AnimationClip clip) => clip != null ? clip.name : "null";
    static string SafeState(Player player) => player?.States?.Current?.StateId ?? "null";
    static string Safe(string value) => string.IsNullOrEmpty(value) ? "-" : value.Replace(' ', '_');
    static int ResolveCommitCountThisFrame(Session session) =>
        session.LastMotorCommitFrame == Time.frameCount ? session.MotorCommitCountThisFrame : 0;
    static bool Contains(string value, string token) =>
        !string.IsNullOrEmpty(value)
        && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static string Format(Vector3 value) => $"({value.x:F4},{value.y:F4},{value.z:F4})";
}

/// <summary>
/// 227.4 / 227.3 — 常规 Locomotion（WASD）位置结算专项探针。
/// 以一次 Locomotion LogicUpdate 为审计边界，把输入、速度、Motor 计划量、Solver 输出量、
/// Motor 最终写回量和下一帧入口位置串成同一条证据链。正常路径仅在输入边沿与 0.25 秒采样点输出，
/// 位置突变、Motor 重复/缺失、Motor 后二次写入则立即输出。
/// </summary>
public static class LocomotionPositionSettlement227Probe
{
    const string LogPrefix = "[Locomotion227Bug]";
    const float NormalSampleInterval = 0.25f;
    const float AbsolutePlanarJumpThreshold = 0.75f;
    const float ExternalPositionJumpThreshold = 0.75f;
    const float MotorPlanarMismatchThreshold = 0.25f;
    const float PostMotorWriteThreshold = 0.02f;

    sealed class Audit
    {
        public int AuditId;
        public int InputSessionId;
        public bool InputSessionActive;
        public Vector3 InputSessionStart;
        public float NextSampleTime;

        public bool FrameOpen;
        public int Frame;
        public Vector3 FrameStart;
        public bool HadMovementIntent;
        public Vector3 MovementIntent;
        public Vector3 PlanarVelocityBefore;
        public int MotorReportCount;
        public string MotorEntry;
        public Vector3 MotorStart;
        public Vector3 MotorPlannedDelta;
        public Vector3 MotorSolvedDelta;
        public Vector3 MotorEnd;

        public bool HasLastFrameEnd;
        public int LastFrameEndFrame;
        public Vector3 LastFrameEnd;
        public bool LastFrameHadMovementIntent;
        public int LastExplicitTeleportFrame = -1;
    }

    static readonly Dictionary<int, Audit> Audits = new Dictionary<int, Audit>();
    static int s_nextAuditId = 1;
    static int s_nextInputSessionId = 1;

    static bool IsEnabled => GameMainDebugSettings.LocomotionTransition227BugLog;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Audits.Clear();
        s_nextAuditId = 1;
        s_nextInputSessionId = 1;
    }

    public static void BeginLocomotionFrame(Player player)
    {
        if (!IsEnabled || player == null) return;

        var audit = GetOrCreate(player);
        var position = player.transform.position;
        var frame = Time.frameCount;
        var hasIntent = player.HasMovementIntent;

        if (audit.HasLastFrameEnd
            && audit.LastFrameEndFrame == frame - 1
            && (audit.LastFrameHadMovementIntent || hasIntent))
        {
            var externalDelta = position - audit.LastFrameEnd;
            if (!IsFinite(position)
                || PlanarMagnitude(externalDelta) >= ExternalPositionJumpThreshold
                || Mathf.Abs(externalDelta.y) >= ExternalPositionJumpThreshold)
            {
                Log(
                    player,
                    audit,
                    "POSITION_CHANGED_AFTER_LOCOMOTION",
                    $"previousEnd={Format(audit.LastFrameEnd)} currentBegin={Format(position)} " +
                    $"externalDelta={Format(externalDelta)} previousFrame={audit.LastFrameEndFrame} " +
                    $"explicitTeleportPreviousFrame={audit.LastExplicitTeleportFrame == frame - 1} " +
                    "classification=POSITION_WRITE_OUTSIDE_LOCOMOTION_AUDIT");
            }
        }

        if (hasIntent && !audit.InputSessionActive)
        {
            audit.InputSessionActive = true;
            audit.InputSessionId = s_nextInputSessionId++;
            audit.InputSessionStart = position;
            audit.NextSampleTime = Time.unscaledTime + NormalSampleInterval;
            Log(
                player,
                audit,
                "WASD_POSITION_SESSION_BEGIN",
                $"position={Format(position)} intent={Format(player.MovementIntent)} " +
                $"planarVelocity={Format(player.PlanarVelocity)} dt={Time.deltaTime:F5}");
        }

        audit.FrameOpen = true;
        audit.Frame = frame;
        audit.FrameStart = position;
        audit.HadMovementIntent = hasIntent;
        audit.MovementIntent = player.MovementIntent;
        audit.PlanarVelocityBefore = player.PlanarVelocity;
        audit.MotorReportCount = 0;
        audit.MotorEntry = "none";
        audit.MotorStart = position;
        audit.MotorPlannedDelta = Vector3.zero;
        audit.MotorSolvedDelta = Vector3.zero;
        audit.MotorEnd = position;
    }

    public static void ReportMotorSettlement(
        Player player,
        string entry,
        Vector3 positionBefore,
        Vector3 plannedDelta,
        Vector3 solvedDelta,
        Vector3 positionAfter)
    {
        if (!TryGetOpenAudit(player, out var audit)) return;

        audit.MotorReportCount++;
        audit.MotorEntry = entry;
        audit.MotorStart = positionBefore;
        audit.MotorPlannedDelta = plannedDelta;
        audit.MotorSolvedDelta = solvedDelta;
        audit.MotorEnd = positionAfter;

        var appliedDelta = positionAfter - positionBefore;
        var settlementResidual = appliedDelta - solvedDelta;
        var nonFinite = !IsFinite(positionBefore)
                        || !IsFinite(plannedDelta)
                        || !IsFinite(solvedDelta)
                        || !IsFinite(positionAfter);
        var planarMismatch = PlanarMagnitude(settlementResidual);

        if (audit.MotorReportCount > 1)
        {
            LogMotorSnapshot(
                player,
                audit,
                "POSITION_MOTOR_DUPLICATE",
                positionBefore,
                plannedDelta,
                solvedDelta,
                positionAfter,
                "classification=CODE_SIDE_DOUBLE_MOTOR_APPLY");
        }
        else if (nonFinite || planarMismatch >= MotorPlanarMismatchThreshold)
        {
            LogMotorSnapshot(
                player,
                audit,
                "POSITION_MOTOR_SETTLEMENT_ANOMALY",
                positionBefore,
                plannedDelta,
                solvedDelta,
                positionAfter,
                $"settlementResidual={Format(settlementResidual)} planarResidual={planarMismatch:F4} " +
                $"classification={(nonFinite ? "NON_FINITE_POSITION" : "MOTOR_POST_SOLVER_CORRECTION_EXCESSIVE")}");
        }
    }

    public static void EndLocomotionFrame(Player player)
    {
        if (!TryGetOpenAudit(player, out var audit)) return;

        var position = player.transform.position;
        var frameDelta = position - audit.FrameStart;
        var motorAppliedDelta = audit.MotorEnd - audit.MotorStart;
        var postMotorDelta = position - audit.MotorEnd;
        var expectedPlanar = Mathf.Max(
            PlanarMagnitude(audit.MotorPlannedDelta),
            PlanarMagnitude(audit.MotorSolvedDelta));
        expectedPlanar = Mathf.Max(
            expectedPlanar,
            audit.PlanarVelocityBefore.magnitude * Mathf.Max(Time.deltaTime, 0f));
        var allowedPlanar = Mathf.Max(AbsolutePlanarJumpThreshold, expectedPlanar * 4f + 0.1f);
        var planarFrameDelta = PlanarMagnitude(frameDelta);

        if (audit.MotorReportCount == 0)
        {
            Log(
                player,
                audit,
                "POSITION_MOTOR_MISSING",
                $"frameStart={Format(audit.FrameStart)} frameEnd={Format(position)} " +
                $"frameDelta={Format(frameDelta)} classification=CODE_SIDE_NO_MOTOR_APPLY");
        }
        else if (!IsFinite(position) || planarFrameDelta >= allowedPlanar)
        {
            LogFrameSnapshot(
                player,
                audit,
                "POSITION_FRAME_TELEPORT_CANDIDATE",
                position,
                frameDelta,
                motorAppliedDelta,
                postMotorDelta,
                $"allowedPlanar={allowedPlanar:F4} classification=" +
                $"{(audit.LastExplicitTeleportFrame == Time.frameCount ? "EXPLICIT_TELEPORT" : "ABNORMAL_FRAME_DISPLACEMENT")}");
        }
        else if (postMotorDelta.magnitude >= PostMotorWriteThreshold)
        {
            LogFrameSnapshot(
                player,
                audit,
                "POSITION_POST_MOTOR_WRITE",
                position,
                frameDelta,
                motorAppliedDelta,
                postMotorDelta,
                "classification=POSITION_WRITE_AFTER_MOTOR_RETURN");
        }

        if (audit.InputSessionActive
            && audit.HadMovementIntent
            && Time.unscaledTime >= audit.NextSampleTime)
        {
            audit.NextSampleTime = Time.unscaledTime + NormalSampleInterval;
            LogFrameSnapshot(
                player,
                audit,
                "WASD_POSITION_SAMPLE",
                position,
                frameDelta,
                motorAppliedDelta,
                postMotorDelta,
                $"allowedPlanar={allowedPlanar:F4} classification=NORMAL_PERIODIC_SAMPLE");
        }

        if (audit.InputSessionActive && !audit.HadMovementIntent)
        {
            var sessionDelta = position - audit.InputSessionStart;
            Log(
                player,
                audit,
                "WASD_POSITION_SESSION_END",
                $"position={Format(position)} sessionDelta={Format(sessionDelta)} " +
                $"planarDistance={PlanarMagnitude(sessionDelta):F4}");
            audit.InputSessionActive = false;
        }

        audit.FrameOpen = false;
        audit.HasLastFrameEnd = true;
        audit.LastFrameEndFrame = Time.frameCount;
        audit.LastFrameEnd = position;
        audit.LastFrameHadMovementIntent = audit.HadMovementIntent;
    }

    public static void LogExplicitTeleport(
        Player player,
        Vector3 requestedPosition,
        Vector3 positionBefore,
        Vector3 positionAfter,
        bool forceAirborne)
    {
        if (!IsEnabled || player == null) return;

        var audit = GetOrCreate(player);
        audit.LastExplicitTeleportFrame = Time.frameCount;
        Log(
            player,
            audit,
            "POSITION_EXPLICIT_TELEPORT",
            $"requested={Format(requestedPosition)} before={Format(positionBefore)} after={Format(positionAfter)} " +
            $"appliedDelta={Format(positionAfter - positionBefore)} forceAirborne={forceAirborne} " +
            "classification=INTENTIONAL_TELEPORT_API");
    }

    static Audit GetOrCreate(Player player)
    {
        var instanceId = player.GetInstanceID();
        if (Audits.TryGetValue(instanceId, out var audit)) return audit;

        audit = new Audit { AuditId = s_nextAuditId++ };
        Audits.Add(instanceId, audit);
        return audit;
    }

    static bool TryGetOpenAudit(Player player, out Audit audit)
    {
        audit = null;
        return IsEnabled
               && player != null
               && Audits.TryGetValue(player.GetInstanceID(), out audit)
               && audit.FrameOpen
               && audit.Frame == Time.frameCount;
    }

    static void LogMotorSnapshot(
        Player player,
        Audit audit,
        string eventName,
        Vector3 positionBefore,
        Vector3 plannedDelta,
        Vector3 solvedDelta,
        Vector3 positionAfter,
        string classification)
    {
        Log(
            player,
            audit,
            eventName,
            $"entry={Safe(audit.MotorEntry)} motorCount={audit.MotorReportCount} " +
            $"positionBefore={Format(positionBefore)} plannedDelta={Format(plannedDelta)} " +
            $"solvedDelta={Format(solvedDelta)} appliedDelta={Format(positionAfter - positionBefore)} " +
            $"positionAfter={Format(positionAfter)} {classification}");
    }

    static void LogFrameSnapshot(
        Player player,
        Audit audit,
        string eventName,
        Vector3 frameEnd,
        Vector3 frameDelta,
        Vector3 motorAppliedDelta,
        Vector3 postMotorDelta,
        string classification)
    {
        Log(
            player,
            audit,
            eventName,
            $"frameStart={Format(audit.FrameStart)} frameEnd={Format(frameEnd)} frameDelta={Format(frameDelta)} " +
            $"intent={Format(audit.MovementIntent)} planarVelocityBefore={Format(audit.PlanarVelocityBefore)} " +
            $"motorEntry={Safe(audit.MotorEntry)} motorCount={audit.MotorReportCount} " +
            $"plannedDelta={Format(audit.MotorPlannedDelta)} solvedDelta={Format(audit.MotorSolvedDelta)} " +
            $"motorAppliedDelta={Format(motorAppliedDelta)} postMotorDelta={Format(postMotorDelta)} " +
            $"dt={Time.deltaTime:F5} {classification}");
    }

    static void Log(Player player, Audit audit, string eventName, string payload)
    {
        Debug.Log(
            $"{LogPrefix} {eventName} sourceDesign=227.4 auditId={audit.AuditId} " +
            $"inputSessionId={audit.InputSessionId} instanceId={player.GetInstanceID()} " +
            $"frame={Time.frameCount} time={Time.time:F3} state={SafeState(player)} {payload}",
            player);
    }

    static float PlanarMagnitude(Vector3 value) => new Vector2(value.x, value.z).magnitude;
    static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static string Format(Vector3 value) => $"({value.x:F4},{value.y:F4},{value.z:F4})";
    static string SafeState(Player player) => player?.States?.Current?.StateId ?? "null";
    static string Safe(string value) => string.IsNullOrEmpty(value) ? "-" : value.Replace(' ', '_');
}

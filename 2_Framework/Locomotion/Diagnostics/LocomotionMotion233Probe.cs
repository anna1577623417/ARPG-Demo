using System.Text;
using UnityEngine;

/// <summary>
/// 233.5 — Walk/Run 点按、Start/Loop/End 与 MotionProfile 有效位移来源只读探针。
/// 可与 CharacterTurnDisplacement233Probe 同时开启；不写输入、状态、速度、位置、Action 或资产。
/// </summary>
public static class LocomotionMotion233Probe
{
    const string Prefix = "[LocomotionMotion233]";
    const float SampleInterval = 0.10f;
    const float PostReleaseWindow = 1.25f;
    const float MoveEpsilon = 0.0005f;

    static Player s_player;
    static int s_sessionId;
    static int s_eventId;
    static int s_tapId;
    static bool s_inputActive;
    static bool s_previousInputActive;
    static int s_sector = -1;
    static int s_previousSector = -1;
    static bool s_wantsRun;
    static bool s_tapStartedAsRun;
    static InputTense s_tense;
    static string s_context = "none";
    static float s_pressTime;
    static float s_releaseTime = -1f;
    static bool s_tapActive;
    static Vector3 s_tapStartPosition;
    static Vector3 s_tapLastPosition;
    static float s_tapPath;
    static int s_tapFrames;
    static int s_tapZeroFrames;
    static float s_nextSampleTime;
    static LocomotionStateId s_lastRequested;
    static LocomotionStateId s_lastResolved;
    static ActionDataSO s_lastResolvedAction;
    static LocomotionStateId s_lastSequenceState;
    static readonly StringBuilder s_sequence = new StringBuilder(96);

    static ActionDataSO s_action;
    static uint s_actionLease;
    static LocomotionStateId s_actionState;
    static Vector3 s_actionStartPosition;
    static Vector3 s_actionLastPosition;
    static float s_actionPath;
    static float s_executorPath;
    static float s_actionMinNonZero = float.PositiveInfinity;
    static float s_actionMaxFrame;
    static int s_actionFrames;
    static int s_actionZeroFrames;
    static int s_actionMotorFrames;
    static float s_actionStartTime;
    static float s_nextActionSampleTime;
    static Vector3 s_authoredDelta;
    static string s_effectiveSource = "none";
    static float s_effectiveExpectedDistance = -1f;

    static bool Enabled => GameMainDebugSettings.LocomotionMotion233Log;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_player = null;
        s_sessionId = 0;
        s_eventId = 0;
        s_tapId = 0;
        s_previousInputActive = false;
        s_previousSector = -1;
        s_tapActive = false;
        s_action = null;
        s_sequence.Length = 0;
    }

    public static void ObserveInput(
        Player player,
        Vector2 rawInput,
        Vector3 worldIntent,
        bool wantsRun,
        InputTense tense,
        string contextSource)
    {
        if (!Enabled || !EnsureSession(player)) return;

        var active = rawInput.sqrMagnitude > 0.0001f;
        var sector = ResolveSector(rawInput);
        var edge = active != s_previousInputActive || (active && sector != s_previousSector);
        s_inputActive = active;
        s_sector = sector;
        s_wantsRun = wantsRun;
        s_tense = tense;
        s_context = Safe(contextSource);

        if (edge)
        {
            if (!s_previousInputActive && active)
            {
                if (s_tapActive) EndTap(player, "next-press");
                s_tapId++;
                s_tapActive = true;
                s_pressTime = Time.unscaledTime;
                s_releaseTime = -1f;
                s_tapStartPosition = player.transform.position;
                s_tapLastPosition = s_tapStartPosition;
                s_tapPath = 0f;
                s_tapFrames = 0;
                s_tapZeroFrames = 0;
                s_tapStartedAsRun = wantsRun;
                s_sequence.Length = 0;
                s_lastSequenceState = LocomotionStateId.None;
                s_lastRequested = LocomotionStateId.None;
                s_lastResolved = LocomotionStateId.None;
                s_lastResolvedAction = null;
                s_nextSampleTime = Time.unscaledTime;
            }
            else if (s_previousInputActive && !active)
            {
                s_releaseTime = Time.unscaledTime;
            }

            Log(
                "INPUT_EDGE",
                $"tap={s_tapId} from={SectorName(s_previousSector)}/{s_previousInputActive} to={SectorName(sector)}/{active} " +
                $"heldMs={(active ? 0f : (Time.unscaledTime - s_pressTime) * 1000f):F0} run={wantsRun} tense={tense} " +
                $"raw={V2(rawInput)} worldYaw={F(Yaw(worldIntent))} ctx={s_context} speed={player.PlanarVelocity.magnitude:F3} pos={V3(player.transform.position)}",
                player);
        }

        s_previousInputActive = active;
        s_previousSector = sector;
    }

    public static void ObserveResolve(
        Player player,
        LocomotionStateId requested,
        in LocomotionDecision decision,
        bool hadInputBefore,
        bool hasInputNow)
    {
        if (!Enabled || !EnsureSession(player)) return;
        var action = decision.DiscreteAction != null ? decision.DiscreteAction : decision.LocomotionAction;
        var edge = requested != s_lastRequested || decision.ResolvedState != s_lastResolved || action != s_lastResolvedAction;
        if (!edge) return;

        AppendSequence(decision.ResolvedState);
        Log(
            "RESOLVE_EDGE",
            $"tap={s_tapId} requested={requested} resolved={decision.ResolvedState} policy={decision.ExecutionPolicy} " +
            $"input={hadInputBefore}>{hasInputNow} run={s_wantsRun} speed={player.PlanarVelocity.magnitude:F3} " +
            $"action={Name(action)} discrete={Name(decision.DiscreteAction)} continuous={Name(decision.LocomotionAction)} " +
            $"fallback={Safe(decision.FallbackReason)} downgraded={decision.DowngradedFromLogicLayer} " + AssetInline(player, action),
            player);
        s_lastRequested = requested;
        s_lastResolved = decision.ResolvedState;
        s_lastResolvedAction = action;
    }

    public static void ObserveStartBypassed(Player player, LocomotionStateId state, ActionDataSO action, string reason)
    {
        if (!Enabled || !EnsureSession(player)) return;
        Log(
            "START_BYPASS",
            $"tap={s_tapId} state={state} action={Name(action)} reason={Safe(reason)} " +
            $"effect=RealtimeKcc_NoActionTimeline_NoMotionProfileConsumption {AssetInline(player, action)}",
            player);
    }

    public static void ObserveLocomotionFrame(Player player, Vector3 before, Vector3 after)
    {
        if (!Enabled || !EnsureSession(player)) return;
        var delta = Planar(after - before).magnitude;
        if (!s_tapActive) return;

        s_tapFrames++;
        s_tapPath += delta;
        if (delta <= MoveEpsilon) s_tapZeroFrames++;
        s_tapLastPosition = after;
        if (Time.unscaledTime >= s_nextSampleTime)
        {
            s_nextSampleTime = Time.unscaledTime + SampleInterval;
            Log(
                "LOOP_SAMPLE",
                $"tap={s_tapId} resolved={s_lastResolved} input={SectorName(s_sector)}/{s_inputActive} run={s_wantsRun} " +
                $"speed={player.PlanarVelocity.magnitude:F3} frameMove={delta:F5} path={s_tapPath:F5} " +
                $"net={Planar(after - s_tapStartPosition).magnitude:F5} driver=RealtimeKccMotor",
                player);
        }

        if (!s_inputActive && s_releaseTime >= 0f && Time.unscaledTime - s_releaseTime >= PostReleaseWindow)
        {
            EndTap(player, "post-release-timeout");
        }
    }

    public static void ObserveActionEnter(
        Player player,
        ActionDataSO action,
        uint lease,
        float duration,
        float normalizedStart,
        ActionMotionExecutionPlan plan,
        in StopRuntimeContext stop)
    {
        if (!Enabled || !EnsureSession(player)) return;
        var state = ClassifyAction(player, action);
        if (!IsTracked(state)) return;

        s_action = action;
        s_actionLease = lease;
        s_actionState = state;
        s_actionStartPosition = player.transform.position;
        s_actionLastPosition = s_actionStartPosition;
        s_actionPath = 0f;
        s_executorPath = 0f;
        s_actionMinNonZero = float.PositiveInfinity;
        s_actionMaxFrame = 0f;
        s_actionFrames = 0;
        s_actionZeroFrames = 0;
        s_actionMotorFrames = 0;
        s_actionStartTime = Time.unscaledTime;
        s_nextActionSampleTime = Time.unscaledTime;
        var motionScale = ResolveMotionScale(player, action != null ? action.MotionProfile : null);
        s_authoredDelta = ResolveAuthoredDelta(action, motionScale);
        s_effectiveSource = ResolveEffectiveSource(plan, in stop);
        s_effectiveExpectedDistance = ResolveExpectedDistance(plan, in stop, s_authoredDelta);
        AppendSequence(state);

        Log(
            "ACTION_BEGIN",
            $"tap={s_tapId} phase={state} action={Name(action)} lease={lease} duration={duration:F3} startNt={normalizedStart:F3} " +
            $"entrySpeed={stop.EntrySpeed:F3} requestedDriver={plan.RequestedMode} effectiveDriver={plan.EffectiveMode} valid={plan.IsValid} " +
            $"effectiveSource={s_effectiveSource} expectedDistance={Expected(s_effectiveExpectedDistance)} " +
            $"stop={stop.IsActive}/{stop.Strategy} disable={stop.DisableStopMotion} authorFixed={stop.UseAuthorFixed} " +
            $"integrated={stop.UseIntegratedBrake} a={stop.BrakeDeceleration:F3} vRef={stop.ReferenceGaitSpeed:F2} tier={stop.SessionTier} " +
            $"runtimeDist={stop.RuntimeDistance:F4} runtimeDur={stop.RuntimeDuration:F4} mask={V3(stop.ApplyMask)} " + AssetInline(player, action),
            player);
    }

    public static void ObserveStopBaseline(
        Player player,
        ActionDataSO action,
        in StopRuntimeContext stop,
        bool clearedVelocity)
    {
        if (!Enabled || !EnsureSession(player) || action == null || !stop.IsActive)
        {
            return;
        }

        var walk = player.RuntimeStats.WalkSpeed;
        var run = player.RuntimeStats.RunSpeed;
        var snapshot = player.LastStopSessionSnapshot;
        var tPhys = StopIntegrator.PredictDuration(stop.EntrySpeed, stop.BrakeDeceleration);
        var vRatio = run > 0.01f ? stop.EntrySpeed / run : 0f;
        Log(
            "STOP_BASELINE",
            $"action={Name(action)} strategy={stop.Strategy} entrySpeed={stop.EntrySpeed:F3} " +
            $"integrateDist={stop.RuntimeDistance:F4} tPhys={tPhys:F4} tLease={stop.RuntimeDuration:F4} " +
            $"clearedVelocity={clearedVelocity} a={stop.BrakeDeceleration:F3} vRef={stop.ReferenceGaitSpeed:F2} " +
            $"walkSpeed={walk:F2} runSpeed={run:F2} vRunRatio={vRatio:F2} gaitUnchanged=true " +
            $"tier={stop.SessionTier} heldTicks={(snapshot.IsValid ? snapshot.HeldTicks : -1)} reachedLoop={(snapshot.IsValid && snapshot.ReachedLoop)} " +
            $"dRefFallback={stop.DerivedFromLegacyMaxDistance} physicsComplete={stop.PhysicsComplete}",
            player);
    }

    public static void ObserveActionFrame(
        Player player,
        ActionDataSO action,
        uint lease,
        float prevNt,
        float currNt,
        ActionMotionExecutionPlan plan,
        in StopRuntimeContext stop,
        MotionContribution contribution,
        Vector3 executorWorldDelta,
        bool appliedMotor,
        Vector3 before,
        Vector3 after)
    {
        if (!Enabled || action != s_action || lease != s_actionLease || !EnsureSession(player)) return;
        var actual = Planar(after - before).magnitude;
        var executor = Planar(executorWorldDelta).magnitude;
        s_actionFrames++;
        if (appliedMotor) s_actionMotorFrames++;
        if (actual <= MoveEpsilon) s_actionZeroFrames++;
        else s_actionMinNonZero = Mathf.Min(s_actionMinNonZero, actual);
        s_actionMaxFrame = Mathf.Max(s_actionMaxFrame, actual);
        s_actionPath += actual;
        s_executorPath += executor;
        s_actionLastPosition = after;
        if (s_tapActive)
        {
            s_tapFrames++;
            s_tapPath += actual;
            if (actual <= MoveEpsilon) s_tapZeroFrames++;
            s_tapLastPosition = after;
        }

        if (Time.unscaledTime < s_nextActionSampleTime) return;
        s_nextActionSampleTime = Time.unscaledTime + SampleInterval;
        Log(
            "ACTION_SAMPLE",
            $"tap={s_tapId} phase={s_actionState} lease={lease} nt={prevNt:F3}>{currNt:F3} source={s_effectiveSource} " +
            $"contribActive={contribution.IsActive} local={V3(contribution.LocalDelta)} executorWorld={V3(executorWorldDelta)} " +
            $"actual={V3(after - before)} executorPath={s_executorPath:F5} actualPath={s_actionPath:F5} " +
            $"motor={appliedMotor} speed={player.PlanarVelocity.magnitude:F3}",
            player);
    }

    public static void ObserveActionExit(
        Player player,
        ActionDataSO action,
        uint lease,
        ActionMotionExecutionPlan plan,
        in StopRuntimeContext stop)
    {
        if (!Enabled || action != s_action || lease != s_actionLease || !EnsureSession(player)) return;
        var net = Planar(s_actionLastPosition - s_actionStartPosition).magnitude;
        Log(
            "ACTION_SUMMARY",
            $"tap={s_tapId} phase={s_actionState} action={Name(action)} lease={lease} ms={(Time.unscaledTime - s_actionStartTime) * 1000f:F0} " +
            $"source={s_effectiveSource} authoredDelta={V3(s_authoredDelta)} expectedDistance={Expected(s_effectiveExpectedDistance)} " +
            $"executorPath={s_executorPath:F5} actualNet={net:F5} actualPath={s_actionPath:F5} " +
            $"minNonZero={MinNonZero()} maxFrame={s_actionMaxFrame:F5} zeroFrames={s_actionZeroFrames} " +
            $"frames={s_actionFrames} motorFrames={s_actionMotorFrames} endSpeed={player.PlanarVelocity.magnitude:F3} " +
            $"stop={stop.IsActive}/{stop.Strategy} runtimeDist={stop.RuntimeDistance:F4}",
            player);

        var endedEndPhase = s_actionState == LocomotionStateId.WalkEnd || s_actionState == LocomotionStateId.RunEnd;
        s_action = null;
        s_actionLease = 0;
        s_actionState = LocomotionStateId.None;
        if (endedEndPhase && s_tapActive) EndTap(player, "end-action-exit");
    }

    static bool EnsureSession(Player player)
    {
        if (player == null) return false;
        if (player == s_player) return true;
        s_player = player;
        s_sessionId++;
        s_eventId = 0;
        s_previousInputActive = false;
        s_previousSector = -1;
        s_tapActive = false;
        s_action = null;
        s_sequence.Length = 0;
        Log(
            "SESSION_BEGIN",
            $"format=compact-v1 stack=off sampleHz=10 player={player.name} profile={Name(player.LocomotionProfile)} " +
            $"coexist=CharacterTurn233:true purpose=Tap_StartLoopEnd_AssetEffectiveExecutorKccReconciliation",
            player);
        return true;
    }

    static void EndTap(Player player, string reason)
    {
        if (!s_tapActive || player == null) return;
        var net = Planar(s_tapLastPosition - s_tapStartPosition).magnitude;
        Log(
            "TAP_SUMMARY",
            $"tap={s_tapId} reason={reason} heldMs={(s_releaseTime >= 0f ? (s_releaseTime - s_pressTime) * 1000f : -1f):F0} " +
            $"mode={(s_tapStartedAsRun ? "Run" : "Walk")} sequence={Safe(s_sequence.ToString())} " +
            $"net={net:F5} path={s_tapPath:F5} frames={s_tapFrames} zeroFrames={s_tapZeroFrames} " +
            $"endSpeed={player.PlanarVelocity.magnitude:F3}",
            player);
        s_tapActive = false;
        s_releaseTime = -1f;
    }

    static string AssetInline(Player player, ActionDataSO action)
    {
        if (action == null) return "asset=none";
        var profile = action.MotionProfile;
        var scale = ResolveMotionScale(player, profile);
        var authored = ResolveAuthoredDelta(action, scale);
        var curves = profile != null ? profile.AxisCurves : default;
        return
            $"movement={action.MovementMode} transition={action.TransitionType} recovery={action.IsLocomotionRecovery} " +
            $"driverCfg={action.MotionDriverMode} stopCfg={action.EnableStopFeature}/{action.StopStrategy} " +
            $"dRef={action.InheritPhysics.FullSpeedStopDistance:F3} dRefFallback={action.InheritPhysics.MaxDistance:F3} " +
            $"vRefFallback={action.InheritPhysics.MaxSpeed:F2} " +
            $"mp={Name(profile)} mpId={(profile != null ? profile.GetInstanceID() : 0)} axes={(profile != null && profile.UsesAxisCurves)} " +
            $"stopAuthor={(profile != null && profile.EnableStopAuthoring)} scaleType={(profile != null ? profile.ScaleType.ToString() : "none")} " +
            $"motionScale={scale:F3} axisScale={V3(new Vector3(curves.XScale, curves.YScale, curves.ZScale))} authoredDelta={V3(authored)}";
    }

    static string ResolveEffectiveSource(ActionMotionExecutionPlan plan, in StopRuntimeContext stop)
    {
        if (!plan.IsValid) return "InvalidDriver_NoGuaranteedMotion";
        if (stop.IsActive)
        {
            if (stop.DisableStopMotion) return "StopSnap_ZeroMotion";
            if (stop.UseAuthorFixed) return "StopMotionProfile_AuthorMeters";
            return "StopInheritPhysics_RuntimeDistance_MPShapeOnly";
        }
        if (plan.EffectiveMode == ActionMotionDriverMode.MotionProfile) return "MotionProfile_AuthorMeters";
        if (plan.EffectiveMode == ActionMotionDriverMode.InheritStateMotor) return "RealtimeStateMotor_MPNotConsumed";
        if (plan.EffectiveMode == ActionMotionDriverMode.Stationary) return "Stationary_ZeroPlanar";
        if (plan.EffectiveMode == ActionMotionDriverMode.ClipRootMotion) return "ClipRootMotion_MPNotConsumed";
        return plan.EffectiveMode.ToString();
    }

    static float ResolveExpectedDistance(ActionMotionExecutionPlan plan, in StopRuntimeContext stop, Vector3 authored)
    {
        if (!plan.IsValid) return -1f;
        if (stop.IsActive)
        {
            if (stop.DisableStopMotion) return 0f;
            if (!stop.UseAuthorFixed) return stop.RuntimeDistance;
        }
        return Planar(authored).magnitude;
    }

    static Vector3 ResolveAuthoredDelta(ActionDataSO action, float scale)
    {
        var profile = action != null ? action.MotionProfile : null;
        if (profile == null || !profile.UsesAxisCurves) return Vector3.zero;
        return profile.AxisCurves.SampleLocalPosition(1f, scale) - profile.AxisCurves.SampleLocalPosition(0f, scale);
    }

    static float ResolveMotionScale(Player player, MotionProfileSO profile)
    {
        if (player == null || profile == null || profile.ScaleType == MotionScaleType.None) return 1f;
        if (profile.ScaleType == MotionScaleType.MoveSpeed)
        {
            return Mathf.Max(0.01f, player.RuntimeStats.RunSpeed / Mathf.Max(0.01f, player.BaseMoveSpeed));
        }
        return 1f;
    }

    static LocomotionStateId ClassifyAction(Player player, ActionDataSO action)
    {
        if (IsBinding(player, action, LocomotionStateId.WalkStart)) return LocomotionStateId.WalkStart;
        if (IsBinding(player, action, LocomotionStateId.WalkEnd)) return LocomotionStateId.WalkEnd;
        if (IsBinding(player, action, LocomotionStateId.RunStart)) return LocomotionStateId.RunStart;
        if (IsBinding(player, action, LocomotionStateId.RunEnd)) return LocomotionStateId.RunEnd;
        return LocomotionStateId.None;
    }

    static bool IsBinding(Player player, ActionDataSO action, LocomotionStateId state)
    {
        var profile = player != null ? player.LocomotionProfile : null;
        return profile != null && action != null && profile.HasState(state)
               && profile.GetBinding(state).ResolveLocomotionAction() == action;
    }

    static bool IsTracked(LocomotionStateId state) =>
        state == LocomotionStateId.WalkStart || state == LocomotionStateId.WalkEnd
        || state == LocomotionStateId.RunStart || state == LocomotionStateId.RunEnd;

    static void AppendSequence(LocomotionStateId state)
    {
        if (state == LocomotionStateId.None || state == s_lastSequenceState) return;
        if (s_sequence.Length > 0) s_sequence.Append('>');
        s_sequence.Append(state);
        s_lastSequenceState = state;
    }

    static int ResolveSector(Vector2 input)
    {
        if (input.sqrMagnitude <= 0.0001f) return -1;
        var angle = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;
        return Mathf.RoundToInt(angle / 45f) & 7;
    }

    static string SectorName(int sector)
    {
        switch (sector)
        {
            case 0: return "W";
            case 1: return "WD";
            case 2: return "D";
            case 3: return "SD";
            case 4: return "S";
            case 5: return "SA";
            case 6: return "A";
            case 7: return "WA";
            default: return "Idle";
        }
    }

    static Vector3 Planar(Vector3 value) => new Vector3(value.x, 0f, value.z);
    static float Yaw(Vector3 value) => value.sqrMagnitude > 0.0001f ? Mathf.Atan2(value.x, value.z) * Mathf.Rad2Deg : float.NaN;
    static string F(float value) => float.IsNaN(value) || float.IsInfinity(value) ? "NA" : value.ToString("F2");
    static string V2(Vector2 value) => $"({value.x:F2},{value.y:F2})";
    static string V3(Vector3 value) => $"({value.x:F4},{value.y:F4},{value.z:F4})";
    static string Safe(string value) => string.IsNullOrEmpty(value) ? "none" : value.Replace(' ', '_');
    static string Name(Object value) => value != null ? Safe(value.name) : "none";
    static string Expected(float value) => value >= 0f ? value.ToString("F5") : "NA";
    static string MinNonZero() => float.IsPositiveInfinity(s_actionMinNonZero) ? "NA" : s_actionMinNonZero.ToString("F5");

    static void Log(string eventName, string details, Object context)
    {
        var entityId = s_player != null ? s_player.GetInstanceID() : 0;
        var message = $"{Prefix} e={eventName} sid={s_sessionId} id={++s_eventId} entity={entityId} " +
                      $"f={Time.frameCount} t={Time.unscaledTime:F3} | {details}";
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, context, "{0}", message);
    }
}

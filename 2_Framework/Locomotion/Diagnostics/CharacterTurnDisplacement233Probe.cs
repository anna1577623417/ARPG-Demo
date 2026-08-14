using UnityEngine;

/// <summary>
/// 233.4 — 角色转向与过渡步态位移的只读运行时探针。
/// 独立于 CameraTurn233Probe；不写朝向、速度、位置、状态或动画参数。
/// </summary>
public static class CharacterTurnDisplacement233Probe
{
    enum Phase : byte
    {
        None,
        Idle,
        WalkLoop,
        RunLoop,
        WalkStart,
        RunStart,
        WalkEnd,
        RunEnd,
        OtherAction
    }

    const string Prefix = "[CharacterTurn233]";
    const float SampleInterval = 0.10f;
    const float CaptureWindow = 4.00f;
    const float MoveEpsilon = 0.0005f;
    const float AngleEpsilon = 0.05f;
    const float AlignedAngle = 5f;

    static Player s_player;
    static int s_sessionId;
    static int s_eventId;
    static Vector2 s_rawInput;
    static Vector3 s_worldIntent;
    static bool s_inputActive;
    static int s_inputSector = -1;
    static int s_previousInputSector = -1;
    static bool s_previousInputActive;
    static bool s_wantsRun;
    static InputTense s_inputTense;
    static string s_inputContext = "none";
    static float s_captureUntil;
    static float s_nextSampleTime;

    static float s_logicYaw = float.NaN;
    static float s_rootYaw = float.NaN;
    static float s_visualYaw = float.NaN;
    static float s_visualSpeed;
    static bool s_visualHeld;
    static TurnInfo s_turnInfo;
    static string s_logicSource = "none";

    static bool s_turnCaptureActive;
    static int s_turnCaptureId;
    static float s_turnStartTime;
    static float s_turnStartLogicYaw;
    static float s_turnStartRootYaw;
    static float s_turnStartVisualYaw;
    static float s_turnTargetYaw;
    static float s_turnLogicTravel;
    static float s_turnVisualTravel;
    static float s_turnMaxVisualLag;
    static float s_turnLogicAlignedTime = -1f;
    static float s_turnVisualAlignedTime = -1f;
    static bool s_turnLogicResponseLogged;
    static bool s_turnVisualResponseLogged;
    static bool s_turnRejectLogged;
    static string s_turnLogicSource = "none";

    static Phase s_phase;
    static ActionDataSO s_phaseAction;
    static uint s_phaseLease;
    static ActionMotionExecutionPlan s_phasePlan;
    static float s_phaseStartTime;
    static Vector3 s_phaseStartPosition;
    static Vector3 s_phaseLastPosition;
    static int s_phaseFrames;
    static int s_phaseZeroFrames;
    static int s_phaseMotorFrames;
    static float s_phasePath;
    static float s_phaseMinNonZero = float.PositiveInfinity;
    static float s_phaseMaxFrameDelta;
    static float s_phaseFirstMoveTime = -1f;
    static float s_lastFrameDelta;
    static float s_lastNormalizedTime;
    static bool s_lastFrameAppliedMotor;

    static bool Enabled => GameMainDebugSettings.CharacterTurnDisplacement233Log;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_player = null;
        s_sessionId = 0;
        s_eventId = 0;
        s_inputSector = -1;
        s_previousInputSector = -1;
        s_previousInputActive = false;
        s_captureUntil = 0f;
        s_turnCaptureActive = false;
        s_phase = Phase.None;
        s_phaseAction = null;
    }

    public static void ObserveInput(
        Player player,
        Vector2 rawInput,
        Vector3 worldIntent,
        bool wantsRun,
        InputTense inputTense,
        string contextSource)
    {
        if (!Enabled || !EnsureSession(player, player)) return;

        var active = rawInput.sqrMagnitude > 0.0001f;
        var sector = ResolveSector(rawInput);
        var edge = active != s_previousInputActive || (active && sector != s_previousInputSector);

        s_rawInput = rawInput;
        s_worldIntent = worldIntent;
        s_inputActive = active;
        s_inputSector = sector;
        s_wantsRun = wantsRun;
        s_inputTense = inputTense;
        s_inputContext = string.IsNullOrEmpty(contextSource) ? "none" : contextSource;

        if (edge)
        {
            EndTurnCapture(active ? "direction-edge" : "release");
            s_captureUntil = Time.unscaledTime + CaptureWindow;
            s_nextSampleTime = Time.unscaledTime;
            Log(
                "INPUT_EDGE",
                $"from={SectorName(s_previousInputSector)}/{s_previousInputActive} to={SectorName(sector)}/{active} " +
                $"raw={V2(rawInput)} world={V3(worldIntent)} targetYaw={F(Yaw(worldIntent))} " +
                $"run={wantsRun} tense={inputTense} ctx={s_inputContext} pos={V3(player.transform.position)} " +
                $"speed={player.PlanarVelocity.magnitude:F3}",
                player);

            if (active && worldIntent.sqrMagnitude > 0.0001f)
            {
                BeginTurnCapture(player, Yaw(worldIntent));
            }
        }

        s_previousInputActive = active;
        s_previousInputSector = sector;
    }

    public static void ObserveTurnInfo(Player player, in TurnInfo info)
    {
        if (!Enabled || !EnsureSession(player, player)) return;
        var edge = info.IsTurning != s_turnInfo.IsTurning
                   || info.Type != s_turnInfo.Type
                   || info.Direction != s_turnInfo.Direction;
        s_turnInfo = info;
        if (edge)
        {
            Log(
                "TURN_PRESENTATION_EDGE",
                $"turn={info.IsTurning} type={info.Type} dir={info.Direction} angle={info.SignedAngle:F2} " +
                $"input={SectorName(s_inputSector)} run={s_wantsRun} phase={s_phase}",
                player);
        }
    }

    public static void ObserveLogicForwardRejected(Player player, Vector3 requested, string source)
    {
        if (!Enabled || !EnsureSession(player, player) || !s_turnCaptureActive || s_turnRejectLogged) return;
        s_turnRejectLogged = true;
        Log(
            "LOGIC_REJECT",
            $"source={Safe(source)} requestedYaw={F(Yaw(requested))} targetYaw={F(s_turnTargetYaw)} " +
            $"logic={F(Yaw(player.LogicForward))} locked=true phase={s_phase}",
            player);
    }

    public static void ObserveLogicForwardWrite(
        Player player,
        Vector3 previous,
        Vector3 current,
        string source,
        float rootYawBefore,
        float rootYawAfter,
        float visualYawBefore,
        float visualYawAfter)
    {
        if (!Enabled || !EnsureSession(player, player)) return;

        var previousYaw = Yaw(previous);
        var currentYaw = Yaw(current);
        var delta = Mathf.Abs(Mathf.DeltaAngle(previousYaw, currentYaw));
        s_logicYaw = currentYaw;
        s_rootYaw = rootYawAfter;
        s_visualYaw = visualYawAfter;
        s_logicSource = Safe(source);

        if (!s_turnCaptureActive || delta < AngleEpsilon) return;
        s_turnLogicTravel += delta;
        var sourceEdge = !string.Equals(s_turnLogicSource, s_logicSource);
        if (!s_turnLogicResponseLogged || sourceEdge)
        {
            s_turnLogicResponseLogged = true;
            s_turnLogicSource = s_logicSource;
            Log(
                "LOGIC_RESPONSE",
                $"capture={s_turnCaptureId} source={s_logicSource} logic={F(previousYaw)}>{F(currentYaw)} d={delta:F2} " +
                $"root={F(rootYawBefore)}>{F(rootYawAfter)} visual={F(visualYawBefore)}>{F(visualYawAfter)} " +
                $"targetErr={AbsDelta(currentYaw, s_turnTargetYaw):F2} phase={s_phase}",
                player);
        }

        if (s_turnLogicAlignedTime < 0f && AbsDelta(currentYaw, s_turnTargetYaw) <= AlignedAngle)
        {
            s_turnLogicAlignedTime = Time.unscaledTime;
        }
    }

    public static void ObserveVisual(
        Player player,
        float yawBefore,
        float yawAfter,
        float logicYaw,
        bool heldByTurnPresentation,
        float angularSpeed)
    {
        if (!Enabled || !EnsureSession(player, player)) return;

        var delta = Mathf.Abs(Mathf.DeltaAngle(yawBefore, yawAfter));
        var heldEdge = heldByTurnPresentation != s_visualHeld;
        s_visualYaw = yawAfter;
        s_logicYaw = logicYaw;
        s_rootYaw = Yaw(player.transform.forward);
        s_visualHeld = heldByTurnPresentation;
        s_visualSpeed = angularSpeed;

        if (heldEdge)
        {
            Log(
                "VISUAL_HOLD_EDGE",
                $"held={heldByTurnPresentation} logic={F(logicYaw)} visual={F(yawAfter)} lag={AbsDelta(yawAfter, logicYaw):F2} phase={s_phase}",
                player);
        }

        if (s_turnCaptureActive)
        {
            s_turnVisualTravel += delta;
            s_turnMaxVisualLag = Mathf.Max(s_turnMaxVisualLag, AbsDelta(yawAfter, logicYaw));
            if (!s_turnVisualResponseLogged && delta >= AngleEpsilon)
            {
                s_turnVisualResponseLogged = true;
                Log(
                    "VISUAL_RESPONSE",
                    $"capture={s_turnCaptureId} visual={F(yawBefore)}>{F(yawAfter)} d={delta:F2} speedCap={angularSpeed:F1} " +
                    $"logic={F(logicYaw)} lag={AbsDelta(yawAfter, logicYaw):F2} held={heldByTurnPresentation} phase={s_phase}",
                    player);
            }

            if (s_turnVisualAlignedTime < 0f && AbsDelta(yawAfter, s_turnTargetYaw) <= AlignedAngle)
            {
                s_turnVisualAlignedTime = Time.unscaledTime;
            }

            if (Time.unscaledTime >= s_captureUntil)
            {
                EndTurnCapture("window-expired");
            }
        }

        TryLogSample(player);
    }

    public static void ObserveLocomotionFrame(Player player, Vector3 positionBefore, Vector3 positionAfter)
    {
        if (!Enabled || !EnsureSession(player, player)) return;
        var phase = !player.HasMovementIntent ? Phase.Idle : (player.WantsRun ? Phase.RunLoop : Phase.WalkLoop);
        EnsurePhase(player, phase, null, 0, default);
        AccumulatePhaseFrame(player, positionBefore, positionAfter, 0f, true);
    }

    public static void ObserveActionEnter(
        Player player,
        ActionDataSO action,
        uint leaseVersion,
        in ActionMotionExecutionPlan plan)
    {
        if (!Enabled || !EnsureSession(player, player)) return;
        EnsurePhase(player, ClassifyAction(player, action), action, leaseVersion, in plan);
    }

    public static void ObserveActionFrame(
        Player player,
        ActionDataSO action,
        uint leaseVersion,
        float normalizedTime,
        in ActionMotionExecutionPlan plan,
        bool motionProfileAppliedMotor,
        Vector3 positionBefore,
        Vector3 positionAfter)
    {
        if (!Enabled || !EnsureSession(player, player)) return;
        EnsurePhase(player, ClassifyAction(player, action), action, leaseVersion, in plan);
        AccumulatePhaseFrame(player, positionBefore, positionAfter, normalizedTime, motionProfileAppliedMotor || plan.RequiresBaseMotorTick);
    }

    public static void ObserveActionExit(Player player, ActionDataSO action, uint leaseVersion)
    {
        if (!Enabled || !EnsureSession(player, player)) return;
        if (s_phaseAction == action && (s_phaseLease == leaseVersion || leaseVersion == 0))
        {
            EndPhase(player, "action-exit");
        }
    }

    static bool EnsureSession(Player player, Object context)
    {
        if (player == null) return false;
        if (player == s_player) return true;

        if (s_player != null)
        {
            EndTurnCapture("entity-change");
            EndPhase(s_player, "entity-change");
        }

        s_player = player;
        s_sessionId++;
        s_eventId = 0;
        s_previousInputActive = false;
        s_previousInputSector = -1;
        s_phase = Phase.None;
        s_turnCaptureActive = false;
        s_turnInfo = default;
        s_logicYaw = Yaw(player.LogicForward);
        s_rootYaw = Yaw(player.transform.forward);
        s_visualYaw = Yaw(player.VisualRotation * Vector3.forward);
        var tuning = player.LocomotionProfile != null ? player.LocomotionProfile.Tuning : null;
        Log(
            "SESSION_BEGIN",
            $"format=compact-v1 stack=off sampleHz=10 maxWindow=4s player={player.name} " +
            $"root={TransformId(player.transform)} visual={TransformId(player.VisualRoot)} " +
            $"vectorResponse={(tuning != null && tuning.UseVectorVelocityResponse)} " +
            $"walkRise={(tuning != null ? tuning.WalkRiseTime : -1f):F3} runRise={(tuning != null ? tuning.RunRiseTime : -1f):F3} " +
            $"dirResponse={(tuning != null ? tuning.DirectionTurnResponseTime : -1f):F3} reverseResponse={(tuning != null ? tuning.ReverseResponseTime : -1f):F3} " +
            $"startFloor={(tuning != null ? tuning.StartSpeedFloorRatio : -1f):F3} " +
            $"motionFacing={(tuning != null ? tuning.MotionFacingAngularSpeedDeg : -1f):F1} " +
            $"visualFacing={(tuning != null ? tuning.VisualMaxAngularSpeedDeg : -1f):F1} " +
            $"runSkipTurn={(tuning != null && tuning.SkipTurnPresentationWhenWantsRun)}",
            context != null ? context : player);
        return true;
    }

    static void BeginTurnCapture(Player player, float targetYaw)
    {
        s_turnCaptureActive = true;
        s_turnCaptureId++;
        s_turnStartTime = Time.unscaledTime;
        s_turnStartLogicYaw = Yaw(player.LogicForward);
        s_turnStartRootYaw = Yaw(player.transform.forward);
        s_turnStartVisualYaw = Yaw(player.VisualRotation * Vector3.forward);
        s_turnTargetYaw = targetYaw;
        s_turnLogicTravel = 0f;
        s_turnVisualTravel = 0f;
        s_turnMaxVisualLag = AbsDelta(s_turnStartVisualYaw, s_turnStartLogicYaw);
        s_turnLogicAlignedTime = AbsDelta(s_turnStartLogicYaw, targetYaw) <= AlignedAngle ? s_turnStartTime : -1f;
        s_turnVisualAlignedTime = AbsDelta(s_turnStartVisualYaw, targetYaw) <= AlignedAngle ? s_turnStartTime : -1f;
        s_turnLogicResponseLogged = false;
        s_turnVisualResponseLogged = false;
        s_turnRejectLogged = false;
        s_turnLogicSource = "none";
        Log(
            "TURN_BEGIN",
            $"capture={s_turnCaptureId} input={SectorName(s_inputSector)} run={s_wantsRun} " +
            $"target={F(targetYaw)} logic={F(s_turnStartLogicYaw)} root={F(s_turnStartRootYaw)} visual={F(s_turnStartVisualYaw)} " +
            $"logicErr={AbsDelta(s_turnStartLogicYaw, targetYaw):F2} visualErr={AbsDelta(s_turnStartVisualYaw, targetYaw):F2} phase={s_phase}",
            player);
    }

    static void EndTurnCapture(string reason)
    {
        if (!s_turnCaptureActive || s_player == null) return;
        var now = Time.unscaledTime;
        var logicYaw = Yaw(s_player.LogicForward);
        var rootYaw = Yaw(s_player.transform.forward);
        var visualYaw = Yaw(s_player.VisualRotation * Vector3.forward);
        Log(
            "TURN_SUMMARY",
            $"capture={s_turnCaptureId} reason={reason} ms={(now - s_turnStartTime) * 1000f:F0} " +
            $"target={F(s_turnTargetYaw)} logic={F(s_turnStartLogicYaw)}>{F(logicYaw)} root={F(s_turnStartRootYaw)}>{F(rootYaw)} " +
            $"visual={F(s_turnStartVisualYaw)}>{F(visualYaw)} logicTravel={s_turnLogicTravel:F2} visualTravel={s_turnVisualTravel:F2} " +
            $"logicAlignMs={ElapsedMs(s_turnLogicAlignedTime, s_turnStartTime)} visualAlignMs={ElapsedMs(s_turnVisualAlignedTime, s_turnStartTime)} " +
            $"logicErr={AbsDelta(logicYaw, s_turnTargetYaw):F2} visualErr={AbsDelta(visualYaw, s_turnTargetYaw):F2} " +
            $"maxVisualLag={s_turnMaxVisualLag:F2} source={s_turnLogicSource} phase={s_phase}",
            s_player);
        s_turnCaptureActive = false;
    }

    static void EnsurePhase(
        Player player,
        Phase phase,
        ActionDataSO action,
        uint leaseVersion,
        in ActionMotionExecutionPlan plan)
    {
        if (s_phase == phase && s_phaseAction == action && s_phaseLease == leaseVersion) return;
        var previous = s_phase;
        EndPhase(player, "phase-change");

        s_phase = phase;
        s_phaseAction = action;
        s_phaseLease = leaseVersion;
        s_phasePlan = plan;
        s_phaseStartTime = Time.unscaledTime;
        s_phaseStartPosition = player.transform.position;
        s_phaseLastPosition = s_phaseStartPosition;
        s_phaseFrames = 0;
        s_phaseZeroFrames = 0;
        s_phaseMotorFrames = 0;
        s_phasePath = 0f;
        s_phaseMinNonZero = float.PositiveInfinity;
        s_phaseMaxFrameDelta = 0f;
        s_phaseFirstMoveTime = -1f;
        s_lastFrameDelta = 0f;
        s_lastNormalizedTime = 0f;
        s_lastFrameAppliedMotor = false;

        if (phase != Phase.None)
        {
            Log(
                "PHASE_EDGE",
                $"from={previous} to={phase} action={Name(action)} lease={leaseVersion} " +
                $"driver={(action != null ? plan.EffectiveMode.ToString() : "LocomotionMotor")} " +
                $"baseMotor={(action != null && plan.RequiresBaseMotorTick)} planarIntent={(action != null && plan.AllowsPlanarIntent)} " +
                $"input={SectorName(s_inputSector)} run={s_wantsRun} pos={V3(s_phaseStartPosition)} speed={player.PlanarVelocity.magnitude:F3}",
                player);
        }
    }

    static void AccumulatePhaseFrame(
        Player player,
        Vector3 positionBefore,
        Vector3 positionAfter,
        float normalizedTime,
        bool appliedMotor)
    {
        var deltaVector = Vector3.ProjectOnPlane(positionAfter - positionBefore, Vector3.up);
        var delta = deltaVector.magnitude;
        s_phaseFrames++;
        s_lastFrameDelta = delta;
        s_lastNormalizedTime = normalizedTime;
        s_lastFrameAppliedMotor = appliedMotor;
        if (appliedMotor) s_phaseMotorFrames++;
        s_phasePath += delta;
        s_phaseMaxFrameDelta = Mathf.Max(s_phaseMaxFrameDelta, delta);
        if (delta <= MoveEpsilon)
        {
            s_phaseZeroFrames++;
        }
        else
        {
            s_phaseMinNonZero = Mathf.Min(s_phaseMinNonZero, delta);
            if (s_phaseFirstMoveTime < 0f) s_phaseFirstMoveTime = Time.unscaledTime;
        }
        s_phaseLastPosition = positionAfter;
    }

    static void EndPhase(Player player, string reason)
    {
        if (s_phase == Phase.None || player == null) return;
        if (IsMeasuredPhase(s_phase))
        {
            var duration = Mathf.Max(0f, Time.unscaledTime - s_phaseStartTime);
            var net = Vector3.ProjectOnPlane(s_phaseLastPosition - s_phaseStartPosition, Vector3.up).magnitude;
            Log(
                "PHASE_SUMMARY",
                $"phase={s_phase} reason={reason} action={Name(s_phaseAction)} lease={s_phaseLease} " +
                $"ms={duration * 1000f:F0} frames={s_phaseFrames} motorFrames={s_phaseMotorFrames} " +
                $"net={net:F5} path={s_phasePath:F5} minNonZero={MinNonZero()} maxFrame={s_phaseMaxFrameDelta:F5} " +
                $"zeroFrames={s_phaseZeroFrames} firstMoveMs={ElapsedMs(s_phaseFirstMoveTime, s_phaseStartTime)} " +
                $"endSpeed={player.PlanarVelocity.magnitude:F3} endNt={s_lastNormalizedTime:F3} driver={DriverName()}",
                player);
        }
        s_phase = Phase.None;
        s_phaseAction = null;
        s_phaseLease = 0;
    }

    static void TryLogSample(Player player)
    {
        var now = Time.unscaledTime;
        if (now > s_captureUntil || now < s_nextSampleTime) return;
        s_nextSampleTime = now + SampleInterval;
        var velocity = player.PlanarVelocity;
        var logicYaw = Yaw(player.LogicForward);
        var rootYaw = Yaw(player.transform.forward);
        var visualYaw = Yaw(player.VisualRotation * Vector3.forward);
        Log(
            "SAMPLE",
            $"capture={(s_turnCaptureActive ? s_turnCaptureId : 0)} phase={s_phase} action={Name(s_phaseAction)} nt={s_lastNormalizedTime:F3} " +
            $"input={SectorName(s_inputSector)} raw={V2(s_rawInput)} run={s_wantsRun} tense={s_inputTense} target={F(Yaw(s_worldIntent))} " +
            $"logic={F(logicYaw)} root={F(rootYaw)} visual={F(visualYaw)} velYaw={F(Yaw(velocity))} speed={velocity.magnitude:F3} " +
            $"intentLogic={AbsDelta(Yaw(s_worldIntent), logicYaw):F2} logicVisual={AbsDelta(logicYaw, visualYaw):F2} rootLogic={AbsDelta(rootYaw, logicYaw):F2} " +
            $"turn={s_turnInfo.IsTurning}/{s_turnInfo.Type}/{s_turnInfo.Direction}/{s_turnInfo.SignedAngle:F1} held={s_visualHeld} visualCap={s_visualSpeed:F1} " +
            $"frameMove={s_lastFrameDelta:F5} phasePath={s_phasePath:F5} motor={s_lastFrameAppliedMotor} source={s_logicSource}",
            player);
    }

    static Phase ClassifyAction(Player player, ActionDataSO action)
    {
        if (player == null || action == null) return Phase.OtherAction;
        if (IsBinding(player, action, LocomotionStateId.WalkStart)) return Phase.WalkStart;
        if (IsBinding(player, action, LocomotionStateId.RunStart)) return Phase.RunStart;
        if (IsBinding(player, action, LocomotionStateId.WalkEnd)) return Phase.WalkEnd;
        if (IsBinding(player, action, LocomotionStateId.RunEnd)) return Phase.RunEnd;
        return Phase.OtherAction;
    }

    static bool IsBinding(Player player, ActionDataSO action, LocomotionStateId state)
    {
        var profile = player.LocomotionProfile;
        return profile != null
               && profile.HasState(state)
               && profile.GetBinding(state).ResolveLocomotionAction() == action;
    }

    static bool IsMeasuredPhase(Phase phase) =>
        phase == Phase.WalkLoop || phase == Phase.RunLoop
        || phase == Phase.WalkStart || phase == Phase.RunStart
        || phase == Phase.WalkEnd || phase == Phase.RunEnd;

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

    static float Yaw(Vector3 value) => value.sqrMagnitude > 0.0001f
        ? Mathf.Atan2(value.x, value.z) * Mathf.Rad2Deg
        : float.NaN;

    static float AbsDelta(float a, float b) => IsFinite(a) && IsFinite(b)
        ? Mathf.Abs(Mathf.DeltaAngle(a, b))
        : float.NaN;

    static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static string F(float value) => IsFinite(value) ? value.ToString("F2") : "NA";
    static string V2(Vector2 value) => $"({value.x:F2},{value.y:F2})";
    static string V3(Vector3 value) => $"({value.x:F3},{value.y:F3},{value.z:F3})";
    static string Safe(string value) => string.IsNullOrEmpty(value) ? "none" : value.Replace(' ', '_');
    static string Name(Object value) => value != null ? Safe(value.name) : "none";
    static string TransformId(Transform value) => value != null ? $"{value.name}#{value.GetInstanceID()}" : "none";
    static string MinNonZero() => float.IsPositiveInfinity(s_phaseMinNonZero) ? "NA" : s_phaseMinNonZero.ToString("F5");
    static string DriverName() => s_phaseAction != null ? s_phasePlan.EffectiveMode.ToString() : "LocomotionMotor";
    static string ElapsedMs(float timestamp, float origin) => timestamp >= 0f ? ((timestamp - origin) * 1000f).ToString("F0") : "NA";

    static void Log(string eventName, string details, Object context)
    {
        var entityId = s_player != null ? s_player.GetInstanceID() : 0;
        var message =
            $"{Prefix} e={eventName} sid={s_sessionId} id={++s_eventId} entity={entityId} " +
            $"f={Time.frameCount} t={Time.unscaledTime:F3} | {details}";
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, context, "{0}", message);
    }
}

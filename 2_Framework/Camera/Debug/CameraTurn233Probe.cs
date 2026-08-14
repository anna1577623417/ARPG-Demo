using UnityEngine;

/// <summary>
/// 233 — Camera / Turn 权威链只读探针。
/// 只采样现有 Runtime 事实，不修正 yaw、输入、转向、Proxy 或 Cinemachine 状态。
/// </summary>
public static class CameraTurn233Probe
{
    const string Prefix = "[CameraTurn233]";
    const float EpsilonDeg = 0.01f;
    const float EdgeDeg = 0.5f;
    const float InstantJumpDeg = 15f;
    const float SampleInterval = 0.50f;
    const float AnomalyRepeatInterval = 1.00f;
    const float CaptureWindowAfterActivity = 8.00f;

    static Player s_player;
    static int s_sessionId;
    static int s_eventId;
    static bool s_renderHooked;

    static Vector2 s_rawInput;
    static Vector3 s_worldIntent;
    static bool s_inputActive;
    static int s_inputSector = -1;
    static string s_contextSource = "none";
    static bool s_cameraRelative;
    static float s_movementReferenceYaw = float.NaN;

    static float s_logicYaw = float.NaN;
    static float s_rootYaw = float.NaN;
    static float s_visualYaw = float.NaN;
    static float s_visualLag;
    static bool s_visualHeld;
    static TurnInfo s_turnInfo;

    static float s_controllerYaw = float.NaN;
    static float s_followWorldYaw = float.NaN;
    static float s_followLocalYaw = float.NaN;
    static float s_proxyYaw = float.NaN;
    static float s_mainYawAtController = float.NaN;
    static float s_mainYawAtProxy = float.NaN;
    static bool s_lookActive;
    static bool s_lookLocked;
    static bool s_chaseApplied;
    static float s_chaseDelta;
    static float s_impulseDelta;

    static bool s_previousInputActive;
    static int s_previousInputSector = -1;
    static float s_previousReferenceYaw = float.NaN;
    static bool s_previousTurnActive;
    static TurnType s_previousTurnType;
    static sbyte s_previousTurnDirection;
    static bool s_previousVisualHeld;
    static bool s_previousChaseApplied;
    static bool s_previousLookActive;
    static bool s_proxyMismatchActive;
    static bool s_captureArmed;
    static float s_nextReferenceJumpLogTime;
    static float s_nextLogicJumpLogTime;
    static float s_nextRejectLogTime;
    static float s_nextProxyMismatchLogTime;
    static float s_nextRenderSampleTime;
    static float s_lastActivityTime = -999f;

    static bool Enabled => GameMainDebugSettings.CameraTurn233Log;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        if (s_renderHooked)
        {
            Camera.onPreCull -= OnCameraPreCull;
        }

        s_player = null;
        s_sessionId = 0;
        s_eventId = 0;
        s_renderHooked = false;
        s_inputSector = -1;
        s_previousInputSector = -1;
        s_movementReferenceYaw = float.NaN;
        s_previousReferenceYaw = float.NaN;
        s_logicYaw = float.NaN;
        s_rootYaw = float.NaN;
        s_visualYaw = float.NaN;
        s_controllerYaw = float.NaN;
        s_followWorldYaw = float.NaN;
        s_followLocalYaw = float.NaN;
        s_proxyYaw = float.NaN;
        s_mainYawAtController = float.NaN;
        s_mainYawAtProxy = float.NaN;
        s_lastActivityTime = -999f;
    }

    public static void ObserveInput(
        Player player,
        Vector2 rawInput,
        Vector3 worldIntent,
        string contextSource,
        bool cameraRelative,
        float movementReferenceYaw)
    {
        if (!Enabled) return;
        if (!EnsureSession(player, player)) return;

        var active = rawInput.sqrMagnitude > 0.0001f;
        var sector = ResolveSector(rawInput);
        var inputEdge = active != s_previousInputActive || (active && sector != s_previousInputSector);
        var referenceShift = IsFinite(movementReferenceYaw)
                             && IsFinite(s_previousReferenceYaw)
                             && Mathf.Abs(Mathf.DeltaAngle(s_previousReferenceYaw, movementReferenceYaw)) >= EdgeDeg;

        s_rawInput = rawInput;
        s_worldIntent = worldIntent;
        s_inputActive = active;
        s_inputSector = sector;
        s_contextSource = string.IsNullOrEmpty(contextSource) ? "none" : contextSource;
        s_cameraRelative = cameraRelative;
        s_movementReferenceYaw = movementReferenceYaw;

        if (inputEdge)
        {
            s_captureArmed = true;
            s_lastActivityTime = Time.unscaledTime;
            Log(
                "INPUT_EDGE",
                $"active={active} sector={SectorName(sector)} raw={V2(rawInput)} " +
                $"refYaw={F(movementReferenceYaw)} worldIntent={V3(worldIntent)} worldYaw={F(Yaw(worldIntent))} " +
                $"ctx={s_contextSource} cameraRelative={cameraRelative}",
                player);
        }
        else if (referenceShift
                 && Mathf.Abs(Mathf.DeltaAngle(s_previousReferenceYaw, movementReferenceYaw)) >= InstantJumpDeg
                 && Time.unscaledTime >= s_nextReferenceJumpLogTime)
        {
            s_nextReferenceJumpLogTime = Time.unscaledTime + AnomalyRepeatInterval;
            Log(
                "REFERENCE_JUMP",
                $"raw={V2(rawInput)} ref={F(s_previousReferenceYaw)}>{F(movementReferenceYaw)} " +
                $"d={F(Mathf.DeltaAngle(s_previousReferenceYaw, movementReferenceYaw))} " +
                $"intent={F(Yaw(worldIntent))} ctx={s_contextSource}",
                player);
        }

        s_previousInputActive = active;
        s_previousInputSector = sector;
        if (IsFinite(movementReferenceYaw)) s_previousReferenceYaw = movementReferenceYaw;
    }

    public static void ObserveLogicForwardRejected(Player player, Vector3 requested, string source)
    {
        if (!Enabled) return;
        if (!EnsureSession(player, player)) return;
        if (Time.unscaledTime < s_nextRejectLogTime) return;
        s_nextRejectLogTime = Time.unscaledTime + AnomalyRepeatInterval;
        Log(
            "LOGIC_FORWARD_REJECT",
            $"source={Safe(source)} reason=logicForwardLocked requested={V3(requested)} " +
            $"logicYaw={F(Yaw(player != null ? player.LogicForward : Vector3.zero))}",
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
        if (!Enabled) return;
        if (!EnsureSession(player, player)) return;

        var previousYaw = Yaw(previous);
        var currentYaw = Yaw(current);
        var logicDelta = Mathf.DeltaAngle(previousYaw, currentYaw);
        var rootDelta = Mathf.DeltaAngle(rootYawBefore, rootYawAfter);
        var visualDelta = Mathf.DeltaAngle(visualYawBefore, visualYawAfter);

        s_logicYaw = currentYaw;
        s_rootYaw = rootYawAfter;
        s_visualYaw = visualYawAfter;

        if (Mathf.Abs(logicDelta) >= InstantJumpDeg && Time.unscaledTime >= s_nextLogicJumpLogTime)
        {
            s_captureArmed = true;
            s_nextLogicJumpLogTime = Time.unscaledTime + AnomalyRepeatInterval;
            s_lastActivityTime = Time.unscaledTime;
            Log(
                "LOGIC_JUMP",
                $"src={Safe(source)} logic={F(previousYaw)}>{F(currentYaw)} d={F(logicDelta)} " +
                $"root={F(rootYawBefore)}>{F(rootYawAfter)} rd={F(rootDelta)} " +
                $"visual={F(visualYawBefore)}>{F(visualYawAfter)} vd={F(visualDelta)} " +
                $"intent={F(Yaw(player != null ? player.MovementIntent : Vector3.zero))}",
                player);
        }
    }

    public static void ObserveTurnInfo(Player player, in TurnInfo previous, in TurnInfo current, string source)
    {
        if (!Enabled) return;
        if (!EnsureSession(player, player)) return;
        s_turnInfo = current;

        var edge = current.IsTurning != s_previousTurnActive
                   || current.Type != s_previousTurnType
                   || current.Direction != s_previousTurnDirection;
        if (edge)
        {
            s_captureArmed = true;
            s_lastActivityTime = Time.unscaledTime;
            Log(
                "TURN_PRESENTATION_EDGE",
                $"source={Safe(source)} turning={previous.IsTurning}->{current.IsTurning} " +
                $"type={previous.Type}->{current.Type} dir={previous.Direction}->{current.Direction} " +
                $"signed={current.SignedAngle:F2} abs={current.Angle:F2} " +
                $"logicYaw={F(Yaw(player != null ? player.LogicForward : Vector3.zero))} " +
                $"visualYaw={F(player != null ? Yaw(player.VisualRotation * Vector3.forward) : float.NaN)} " +
                $"intentYaw={F(player != null ? Yaw(player.MovementIntent) : float.NaN)}",
                player);
        }

        s_previousTurnActive = current.IsTurning;
        s_previousTurnType = current.Type;
        s_previousTurnDirection = current.Direction;
    }

    public static void ObserveVisual(
        Player player,
        float visualYawBefore,
        float visualYawAfter,
        float logicYaw,
        float lagAngle,
        bool heldByTurnPresentation,
        float angularSpeed)
    {
        if (!Enabled) return;
        if (!EnsureSession(player, player)) return;

        s_visualYaw = visualYawAfter;
        s_logicYaw = logicYaw;
        s_visualLag = lagAngle;
        s_visualHeld = heldByTurnPresentation;

        if (heldByTurnPresentation != s_previousVisualHeld)
        {
            Log(
                "VISUAL_AUTHORITY_EDGE",
                $"heldByTurnPresentation={heldByTurnPresentation} visualYaw={F(visualYawAfter)} " +
                $"logicYaw={F(logicYaw)} lag={lagAngle:F2} angularSpeed={angularSpeed:F1}",
                player);
        }

        s_previousVisualHeld = heldByTurnPresentation;
    }

    public static void ObserveCameraController(
        Player player,
        Object context,
        Vector2 rawLook,
        bool lookLocked,
        bool chaseEnabled,
        float chaseDelay,
        float chaseDeadzone,
        float chaseSpeed,
        float lookIdleAge,
        float parentYaw,
        float yawBefore,
        float yawAfterLook,
        float yawAfterChase,
        float yawAfterImpulse,
        float pitch,
        float followWorldYawBefore,
        float followLocalYawBefore,
        float followWorldYawAfter,
        float followLocalYawAfter,
        float mainCameraYaw)
    {
        if (!Enabled) return;
        if (!EnsureSession(player, context)) return;
        EnsureRenderHook();

        var lookActive = !lookLocked && rawLook.sqrMagnitude > 0.0001f;
        var lookDelta = Mathf.DeltaAngle(yawBefore, yawAfterLook);
        var chaseDelta = Mathf.DeltaAngle(yawAfterLook, yawAfterChase);
        var impulseDelta = Mathf.DeltaAngle(yawAfterChase, yawAfterImpulse);
        var chaseApplied = Mathf.Abs(chaseDelta) > EpsilonDeg;
        var chaseError = Mathf.DeltaAngle(yawAfterLook, parentYaw);
        var chaseEligible = chaseEnabled && !lookLocked && lookIdleAge >= chaseDelay
                            && Mathf.Abs(chaseError) > chaseDeadzone;

        s_controllerYaw = yawAfterImpulse;
        s_rootYaw = parentYaw;
        s_followWorldYaw = followWorldYawAfter;
        s_followLocalYaw = followLocalYawAfter;
        s_mainYawAtController = mainCameraYaw;
        s_lookActive = lookActive;
        s_lookLocked = lookLocked;
        s_chaseApplied = chaseApplied;
        s_chaseDelta = chaseDelta;
        s_impulseDelta = impulseDelta;

        if (lookActive != s_previousLookActive)
        {
            s_captureArmed = true;
            s_lastActivityTime = Time.unscaledTime;
            Log(
                "LOOK_EDGE",
                $"active={lookActive} locked={lookLocked} raw={V2(rawLook)} " +
                $"cam={F(yawBefore)}>{F(yawAfterLook)} lookD={F(lookDelta)}",
                context);
        }

        var chaseEdge = chaseApplied != s_previousChaseApplied;
        if (chaseEdge)
        {
            s_captureArmed = true;
            s_lastActivityTime = Time.unscaledTime;
            Log(
                "CHASE_EDGE",
                $"applied={chaseApplied} eligible={chaseEligible} enabled={chaseEnabled} lookActive={lookActive} " +
                $"lookLocked={lookLocked} lookIdleAge={lookIdleAge:F3} delay={chaseDelay:F3} " +
                $"parentYaw={F(parentYaw)} controllerYaw={F(yawAfterLook)} error={F(chaseError)} " +
                $"deadzone={chaseDeadzone:F2} speed={chaseSpeed:F1}",
                context);
        }

        if (chaseApplied && !lookActive && chaseEdge)
        {
            s_lastActivityTime = Time.unscaledTime;
            Log(
                "COUPLING_BEGIN",
                $"driver=runtime-chase parent={F(parentYaw)} cam={F(yawAfterLook)}>{F(yawAfterChase)} " +
                $"cd={F(chaseDelta)} ref={F(yawAfterImpulse)} raw={V2(s_rawInput)} " +
                $"intent={F(Yaw(s_worldIntent))} logic={F(s_logicYaw)} " +
                $"followW={F(followWorldYawBefore)}>{F(followWorldYawAfter)} " +
                $"followL={F(followLocalYawBefore)}>{F(followLocalYawAfter)} " +
                $"lookD={F(lookDelta)} impulseD={F(impulseDelta)}",
                context);
        }

        s_previousChaseApplied = chaseApplied;
        s_previousLookActive = lookActive;
    }

    public static void ObserveProxy(
        Player player,
        Object context,
        float sourceYaw,
        float proxyYawBefore,
        float proxyYawAfter,
        Vector3 sourcePosition,
        Vector3 proxyPosition,
        float mainCameraYaw)
    {
        if (!Enabled) return;
        if (!EnsureSession(player, context)) return;

        s_proxyYaw = proxyYawAfter;
        s_mainYawAtProxy = mainCameraYaw;
        var mirrorError = Mathf.Abs(Mathf.DeltaAngle(sourceYaw, proxyYawAfter));
        var mismatch = mirrorError > 0.10f;
        if (mismatch != s_proxyMismatchActive
            || (mismatch && Time.unscaledTime >= s_nextProxyMismatchLogTime))
        {
            s_captureArmed = true;
            s_lastActivityTime = Time.unscaledTime;
            s_nextProxyMismatchLogTime = Time.unscaledTime + AnomalyRepeatInterval;
            Log(
                "PROXY_MIRROR_EDGE",
                $"active={mismatch} source={F(sourceYaw)} proxy={F(proxyYawBefore)}>{F(proxyYawAfter)} " +
                $"error={mirrorError:F3} sourcePos={V3(sourcePosition)} proxyPos={V3(proxyPosition)}",
                context);
        }
        s_proxyMismatchActive = mismatch;
    }

    public static void ObserveBinding(Player player, Object context, Transform follow, Transform proxy, Transform lookAt)
    {
        if (!Enabled) return;
        if (!EnsureSession(player, context)) return;
        Log(
            "CAMERA_BIND",
            $"follow={TransformId(follow)} followParent={TransformId(follow != null ? follow.parent : null)} " +
            $"proxy={TransformId(proxy)} proxyParent={TransformId(proxy != null ? proxy.parent : null)} " +
            $"lookAt={TransformId(lookAt)}",
            context);
    }

    static bool EnsureSession(Player player, Object context)
    {
        if (player == null) return false;
        if (player == s_player) return true;

        s_player = player;
        s_sessionId++;
        s_eventId = 0;
        s_previousInputActive = false;
        s_previousInputSector = -1;
        s_previousReferenceYaw = float.NaN;
        s_previousTurnActive = false;
        s_previousTurnType = TurnType.None;
        s_previousTurnDirection = 0;
        s_previousVisualHeld = false;
        s_previousChaseApplied = false;
        s_previousLookActive = false;
        s_proxyMismatchActive = false;
        s_captureArmed = false;
        s_nextReferenceJumpLogTime = 0f;
        s_nextLogicJumpLogTime = 0f;
        s_nextRejectLogTime = 0f;
        s_nextProxyMismatchLogTime = 0f;
        s_nextRenderSampleTime = 0f;
        s_lastActivityTime = Time.unscaledTime;

        Log(
            "SESSION_BEGIN",
            $"format=compact-v2 stack=off sampleHz=2 maxWindow=8s player={player.name} " +
            $"root={TransformId(player.transform)} visual={TransformId(player.VisualRoot)}",
            context != null ? context : player);
        return true;
    }

    static void EnsureRenderHook()
    {
        if (s_renderHooked) return;
        Camera.onPreCull += OnCameraPreCull;
        s_renderHooked = true;
    }

    static void OnCameraPreCull(Camera camera)
    {
        if (!Enabled || camera == null || camera != Camera.main || s_player == null) return;

        var now = Time.unscaledTime;
        // 真实 Edge 唤醒采样窗；连续按键、持续 Look/Chase 不续期，避免无限生成日志。
        var activeWindow = s_captureArmed && now - s_lastActivityTime <= CaptureWindowAfterActivity;
        if (!activeWindow || now < s_nextRenderSampleTime) return;
        s_nextRenderSampleTime = now + SampleInterval;

        var mainYaw = Yaw(camera.transform.forward);
        var mainPitch = NormalizePitch(camera.transform.eulerAngles.x);
        var rootYaw = Yaw(s_player.transform.forward);
        var logicYaw = Yaw(s_player.LogicForward);
        var visualYaw = Yaw(s_player.VisualRotation * Vector3.forward);
        var velocityYaw = Yaw(s_player.PlanarVelocity);

        Log(
            "SAMPLE",
            $"raw={V2(s_rawInput)} sec={SectorName(s_inputSector)} ref={F(s_movementReferenceYaw)} " +
            $"ctx={s_contextSource}/{s_cameraRelative} " +
            $"intent={F(Yaw(s_worldIntent))} logic={F(logicYaw)} root={F(rootYaw)} visual={F(visualYaw)} " +
            $"vlag={Mathf.Abs(Mathf.DeltaAngle(visualYaw, logicYaw)):F2} held={s_visualHeld} " +
            $"turn={s_turnInfo.IsTurning}/{s_turnInfo.Type}/{s_turnInfo.Direction}/{s_turnInfo.SignedAngle:F1} " +
            $"vel={F(velocityYaw)} speed={s_player.PlanarVelocity.magnitude:F2} " +
            $"cam={F(s_controllerYaw)} look={s_lookActive}/{s_lookLocked} chase={s_chaseApplied}/{F(s_chaseDelta)} " +
            $"followW={F(s_followWorldYaw)} followL={F(s_followLocalYaw)} proxy={F(s_proxyYaw)} " +
            $"mainC={F(s_mainYawAtController)} mainP={F(s_mainYawAtProxy)} render={F(mainYaw)}/{mainPitch:F1}",
            camera);
    }

    static void Log(string eventName, string details, Object context)
    {
        var entityId = s_player != null ? s_player.GetInstanceID() : 0;
        var message =
            $"{Prefix} e={eventName} sid={s_sessionId} id={++s_eventId} entity={entityId} " +
            $"f={Time.frameCount} t={Time.unscaledTime:F3} | {details}";
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, context, "{0}", message);
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
            default: return "None";
        }
    }

    static float Yaw(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.000001f) return float.NaN;
        return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
    }

    static float NormalizePitch(float pitch) => pitch > 180f ? pitch - 360f : pitch;
    static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static string F(float value) => IsFinite(value) ? value.ToString("F2") : "NA";
    static string V2(Vector2 value) => $"({value.x:F3},{value.y:F3})";
    static string V3(Vector3 value) => $"({value.x:F3},{value.y:F3},{value.z:F3})";
    static string Safe(string value) => string.IsNullOrEmpty(value) ? "unknown" : value;

    static string TransformId(Transform value) =>
        value == null ? "null" : $"{value.name}#{value.GetInstanceID()}";
}

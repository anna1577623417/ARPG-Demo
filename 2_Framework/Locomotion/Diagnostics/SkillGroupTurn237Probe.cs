using UnityEngine;

/// <summary>
/// 237 v2 — 为【资料】DesiredFacing / CommittedFacing / EntryBasis 取证。
/// 要证伪：WASD 是否把 Desired 直接写成 Committed；ChordReframe 是否丢掉 MoveDown basisFacing。
/// 边沿事件，无堆栈。Console 过滤：[Turn237]
/// </summary>
public static class SkillGroupTurn237Probe
{
    public const string Prefix = "[Turn237]";
    const float LogicSnapDeg = 8f;
    const float VisualSnapDeg = 25f;
    const float PoseWarnDeg = 20f;
    const float BasisSplitDeg = 20f;
    const float VisualSnapThrottleSec = 0.12f;

    static Player s_player;
    static int s_sessionId;
    static int s_eventId;
    static float s_lastVisualSnapUnscaled = -999f;
    static bool s_poseWarned;
    static int s_lastLogicSnapFrame = -1;
    static string s_lastLogicSnapSource;
    static string s_lastPickKey;
    static int s_lastCommitFrame = -1;
    static SkillEntrySlot s_lastPickSlot;
    static string s_lastPickGroup;
    static DirectionalRouteType s_lastResolved;
    static float s_lastCaptureYaw;

    static bool Enabled => GameMainDebugSettings.SkillGroupTurn237Log;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_player = null;
        s_sessionId = 0;
        s_eventId = 0;
        s_lastVisualSnapUnscaled = -999f;
        s_poseWarned = false;
        s_lastLogicSnapFrame = -1;
        s_lastLogicSnapSource = null;
        s_lastPickKey = null;
        s_lastCommitFrame = -1;
        s_lastPickGroup = null;
        s_lastCaptureYaw = 0f;
    }

    public static void ObserveDirDown(Player player, Vector2 rawMove, Vector3 basisForward, Vector3 logicForward)
    {
        if (!Enabled || player == null)
        {
            return;
        }

        EnsureSession(player);
        s_lastCaptureYaw = Yaw(basisForward);
        var liveYaw = Yaw(logicForward);
        Log(
            "DIR_DOWN",
            $"axis=({rawMove.x:F2},{rawMove.y:F2}) basisYaw={s_lastCaptureYaw:F1} " +
            $"liveYaw={liveYaw:F1} desiredIndependent=False commitPending=True " +
            $"state={StateName(player)}",
            player);
    }

    public static void ObserveLogicSnap(Player player, Vector3 prev, Vector3 next, string source)
    {
        if (!Enabled || player == null)
        {
            return;
        }

        var delta = SignedYaw(prev, next);
        if (Mathf.Abs(delta) < LogicSnapDeg)
        {
            return;
        }

        if (Time.frameCount == s_lastLogicSnapFrame
            && string.Equals(s_lastLogicSnapSource, source, System.StringComparison.Ordinal))
        {
            return;
        }

        EnsureSession(player);
        s_lastLogicSnapFrame = Time.frameCount;
        s_lastLogicSnapSource = source ?? "unknown";
        var isImmediate = string.Equals(source, "TurnCompensation.ImmediateCommit", System.StringComparison.Ordinal);
        Log(
            "LOGIC_SNAP",
            $"source={Safe(source)} dYaw={delta:F1} prev={Yaw(prev):F1} next={Yaw(next):F1} " +
            $"desiredYaw={Yaw(next):F1} committedYaw={Yaw(next):F1} " +
            $"commitPolicy={(isImmediate ? "Immediate" : "LiveWrite")} " +
            $"state={StateName(player)} lock={player.IsLogicForwardLocked}",
            player);
    }

    public static void ObserveCue(Player player, in TurnCompensationCue cue, string reason)
    {
        if (!Enabled || player == null || !cue.IsTurning)
        {
            return;
        }

        EnsureSession(player);
        var claimed = DirectionTokenOwner.None;
        var token = player.DirectionHistory.LastToken;
        if (token > 0)
        {
            player.DirectionHistory.TryGetOwner(token, out claimed);
        }

        Log(
            "CUE_FIRE",
            $"reason={Safe(reason)} type={cue.Type} gen={cue.Generation} signed={cue.SignedAngle:F1} " +
            $"abs={cue.AbsAngle:F1} logicYaw={Yaw(player.LogicForward):F1} visualYaw={VisualYaw(player):F1} " +
            $"claimed={claimed}",
            player);
    }

    public static void ObserveCommit(
        Player player,
        Vector2 pulse,
        float holdDur,
        float chordWin,
        string source,
        Vector3 captureForward,
        Vector3 liveForward,
        Vector3 committedForward)
    {
        if (!Enabled || player == null)
        {
            return;
        }

        if (Time.frameCount == s_lastCommitFrame)
        {
            return;
        }

        EnsureSession(player);
        s_lastCommitFrame = Time.frameCount;
        s_lastCaptureYaw = Yaw(captureForward);
        var isChord = holdDur >= 0f && holdDur <= chordWin;
        var captureYaw = s_lastCaptureYaw;
        var liveYaw = Yaw(liveForward);
        var commitYaw = Yaw(committedForward);
        Log(
            "COMMIT",
            $"source={Safe(source)} pulse=({pulse.x:F2},{pulse.y:F2}) holdDur={holdDur:F3} chordWin={chordWin:F3} " +
            $"mode={(isChord ? "Chord" : "Motion")} captureYaw={captureYaw:F1} liveYaw={liveYaw:F1} " +
            $"commitYaw={commitYaw:F1} basisSplit={AbsDelta(captureYaw, liveYaw) >= BasisSplitDeg} " +
            $"committed={player.InputContext.DirectionalCommitted}",
            player);
    }

    public static void ObservePick(
        Player player,
        SkillEntrySlot slot,
        SkillGroupDefinition group,
        DirectionalRouteType cameraSlot,
        DirectionalRouteType logicSlot,
        bool reframed,
        SkillRouteDefinition route,
        bool isMotionMode,
        float holdDur,
        float chordWin,
        Vector2 axis)
    {
        if (!Enabled || player == null)
        {
            return;
        }

        var groupName = group != null ? group.name : "null";
        var routeName = route != null ? route.name : "null";
        var resolved = reframed ? logicSlot : cameraSlot;
        var key =
            $"{slot}|{groupName}|{cameraSlot}|{logicSlot}|{reframed}|{routeName}|{isMotionMode}";
        if (string.Equals(key, s_lastPickKey, System.StringComparison.Ordinal))
        {
            return;
        }

        EnsureSession(player);
        s_lastPickKey = key;
        s_lastPickSlot = slot;
        s_lastPickGroup = groupName;
        s_lastResolved = resolved;
        var captureYaw = player.InputContext.TryGetMoveDownPlanarForward(out var captured)
            ? Yaw(captured)
            : s_lastCaptureYaw;
        var liveYaw = Yaw(player.LogicForward);
        var inputFrame = group != null ? group.DirectionalInputFrame.ToString() : "null";
        var motionBasis = group != null
            ? group.ResolveMotionCurveBasis(route?.FirstStage()?.Action?.MotionProfile).ToString()
            : "null";
        Log(
            "PICK",
            $"slot={slot} group={groupName} inputFrame={inputFrame} motionBasis={motionBasis} " +
            $"cameraSlot={cameraSlot} logicSlot={logicSlot} reframe={reframed} resolved={resolved} " +
            $"route={routeName} mode={(isMotionMode ? "Motion" : "Chord")} " +
            $"holdDur={holdDur:F3} chordWin={chordWin:F3} axis=({axis.x:F2},{axis.y:F2}) " +
            $"captureYaw={captureYaw:F1} liveYaw={liveYaw:F1} " +
            $"usedLiveLogicForReframe={reframed}",
            player);
    }

    public static void ObserveActionBegin(
        Player player,
        ActionDataSO action,
        SkillGroupDefinition group,
        SkillEntrySlot slot,
        Vector3 burstFace)
    {
        if (!Enabled || player == null)
        {
            return;
        }

        EnsureSession(player);
        s_poseWarned = false;
        var logicYaw = Yaw(player.LogicForward);
        var visualYaw = VisualYaw(player);
        var burstYaw = Yaw(burstFace);
        var mismatch = AbsDelta(logicYaw, visualYaw);
        var basisSplit = AbsDelta(burstYaw, logicYaw) >= BasisSplitDeg;
        var groupName = group != null ? group.name : s_lastPickGroup ?? "null";
        Log(
            "ACTION_BEGIN",
            $"slot={slot} pickSlot={s_lastPickSlot} group={groupName} " +
            $"action={(action != null ? action.name : "null")} resolved={s_lastResolved} " +
            $"burstYaw={burstYaw:F1} logicYaw={logicYaw:F1} visualYaw={visualYaw:F1} " +
            $"captureYaw={s_lastCaptureYaw:F1} visLogicDelta={mismatch:F1} basisSplit={basisSplit} " +
            $"lock={player.IsLogicForwardLocked} state={StateName(player)}",
            player);

        if (mismatch >= PoseWarnDeg || basisSplit)
        {
            ObservePoseWarn(player, basisSplit ? "basis_split" : "action_enter");
        }
    }

    public static void ObserveActionEnd(Player player, ActionDataSO action)
    {
        if (!Enabled || player == null)
        {
            return;
        }

        EnsureSession(player);
        var leftover = player.CurrentTurnCompensationCue;
        var claimed = DirectionTokenOwner.None;
        if (player.FrozenDirectionalEntry.IsValid)
        {
            player.DirectionHistory.TryGetOwner(player.FrozenDirectionalEntry.Token, out claimed);
        }
        else if (player.DirectionHistory.LastToken > 0)
        {
            player.DirectionHistory.TryGetOwner(player.DirectionHistory.LastToken, out claimed);
        }

        Log(
            "ACTION_END",
            $"action={(action != null ? action.name : "null")} " +
            $"logicYaw={Yaw(player.LogicForward):F1} visualYaw={VisualYaw(player):F1} " +
            $"state={StateName(player)} leftoverCue={leftover.IsTurning} leftoverGen={leftover.Generation} " +
            $"claimed={claimed}",
            player);
        s_poseWarned = false;
    }

    public static void ObserveTurnInterrupt(Player player, string reason, bool wasTurnVisual, bool wasTurnCue)
    {
        if (!Enabled || player == null || (!wasTurnVisual && !wasTurnCue))
        {
            return;
        }

        EnsureSession(player);
        var visLogic = AbsDelta(Yaw(player.LogicForward), VisualYaw(player));
        Log(
            "INTERRUPT",
            $"reason={Safe(reason)} wasTurnVisual={wasTurnVisual} wasTurnCue={wasTurnCue} " +
            $"visLogicDelta={visLogic:F1} logicYaw={Yaw(player.LogicForward):F1} visualYaw={VisualYaw(player):F1} " +
            $"state={StateName(player)} claimed=False",
            player);

        if (visLogic >= PoseWarnDeg)
        {
            ObservePoseWarn(player, "interrupt");
        }
    }

    public static void ObserveVisualSnap(
        Player player,
        float visualYawBefore,
        float visualYawAfter,
        float logicYaw,
        float deltaAngle,
        bool heldByTurn)
    {
        if (!Enabled || player == null || heldByTurn || deltaAngle < VisualSnapDeg)
        {
            return;
        }

        var now = Time.unscaledTime;
        if (now - s_lastVisualSnapUnscaled < VisualSnapThrottleSec)
        {
            return;
        }

        EnsureSession(player);
        s_lastVisualSnapUnscaled = now;
        var inAction = IsActionState(player);
        Log(
            "VISUAL_SNAP",
            $"dVis={deltaAngle:F1} visBefore={visualYawBefore:F1} visAfter={visualYawAfter:F1} " +
            $"logicYaw={logicYaw:F1} inAction={inAction} heldByTurn={heldByTurn}",
            player);

        if (inAction || visLogicDelta(visualYawAfter, logicYaw) >= PoseWarnDeg)
        {
            ObservePoseWarn(player, "visual_snap");
        }
    }

    static void ObservePoseWarn(Player player, string reason)
    {
        if (s_poseWarned)
        {
            return;
        }

        s_poseWarned = true;
        var visLogic = AbsDelta(Yaw(player.LogicForward), VisualYaw(player));
        Log(
            "POSE_WARN",
            $"reason={Safe(reason)} visLogicDelta={visLogic:F1} " +
            $"logicYaw={Yaw(player.LogicForward):F1} visualYaw={VisualYaw(player):F1} " +
            $"inAction={IsActionState(player)}",
            player);
    }

    static void EnsureSession(Player player)
    {
        if (player == s_player)
        {
            return;
        }

        s_player = player;
        s_sessionId++;
        s_eventId = 0;
        s_poseWarned = false;
        s_lastPickKey = null;
        s_lastCommitFrame = -1;
        Log(
            "SESSION_BEGIN",
            $"format=compact-v2 stack=off purpose=DesiredVsCommitted_EntryBasis_ChordWindow player={player.name}",
            player);
    }

    static void Log(string evt, string body, Player player)
    {
        s_eventId++;
        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            player,
            "{0}",
            $"{Prefix} sid={s_sessionId} e={evt} eid={s_eventId} frame={Time.frameCount} t={Time.time:F3} {body}");
    }

    static string StateName(Player player)
    {
        if (player?.States?.Current == null)
        {
            return "NoState";
        }

        return player.States.Current.StateId;
    }

    static bool IsActionState(Player player)
        => StateName(player) == "PlayerActionState";

    static float Yaw(Vector3 forward)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            return 0f;
        }

        return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
    }

    static float VisualYaw(Player player)
        => Yaw(player.VisualRotation * Vector3.forward);

    static float SignedYaw(Vector3 from, Vector3 to)
        => Vector3.SignedAngle(Planar(from), Planar(to), Vector3.up);

    static Vector3 Planar(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }

    static float visLogicDelta(float visualYaw, float logicYaw)
        => AbsDelta(visualYaw, logicYaw);

    static float AbsDelta(float a, float b)
        => Mathf.Abs(Mathf.DeltaAngle(a, b));

    static string Safe(string value)
        => string.IsNullOrEmpty(value) ? "none" : value;
}

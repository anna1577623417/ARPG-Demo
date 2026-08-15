using UnityEngine;

/// <summary>
/// 237 L0–L6 / v3 — 方向意图、Locomotion 双通道、Route Selection Frame 合同探针。行为只读。
/// v3 要证伪：Held 改向是否更新 DesiredTravel/DesiredFacing/Commit；Inspector DirectionalInputFrame 是否进入 Resolver。
/// 边沿 + 短 Burst，无堆栈。Console 过滤：[DIR]
/// </summary>
public static partial class DirectionAuthority237Probe
{
    public const string Prefix = "[DIR]";
    const float FacingCommitDeg = 8f;

    static Player s_player;
    static int s_sessionId;
    static int s_eventId;
    static int s_rid;
    static int s_placeholderToken;
    static bool s_loggedCtxOpen;
    static int s_lastCommitFrame = -1;
    static string s_lastCommitSource;
    static string s_lastMatchKey;
    static string s_lastFacingReqKey;
    static FacingLeaseOwner s_lastVisualAuthOwner = FacingLeaseOwner.None;
    static bool s_loggedPolicyMissing;

    static bool Enabled => GameMainDebugSettings.DirectionAuthority237Log;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_player = null;
        s_sessionId = 0;
        s_eventId = 0;
        s_rid = 0;
        s_placeholderToken = 0;
        s_loggedCtxOpen = false;
        s_lastCommitFrame = -1;
        s_lastCommitSource = null;
        s_lastMatchKey = null;
        s_lastFacingReqKey = null;
        s_lastVisualAuthOwner = FacingLeaseOwner.None;
        s_loggedPolicyMissing = false;
        ResetV3Statics();
    }

    public static void ObserveDown(
        Player player,
        Vector2 rawMove,
        Vector3 worldDir,
        Vector3 captureForward,
        Vector3 desiredFacing,
        Vector3 committedFacing,
        int token)
    {
        if (!Enabled || player == null)
        {
            return;
        }

        EnsureSession(player);
        s_rid++;
        s_placeholderToken = token > 0 ? token : s_placeholderToken + 1;
        BeginBurst();
        var captureYaw = Yaw(captureForward);
        var desiredYaw = Yaw(desiredFacing);
        var committedYaw = Yaw(committedFacing);
        var same = Mathf.Abs(SignedYaw(desiredFacing, committedFacing)) < 0.5f;
        Log(
            "DOWN",
            $"token={s_placeholderToken} event=Down held={HeldMask(rawMove)} " +
            $"prevRaw=(0.00,0.00) raw=({rawMove.x:F2},{rawMove.y:F2}) worldYaw={Yaw(worldDir):F1} " +
            $"captureFacingYaw={captureYaw:F1} visualYaw={Yaw(player.PresentationFacing):F1} " +
            $"desiredYaw={desiredYaw:F1} committedYaw={committedYaw:F1} " +
            $"desiredEqualsCommitted={same} gateOpen=True",
            player);
        ObserveDirCapture(player, captureForward);
        ObserveDirSnapshot(player, rawMove, worldDir, "MoveDown");
    }

    public static void ObserveIntent(Player player, int token)
    {
        if (!Enabled || player == null)
        {
            return;
        }

        EnsureSession(player);
        s_placeholderToken = token > 0 ? token : s_placeholderToken;
        Log(
            "INTENT",
            $"token={s_placeholderToken} desiredYaw={Yaw(player.DesiredFacing):F1} " +
            $"committedYaw={Yaw(player.LogicForward):F1} clock=unscaled tUnscaledSrc={InputClock.UnscaledNow:F3} " +
            $"state={StateName(player)}",
            player);
    }

    public static void ObserveCtxOpen(Player player, in DirectionalTimingSnapshot timing)
    {
        if (!Enabled || player == null || s_loggedCtxOpen)
        {
            return;
        }

        EnsureSession(player);
        s_loggedCtxOpen = true;
        Log(
            "CTX_OPEN",
            $"token={s_placeholderToken} pre={timing.PreTriggerWindowSec:F3} " +
            $"delay={timing.FacingCommitDelaySec:F3} post={timing.PostTriggerWindowSec:F3} " +
            $"release={timing.ReleaseGraceSec:F3} turnTapMax={timing.TurnTapMaxDurationSec:F3} " +
            $"redirectMin={timing.RedirectFacingMinDeltaDeg:F1} clock=unscaled",
            player);
    }

    public static void ObserveGateOpen(Player player, int token, float delaySec)
    {
        if (!Enabled || player == null)
        {
            return;
        }

        EnsureSession(player);
        s_placeholderToken = token > 0 ? token : s_placeholderToken;
        Log(
            "GATE_OPEN",
            $"token={s_placeholderToken} delay={delaySec:F3} committedUnchanged=True " +
            $"desiredYaw={Yaw(player.DesiredFacing):F1} committedYaw={Yaw(player.LogicForward):F1} " +
            $"age=0.000 clock=unscaled state={StateName(player)}",
            player);
    }

    public static void ObserveDelayClamp(Player player, float requested, float clamped)
    {
        if (!Enabled || player == null)
        {
            return;
        }

        EnsureSession(player);
        Debug.LogFormat(
            LogType.Warning,
            LogOption.NoStacktrace,
            player,
            "{0}",
            $"{Prefix} sid={s_sessionId} e=GATE_DELAY_CLAMP eid={++s_eventId} frame={Time.frameCount} " +
            $"tUnscaled={InputClock.UnscaledNow:F3} requested={requested:F3} clamped={clamped:F3}");
    }

    public static void ObserveFacingCommit(Player player, Vector3 prev, Vector3 next, string source)
    {
        if (!Enabled || player == null)
        {
            return;
        }

        var delta = SignedYaw(prev, next);
        if (Mathf.Abs(delta) < FacingCommitDeg)
        {
            return;
        }

        if (Time.frameCount == s_lastCommitFrame
            && string.Equals(s_lastCommitSource, source, System.StringComparison.Ordinal))
        {
            return;
        }

        EnsureSession(player);
        s_lastCommitFrame = Time.frameCount;
        s_lastCommitSource = source ?? "unknown";
        Log(
            "FACING_COMMIT",
            $"token={s_placeholderToken} policy={CommitPolicy(source)} source={Safe(source)} " +
            $"dYaw={delta:F1} prev={Yaw(prev):F1} next={Yaw(next):F1} " +
            $"desiredYaw={Yaw(next):F1} committedYaw={Yaw(next):F1} " +
            $"state={StateName(player)}",
            player);
    }

    public static void ObserveCtxMatch(
        Player player,
        SkillEntrySlot slot,
        SkillGroupDefinition group,
        in DirectionalContextResult context,
        SkillRouteDefinition route,
        bool isMotionMode)
    {
        if (!Enabled || player == null || !context.Success)
        {
            return;
        }

        var groupName = group != null ? group.name : "null";
        var routeName = route != null ? route.name : "null";
        var key = $"{context.Token}|{slot}|{groupName}|{context.Mode}|{context.Slot}|{routeName}";
        if (string.Equals(key, s_lastMatchKey, System.StringComparison.Ordinal))
        {
            return;
        }

        EnsureSession(player);
        s_lastMatchKey = key;
        s_placeholderToken = context.Token > 0 ? context.Token : s_placeholderToken;
        var captureYaw = Yaw(context.BasisFacing);
        var liveYaw = Yaw(player.LogicForward);
        var ageText = context.AgeSec >= 0f ? context.AgeSec.ToString("F3") : "-1";
        ObserveGroupConfig(player, group);
        var configured = group != null ? group.DirectionalInputFrame.ToString() : "null";
        var actualSource = ResolveActualFrameSource(group, isMotionMode, context.Mode);
        var snapshotSource = ResolveSnapshotSource(isMotionMode, context.Mode);
        BeginBurst();
        Log(
            "DIR_ROUTE_PICK",
            $"legacy=CTX_MATCH token={s_placeholderToken} slot={slot} group={groupName} mode={context.Mode} " +
            $"configuredFrame={configured} actualFrameSource={actualSource} snapshotOrCurrent={snapshotSource} " +
            $"raw=({context.Axis.x:F2},{context.Axis.y:F2}) " +
            $"cameraSlot={context.Slot} logicSlot={context.Slot} reframe=False " +
            $"resolvedSlot={context.Slot} route={routeName} age={ageText} " +
            $"frameYaw={captureYaw:F1} captureYaw={captureYaw:F1} liveYaw={liveYaw:F1} " +
            $"cameraYaw={FormatYaw(s_lastCameraYaw)} committedYaw={liveYaw:F1} " +
            $"cameraDeltaDeg={FormatAbsDelta(s_lastCameraYaw, liveYaw)} " +
            $"usedLiveLogic={context.UsedLiveLogic} usedLiveLogicForReframe=False motion={isMotionMode}",
            player);
        ObserveDirSnapshot(
            player,
            context.Axis,
            context.WorldDir,
            "SkillPick",
            resolvedSlot: context.Slot.ToString(),
            selectionFrame: configured);
    }

    public static void ObserveCtxFail(
        Player player,
        SkillEntrySlot slot,
        SkillGroupDefinition group,
        string reason)
    {
        if (!Enabled || player == null)
        {
            return;
        }

        EnsureSession(player);
        var groupName = group != null ? group.name : "null";
        Log(
            "CTX_FAIL",
            $"token={s_placeholderToken} slot={slot} group={groupName} reason={Safe(reason)} usedLiveLogic=False",
            player);
    }

    public static void ObserveClaim(
        Player player,
        int token,
        DirectionTokenOwner owner,
        bool cancelTurn)
    {
        if (!Enabled || player == null)
        {
            return;
        }

        EnsureSession(player);
        s_placeholderToken = token > 0 ? token : s_placeholderToken;
        Log(
            "CLAIM",
            $"token={s_placeholderToken} owner={owner} cancelTurn={cancelTurn} " +
            $"state={StateName(player)}",
            player);
    }

    public static void ObserveClaimFail(Player player, int token, DirectionTokenOwner already)
    {
        if (!Enabled || player == null)
        {
            return;
        }

        EnsureSession(player);
        s_placeholderToken = token > 0 ? token : s_placeholderToken;
        Log(
            "CLAIM_FAIL",
            $"token={s_placeholderToken} already={already} state={StateName(player)}",
            player);
    }

    public static void ObserveClaimOpen(Player player, int token, string reason)
    {
        if (!Enabled || player == null)
        {
            return;
        }

        EnsureSession(player);
        Log(
            "CLAIM_OPEN",
            $"token={token} reason={Safe(reason)} state={StateName(player)}",
            player);
    }

    public static void ObserveFacingReq(
        Player player,
        FacingLeaseOwner owner,
        ActionFacingPolicy policy,
        bool granted,
        string deny,
        string source,
        Vector3 requestedDir = default)
    {
        if (!Enabled || player == null)
        {
            return;
        }

        var denyText = string.IsNullOrEmpty(deny) ? "none" : deny;
        var key = $"{owner}|{policy}|{granted}|{denyText}|{Safe(source)}|{Time.frameCount}";
        if (!granted)
        {
            key = $"{owner}|{denyText}|{Safe(source)}";
        }

        if (string.Equals(key, s_lastFacingReqKey, System.StringComparison.Ordinal))
        {
            return;
        }

        EnsureSession(player);
        s_lastFacingReqKey = key;
        var policyText = policy == ActionFacingPolicy.PreserveEntryFacing
            ? "PreserveEntry"
            : policy.ToString();
        var reqYaw = requestedDir.sqrMagnitude > 0.0001f
            ? Yaw(requestedDir)
            : Yaw(player.DesiredFacing);
        Log(
            "FACING_REQ",
            $"token={s_placeholderToken} owner={owner} policy={policyText} granted={granted} " +
            $"denied={denyText} source={Safe(source)} requestedYaw={reqYaw:F1} " +
            $"desiredTravelYaw={Yaw(player.DesiredFacing):F1} committedYaw={Yaw(player.LogicForward):F1} " +
            $"visualYaw={Yaw(player.PresentationFacing):F1} gateOpen={player.FacingCommit != null && player.FacingCommit.IsPending} " +
            $"lease={owner}",
            player);
    }

    public static void ObserveVisualAuth(Player player, FacingLeaseOwner owner)
    {
        if (!Enabled || player == null)
        {
            return;
        }

        if (owner == s_lastVisualAuthOwner)
        {
            return;
        }

        EnsureSession(player);
        s_lastVisualAuthOwner = owner;
        Log(
            "VISUAL_AUTH",
            $"token={s_placeholderToken} owner={owner} " +
            $"presentationYaw={Yaw(player.PresentationFacing):F1} committedYaw={Yaw(player.LogicForward):F1} " +
            $"state={StateName(player)}",
            player);
    }

    public static void ObserveAuthorityMissing(Player player, string source)
    {
        EnsureSession(player);
        Debug.LogFormat(
            LogType.Error,
            LogOption.NoStacktrace,
            player,
            "{0}",
            $"{Prefix} sid={s_sessionId} e=AUTHORITY_MISSING eid={++s_eventId} frame={Time.frameCount} " +
            $"tUnscaled={InputClock.UnscaledNow:F3} source={Safe(source)}");
    }

    public static void ObservePolicyMissing(Player player)
    {
        if (!Enabled || player == null || s_loggedPolicyMissing)
        {
            return;
        }

        s_loggedPolicyMissing = true;
        EnsureSession(player);
        Debug.LogFormat(
            LogType.Warning,
            LogOption.NoStacktrace,
            player,
            "{0}",
            $"{Prefix} sid={s_sessionId} e=POLICY_MISSING eid={++s_eventId} frame={Time.frameCount} " +
            $"tUnscaled={InputClock.UnscaledNow:F3} fallback=PreserveEntry");
    }

    public static void ObserveMotionFrame(Player player, in MotionFrameSnapshot frame)
    {
        if (!Enabled || player == null || !frame.IsValid)
        {
            return;
        }

        EnsureSession(player);
        ResetMotionSteps();
        Log(
            "MOTION_BEGIN",
            $"legacy=MOTION_FRAME token={s_placeholderToken} fwd=entry frozen={frame.Frozen} space={frame.Space} " +
            $"basisYaw={Yaw(frame.Forward):F1} yaw={Yaw(frame.Forward):F1} committedYaw={Yaw(player.LogicForward):F1} " +
            $"desiredYaw={Yaw(player.DesiredFacing):F1}",
            player);
    }

    public static void ObserveMotionFail(Player player, string reason)
    {
        if (player == null)
        {
            return;
        }

        EnsureSession(player);
        Debug.LogFormat(
            LogType.Warning,
            LogOption.NoStacktrace,
            player,
            "{0}",
            $"{Prefix} sid={s_sessionId} e=MOTION_FAIL eid={++s_eventId} frame={Time.frameCount} " +
            $"tUnscaled={InputClock.UnscaledNow:F3} reason={Safe(reason)} state={StateName(player)}");
    }

    public static void ObserveMotionFrameReplace(
        Player player,
        in MotionFrameSnapshot previous,
        in MotionFrameSnapshot next)
    {
        if (!Enabled || player == null)
        {
            return;
        }

        EnsureSession(player);
        Log(
            "MOTION_FRAME_REPLACE",
            $"token={s_placeholderToken} prevYaw={Yaw(previous.Forward):F1} nextYaw={Yaw(next.Forward):F1} " +
            $"state={StateName(player)}",
            player);
    }

    public static void ResetMatchKey()
    {
        s_lastMatchKey = null;
    }

    public static void ObserveActionEnd(
        Player player,
        bool leftoverCue,
        DirectionTokenOwner claimed)
    {
        s_lastMatchKey = null;
        if (!Enabled || player == null)
        {
            return;
        }

        EnsureSession(player);
        Log(
            "ACTION_END",
            $"token={s_placeholderToken} leftoverCue={leftoverCue} claimed={claimed} " +
            $"state={StateName(player)}",
            player);
    }

    public static void ObserveTurnCue(Player player, in TurnCompensationCue cue, string reason)
    {
        if (!Enabled || player == null || !cue.IsTurning)
        {
            return;
        }

        EnsureSession(player);
        Log(
            "TURN_CUE",
            $"token={s_placeholderToken} reason={Safe(reason)} type={cue.Type} gen={cue.Generation} " +
            $"signed={cue.SignedAngle:F1} abs={cue.AbsAngle:F1} " +
            $"logicYaw={Yaw(player.LogicForward):F1}",
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
        s_rid = 0;
        s_placeholderToken = 0;
        s_loggedCtxOpen = false;
        s_lastMatchKey = null;
        s_lastFacingReqKey = null;
        s_lastVisualAuthOwner = FacingLeaseOwner.None;
        s_lastCommitFrame = -1;
        ResetV3Statics();
        Log(
            "SESSION_BEGIN",
            $"format=dir-v3 stack=off purpose=LocomotionDualChannel_RouteSelectionFrame player={player.name}",
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
            $"{Prefix} sid={s_sessionId} rid={s_rid} e={evt} eid={s_eventId} frame={Time.frameCount} " +
            $"tUnscaled={InputClock.UnscaledNow:F3} phase={StateName(player)} action={ActionName(player)} {body}");
    }

    static string CommitPolicy(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return "Other";
        }

        if (source.IndexOf("FacingCommitGate", System.StringComparison.Ordinal) >= 0
            || source.IndexOf("GateExpire", System.StringComparison.Ordinal) >= 0)
        {
            return "GateExpire";
        }

        if (source.IndexOf("ActionFacing", System.StringComparison.Ordinal) >= 0
            || source.IndexOf("ActionEnter", System.StringComparison.Ordinal) >= 0
            || source.IndexOf("PendingFacing", System.StringComparison.Ordinal) >= 0)
        {
            return "ActionLease";
        }

        if (source.IndexOf("ActionExit.Held", System.StringComparison.Ordinal) >= 0)
        {
            return "ActionExitHeld";
        }

        if (source.IndexOf("ImmediateCommit", System.StringComparison.Ordinal) >= 0)
        {
            return "Immediate";
        }

        if (source.IndexOf("FreeImmediate", System.StringComparison.Ordinal) >= 0)
        {
            return "FreeImmediate";
        }

        return "Other";
    }

    static string StateName(Player player)
        => player?.States?.Current == null ? "NoState" : player.States.Current.StateId;

    static float Yaw(Vector3 forward)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            return 0f;
        }

        return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
    }

    static float SignedYaw(Vector3 from, Vector3 to)
        => Vector3.SignedAngle(Planar(from), Planar(to), Vector3.up);

    static Vector3 Planar(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }

    static string Safe(string value)
        => string.IsNullOrEmpty(value) ? "none" : value;
}

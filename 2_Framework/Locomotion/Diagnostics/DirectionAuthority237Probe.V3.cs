using UnityEngine;

/// <summary>
/// 237 v3 — Locomotion 连续通道 / Route Selection Frame / Burst 采样。与主探针同开关、同前缀 [DIR]。
/// </summary>
public static partial class DirectionAuthority237Probe
{
    const float RawChange = 0.35f;
    const float TravelChangeDeg = 12f;
    const float VisualWriteDeg = 8f;
    const int BurstFrames = 20;
    const int MotionStepMax = 5;

    static Vector2 s_lastRaw;
    static float s_lastTravelYaw;
    static int s_burstUntilFrame = -1;
    static int s_lastLocoSampleFrame = -1;
    static int s_motionSteps;
    static string s_lastConfigKey;
    static string s_lastChangeKey;
    static float s_lastVisualWriteYaw = float.NaN;
    static float s_lastCameraYaw = float.NaN;

    static void ResetV3Statics()
    {
        s_lastRaw = Vector2.zero;
        s_lastTravelYaw = 0f;
        s_burstUntilFrame = -1;
        s_lastLocoSampleFrame = -1;
        s_motionSteps = 0;
        s_lastConfigKey = null;
        s_lastChangeKey = null;
        s_lastVisualWriteYaw = float.NaN;
        s_lastCameraYaw = float.NaN;
    }

    static void BeginBurst()
    {
        s_burstUntilFrame = Time.frameCount + BurstFrames;
    }

    static bool InBurst => Time.frameCount <= s_burstUntilFrame;

    /// <summary>PlayerController 每帧 Move 采样。只读。Down 已由 ObserveDown 打点。</summary>
    public static void ObserveMoveTick(
        Player player,
        Vector2 raw,
        Vector3 worldDir,
        bool moveWasActive,
        bool moveActive,
        float cameraYaw)
    {
        if (!Enabled || player == null)
        {
            return;
        }

        EnsureSession(player);
        s_lastCameraYaw = cameraYaw;

        if (moveWasActive && !moveActive)
        {
            BeginBurst();
            Log(
                "INPUT_EDGE",
                $"event=Up token={s_placeholderToken} held=none prevRaw=({s_lastRaw.x:F2},{s_lastRaw.y:F2}) " +
                $"raw=(0.00,0.00) inputZero=True gateOpen={GateOpen(player)}",
                player);
            ObserveLocoTransition(
                player,
                s_lastRaw,
                Vector2.zero,
                s_lastTravelYaw,
                0f,
                inputZero: true,
                classification: "StopCandidate",
                reason: "MoveIntentZero");
            ObserveDirSnapshot(player, Vector2.zero, player.DesiredFacing, "MoveUp");
            s_lastRaw = Vector2.zero;
            return;
        }

        if (!moveActive)
        {
            return;
        }

        var travelYaw = Yaw(worldDir);
        if (!moveWasActive && moveActive)
        {
            ObserveLocoTransition(
                player,
                Vector2.zero,
                raw,
                s_lastTravelYaw,
                travelYaw,
                inputZero: false,
                classification: "StartCandidate",
                reason: "MoveIntentAppear");
            s_lastRaw = raw;
            s_lastTravelYaw = travelYaw;
            if (InBurst)
            {
                ObserveLocoSample(player, raw, worldDir);
            }

            return;
        }

        var rawChanged = Vector2.Distance(raw, s_lastRaw) >= RawChange;
        var travelChanged = Mathf.Abs(Mathf.DeltaAngle(s_lastTravelYaw, travelYaw)) >= TravelChangeDeg;
        if (rawChanged || travelChanged)
        {
            var changeKey = $"{HeldMask(s_lastRaw)}>{HeldMask(raw)}|{Time.frameCount}";
            if (!string.Equals(changeKey, s_lastChangeKey, System.StringComparison.Ordinal))
            {
                s_lastChangeKey = changeKey;
                BeginBurst();
                var holdDur = player.InputContext != null
                    ? player.InputContext.MoveHoldDurationSec(Time.time)
                    : 0f;
                Log(
                    "INPUT_EDGE",
                    $"event=Change token={s_placeholderToken} held={HeldMask(raw)} " +
                    $"prevRaw=({s_lastRaw.x:F2},{s_lastRaw.y:F2}) raw=({raw.x:F2},{raw.y:F2}) " +
                    $"heldDur={holdDur:F3} newDown=False gateOpen={GateOpen(player)}",
                    player);
                ObserveLocoChange(player, s_lastRaw, raw, s_lastTravelYaw, travelYaw);
                var inAction = player.States != null && player.States.Current is PlayerActionState;
                ObserveLocoTransition(
                    player,
                    s_lastRaw,
                    raw,
                    s_lastTravelYaw,
                    travelYaw,
                    inputZero: false,
                    classification: inAction ? "ActionHoldRedirect" : "RedirectCandidate",
                    reason: GateOpen(player)
                        ? "RefreshPendingGateDesired"
                        : "HoldRedirectWithoutNewDown");
                ObserveDirSnapshot(player, raw, worldDir, "HoldRedirect");
            }
        }

        s_lastRaw = raw;
        s_lastTravelYaw = travelYaw;
        if (InBurst)
        {
            ObserveLocoSample(player, raw, worldDir);
        }
    }

    public static void ObserveActionEntry(
        Player player,
        ActionDataSO action,
        SkillGroupDefinition group,
        in MotionFrameSnapshot frame)
    {
        if (!Enabled || player == null)
        {
            return;
        }

        EnsureSession(player);
        BeginBurst();
        ObserveGroupConfig(player, group);
        var authored = action != null ? action.FacingPolicy : ActionFacingPolicy.PreserveEntryFacing;
        var resolution = player.LastActionFacingResolution;
        if (resolution.ActionPolicy != authored)
        {
            resolution = ActionFacingPolicyResolver.Resolve(authored, FacingPolicyGameplayContext.Unwired);
        }

        var slot = DirectionalRouteType.Forward;
        var routeName = "none";
        if (player.TryGetFrozenDirectionalEntry(out var entry) && entry.IsValid)
        {
            slot = entry.Slot;
            routeName = entry.Route != null ? entry.Route.name : "none";
        }

        var requestedFacing = resolution.EffectivePolicy == ActionFacingPolicy.FaceMotionAtEntry
            ? frame.Forward
            : player.LogicForward;
        Log(
            "ACTION_ENTRY",
            $"action={Safe(action != null ? action.name : null)} route={routeName} resolvedSlot={slot} " +
            $"facingPolicy={PolicyText(authored)} effectivePolicy={PolicyText(resolution.EffectivePolicy)} " +
            $"motionBasisPolicy={frame.Space} " +
            $"entryCommittedYaw={Yaw(player.LogicForward):F1} entryVisualYaw={Yaw(player.PresentationFacing):F1} " +
            $"desiredFacingYaw={Yaw(player.DesiredFacing):F1} motionForwardYaw={Yaw(frame.Forward):F1} " +
            $"requestedActionFacingYaw={Yaw(requestedFacing):F1} owner={Lease(player)}",
            player);
        ObserveDirSnapshot(player, s_lastRaw, player.DesiredFacing, "ActionEntry");
    }

    public static void ObserveMotionStep(Player player, Vector3 localDelta, Vector3 worldDelta)
    {
        if (!Enabled || player == null || s_motionSteps >= MotionStepMax)
        {
            return;
        }

        s_motionSteps++;
        EnsureSession(player);
        Log(
            "MOTION_STEP",
            $"i={s_motionSteps} localF={localDelta.z:F3} localL={localDelta.x:F3} localV={localDelta.y:F3} " +
            $"worldDeltaYaw={Yaw(worldDelta):F1} rootYaw={Yaw(player.LogicForward):F1} " +
            $"visualYaw={Yaw(player.PresentationFacing):F1}",
            player);
    }

    public static void ObserveVisualWrite(
        Player player,
        float beforeYaw,
        float afterYaw,
        float targetYaw,
        FacingLeaseOwner owner,
        bool inAction)
    {
        if (!Enabled || player == null)
        {
            return;
        }

        var delta = Mathf.DeltaAngle(beforeYaw, afterYaw);
        if (Mathf.Abs(delta) < VisualWriteDeg)
        {
            return;
        }

        if (!float.IsNaN(s_lastVisualWriteYaw)
            && Mathf.Abs(Mathf.DeltaAngle(s_lastVisualWriteYaw, afterYaw)) < 0.5f
            && Time.frameCount == s_lastLocoSampleFrame)
        {
            return;
        }

        s_lastVisualWriteYaw = afterYaw;
        EnsureSession(player);
        Log(
            "VISUAL_WRITE",
            $"owner={owner} policy=PresentationFacing beforeYaw={beforeYaw:F1} targetYaw={targetYaw:F1} " +
            $"afterYaw={afterYaw:F1} delta={delta:F1} mode=Snap writerSource=VisualFacingDriver inAction={inAction}",
            player);
    }

    static void ObserveLocoSample(Player player, Vector2 raw, Vector3 worldDir)
    {
        if (Time.frameCount == s_lastLocoSampleFrame)
        {
            return;
        }

        s_lastLocoSampleFrame = Time.frameCount;
        var vel = player.PlanarVelocity;
        vel.y = 0f;
        Log(
            "LOCO_SAMPLE",
            $"raw=({raw.x:F2},{raw.y:F2}) held={HeldMask(raw)} moveFrame=CameraRelative " +
            $"desiredTravelYaw={Yaw(worldDir):F1} actualTravelYaw={Yaw(vel):F1} " +
            $"desiredFacingYaw={Yaw(player.DesiredFacing):F1} committedFacingYaw={Yaw(player.LogicForward):F1} " +
            $"visualYaw={Yaw(player.PresentationFacing):F1} speed={vel.magnitude:F2} " +
            $"gateOpen={GateOpen(player)} lease={Lease(player)}",
            player);
    }

    static void ObserveLocoChange(
        Player player,
        Vector2 fromRaw,
        Vector2 toRaw,
        float fromTravelYaw,
        float toTravelYaw)
    {
        var vel = player.PlanarVelocity;
        vel.y = 0f;
        Log(
            "LOCO_CHANGE",
            $"fromRaw=({fromRaw.x:F2},{fromRaw.y:F2}) toRaw=({toRaw.x:F2},{toRaw.y:F2}) " +
            $"fromDesiredTravelYaw={fromTravelYaw:F1} toDesiredTravelYaw={toTravelYaw:F1} " +
            $"travelDeltaAngle={Mathf.DeltaAngle(fromTravelYaw, toTravelYaw):F1} " +
            $"facingDeltaAngle={SignedYaw(player.LogicForward, player.DesiredFacing):F1} " +
            $"actualTravelYaw={Yaw(vel):F1} speed={vel.magnitude:F2} inputZero=False",
            player);
    }

    static void ObserveLocoTransition(
        Player player,
        Vector2 rawBefore,
        Vector2 rawAfter,
        float fromTravelYaw,
        float toTravelYaw,
        bool inputZero,
        string classification,
        string reason)
    {
        var vel = player.PlanarVelocity;
        Log(
            "LOCO_TRANSITION",
            $"fromSemantic={StateName(player)} toSemantic={classification} classification={classification} " +
            $"rawBefore=({rawBefore.x:F2},{rawBefore.y:F2}) rawAfter=({rawAfter.x:F2},{rawAfter.y:F2}) " +
            $"inputZero={inputZero} travelDeltaAngle={Mathf.DeltaAngle(fromTravelYaw, toTravelYaw):F1} " +
            $"speed={vel.magnitude:F2} reason={Safe(reason)} gateOpen={GateOpen(player)}",
            player);
    }

    static void ObserveDirCapture(Player player, Vector3 captureForward)
    {
        var cam = Camera.main;
        var camYaw = 0f;
        if (cam != null)
        {
            var fwd = cam.transform.forward;
            camYaw = Yaw(fwd);
        }

        Log(
            "DIR_CAPTURE",
            $"characterCommittedYaw={Yaw(player.LogicForward):F1} characterVisualYaw={Yaw(player.PresentationFacing):F1} " +
            $"cameraForwardYaw={camYaw:F1} captureFacingYaw={Yaw(captureForward):F1} " +
            $"desiredFacingYaw={Yaw(player.DesiredFacing):F1} lock=none",
            player);
    }

    static void ObserveDirSnapshot(
        Player player,
        Vector2 raw,
        Vector3 travelDir,
        string reason,
        string resolvedSlot = null,
        string selectionFrame = null)
    {
        var vel = player.PlanarVelocity;
        vel.y = 0f;
        var slot = resolvedSlot;
        var frame = selectionFrame;
        if (string.IsNullOrEmpty(slot) || string.IsNullOrEmpty(frame))
        {
            if (player.TryGetFrozenDirectionalEntry(out var entry) && entry.IsValid)
            {
                if (string.IsNullOrEmpty(slot))
                {
                    slot = entry.Slot.ToString();
                }

                if (string.IsNullOrEmpty(frame))
                {
                    frame = entry.Group != null ? entry.Group.DirectionalInputFrame.ToString() : "none";
                }
            }
        }

        if (string.IsNullOrEmpty(slot))
        {
            slot = "none";
        }

        if (string.IsNullOrEmpty(frame))
        {
            frame = "none";
        }

        var ctx = player.LocomotionRuntime;
        var snapRaw = ctx.Raw.sqrMagnitude > 0.0001f ? ctx.Raw : raw;
        var travel = ctx.DesiredTravel.sqrMagnitude > 0.0001f ? ctx.DesiredTravel : travelDir;
        var actual = ctx.ActualTravel.sqrMagnitude > 0.0001f ? ctx.ActualTravel : vel;
        var desiredFacing = ctx.DesiredFacing.sqrMagnitude > 0.0001f ? ctx.DesiredFacing : player.DesiredFacing;
        var committed = ctx.CommittedFacing.sqrMagnitude > 0.0001f ? ctx.CommittedFacing : player.LogicForward;
        Log(
            "DIR_SNAPSHOT",
            $"reason={Safe(reason)} raw=({snapRaw.x:F2},{snapRaw.y:F2}) held={HeldMask(snapRaw)} " +
            $"desiredTravelYaw={Yaw(travel):F1} actualTravelYaw={Yaw(actual):F1} " +
            $"desiredFacingYaw={Yaw(desiredFacing):F1} committedFacingYaw={Yaw(committed):F1} " +
            $"visualFacingYaw={Yaw(player.PresentationFacing):F1} selectionFrame={frame} resolvedSlot={slot} " +
            $"facingOwner={Lease(player)} gateOpen={GateOpen(player)}",
            player);
    }

    static void ObserveGroupConfig(Player player, SkillGroupDefinition group)
    {
        if (group == null)
        {
            return;
        }

        var key = $"{group.name}|{group.DirectionalInputFrame}";
        if (string.Equals(key, s_lastConfigKey, System.StringComparison.Ordinal))
        {
            return;
        }

        s_lastConfigKey = key;
        var timing = player.ResolveDirectionalTiming();
        var chordSource = ResolveActualFrameSource(group, false, DirectionalContextMode.RecentChord);
        Log(
            "CONFIG",
            $"group={group.name} configuredDirectionalInputFrame={group.DirectionalInputFrame} " +
            $"runtimeChordSource={chordSource} " +
            $"neutralFallback={group.UseFallbackOnNeutral} " +
            $"motionForwardRoute={(group.MotionForwardRoute != null ? group.MotionForwardRoute.name : "null")} " +
            $"pre={timing.PreTriggerWindowSec:F3} delay={timing.FacingCommitDelaySec:F3} " +
            $"post={timing.PostTriggerWindowSec:F3} redirectMin={timing.RedirectFacingMinDeltaDeg:F1} " +
            $"CharacterStick=CollapsedToScreenStickRaw WorldStick=CollapsedToScreenStickRaw codeStatic=True playSlotCollapse=WorldStickLargeDelta CharacterStick90=Uncovered",
            player);
    }

    static string ResolveActualFrameSource(
        SkillGroupDefinition group,
        bool isMotionMode,
        DirectionalContextMode mode)
    {
        if (isMotionMode || mode == DirectionalContextMode.MotionForward)
        {
            return "ChordWindow_MotionForward_BypassesInputFrame";
        }

        var frame = group != null ? group.DirectionalInputFrame : DirectionalInputFrame.BodyFixed;
        switch (frame)
        {
            case DirectionalInputFrame.LogicProjected:
                return mode == DirectionalContextMode.RecentChord
                    ? "SnapshotBasisFacing"
                    : "CurrentDesiredFacing";
#pragma warning disable CS0618
            case DirectionalInputFrame.CharacterStick:
            case DirectionalInputFrame.WorldStick:
#pragma warning restore CS0618
                return "CollapsedToScreenStickRaw";
            default:
                return "ScreenStickRaw";
        }
    }

    static string ResolveSnapshotSource(bool isMotionMode, DirectionalContextMode mode)
    {
        if (isMotionMode || mode == DirectionalContextMode.MotionForward)
        {
            return "ChordWindowExpired";
        }

        switch (mode)
        {
            case DirectionalContextMode.RecentChord:
                return "DirectionDown";
            case DirectionalContextMode.HeldMovement:
                return "TriggerCurrent";
            default:
                return mode.ToString();
        }
    }

    static string FormatYaw(float yaw) => float.IsNaN(yaw) ? "nan" : yaw.ToString("F1");

    static string FormatAbsDelta(float a, float b)
    {
        if (float.IsNaN(a) || float.IsNaN(b))
        {
            return "nan";
        }

        return Mathf.Abs(Mathf.DeltaAngle(a, b)).ToString("F1");
    }

    static void ResetMotionSteps() => s_motionSteps = 0;

    static bool GateOpen(Player player)
        => player.FacingCommit != null && player.FacingCommit.IsPending;

    static FacingLeaseOwner Lease(Player player)
        => player.Orientation != null ? player.Orientation.LeaseOwner : FacingLeaseOwner.None;

    static string ActionName(Player player)
    {
        if (player?.States?.Current is PlayerActionState actionState
            && actionState.TryGetInterruptProbe(out var action, out _))
        {
            return action != null ? action.name : "Action";
        }

        return "none";
    }

    static string PolicyText(ActionFacingPolicy policy)
        => policy == ActionFacingPolicy.PreserveEntryFacing ? "PreserveEntry" : policy.ToString();

    static string HeldMask(Vector2 raw)
    {
        var w = raw.y > 0.3f;
        var a = raw.x < -0.3f;
        var s = raw.y < -0.3f;
        var d = raw.x > 0.3f;
        if (!w && !a && !s && !d)
        {
            return "none";
        }

        var mask = string.Empty;
        if (w) mask += "W";
        if (a) mask += mask.Length == 0 ? "A" : "+A";
        if (s) mask += mask.Length == 0 ? "S" : "+S";
        if (d) mask += mask.Length == 0 ? "D" : "+D";
        return mask;
    }
}

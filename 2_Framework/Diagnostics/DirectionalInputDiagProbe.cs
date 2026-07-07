using UnityEngine;

/// <summary>
/// 213.1 — 八向输入（Space / Shift + WASD）全链路诊断。
/// Console 过滤：<c>[DirInput213]</c>
/// 开关：GameMain → Debug → Log Settings → Directional Input Diag
/// </summary>
public static class DirectionalInputDiagProbe
{
    public const string Prefix = "[DirInput213]";

    public static bool IsEnabled => GameMainDebugSettings.DirectionalInputDiagLog;

    static int s_pulseIndex;

    public static int CurrentPulseIndex => s_pulseIndex;

    public static DirectionalRouteType LastResolvedDir { get; private set; }

    static bool s_lastPulseReframed;

    public static void NotifyResolvedDir(DirectionalRouteType dir) => LastResolvedDir = dir;

    public static void NotifyAbilityPulse(SkillEntrySlot slot)
    {
        if (slot == SkillEntrySlot.Space || slot == SkillEntrySlot.Shift)
        {
            s_pulseIndex++;
            s_lastPulseReframed = false;
        }
    }

    public static void LogChordReframe(
        DirectionalRouteType cameraSlot,
        DirectionalRouteType characterSlot,
        MotionSpace motionBasis)
    {
        if (!IsEnabled)
        {
            return;
        }

        s_lastPulseReframed = true;
        Debug.Log(
            $"{Prefix} STAGE=REFRAME pulse#{s_pulseIndex} " +
            $"camera={cameraSlot} → character={characterSlot} motionBasis={motionBasis}");
    }

    public static void LogDispatch(
        SkillEntrySlot slot,
        Vector2 bufferAxis,
        bool bufferValid,
        Vector2 liveMove,
        bool ctxMoveActive,
        float ctxHoldDur,
        bool ctxCommitted,
        string branchNote)
    {
        if (!IsEnabled)
        {
            return;
        }

        Debug.Log(
            $"{Prefix} STAGE=DISPATCH pulse#{s_pulseIndex} slot={slot} " +
            $"buf=({bufferAxis.x:F2},{bufferAxis.y:F2}) bufValid={bufferValid} " +
            $"live=({liveMove.x:F2},{liveMove.y:F2}) ctxActive={ctxMoveActive} " +
            $"ctxHold={ctxHoldDur:F3}s committed={ctxCommitted} {branchNote}");
    }

    public static void LogSemantic(
        SkillEntrySlot slot,
        InputSemanticType semantic,
        Vector2 moveBuffered,
        bool moveBufferValid)
    {
        if (!IsEnabled)
        {
            return;
        }

        var chord = moveBufferValid ? InputChordResolver.Resolve(moveBuffered) : DirectionalRouteType.Forward;
        Debug.Log(
            $"{Prefix} STAGE=SEMANTIC pulse#{s_pulseIndex} slot={slot} semantic={semantic} " +
            $"moveBuf=({moveBuffered.x:F2},{moveBuffered.y:F2}) bufValid={moveBufferValid} chord={chord}");
    }

    public static void LogCommit(
        Vector3 committedForward,
        Vector3 capturedForward,
        Vector3 liveForward,
        float holdDur,
        float chordWin,
        string commitSource)
    {
        if (!IsEnabled)
        {
            return;
        }

        var angleCapLive = Vector3.Angle(
            Planarize(capturedForward),
            Planarize(liveForward));
        var angleCommitLive = Vector3.Angle(
            Planarize(committedForward),
            Planarize(liveForward));

        Debug.Log(
            $"{Prefix} STAGE=COMMIT pulse#{s_pulseIndex} fwd=({committedForward.x:F2},{committedForward.z:F2}) " +
            $"source={commitSource} captured=({capturedForward.x:F2},{capturedForward.z:F2}) " +
            $"live=({liveForward.x:F2},{liveForward.z:F2}) holdDur={holdDur:F3}s chordWin={chordWin:F2}s " +
            $"angle(cap,live)={angleCapLive:F1}° angle(commit,live)={angleCommitLive:F1}°");

        if (angleCommitLive > 45f)
        {
            LogWarn("COMMIT_LIVE_DRIFT",
                $"commit/live 偏差 {angleCommitLive:F0}° — Motion 基底可能与当前朝向不一致");
        }
    }

    public static void LogMode(
        float now,
        Vector2 pulseAxis,
        float ctxHoldDur,
        float chordWin,
        float motionWin,
        bool isMotionMode,
        string modeLabel,
        bool ctxMoveActive,
        bool ctxCommitted,
        Vector2 liveMove)
    {
        if (!IsEnabled)
        {
            return;
        }

        Debug.Log(
            $"{Prefix} STAGE=MODE pulse#{s_pulseIndex} t={now:F3} pulseAxis=({pulseAxis.x:F2},{pulseAxis.y:F2}) " +
            $"ctxHold={ctxHoldDur:F3}s chordWin={chordWin:F2}s motionWin={motionWin:F2}s " +
            $"→ {modeLabel} isMotion={isMotionMode} ctxActive={ctxMoveActive} committed={ctxCommitted} " +
            $"liveMove=({liveMove.x:F2},{liveMove.y:F2})");

        if (!isMotionMode && ctxMoveActive && ctxHoldDur > chordWin)
        {
            LogWarn("MODE_HOLD_STALE",
                $"ctxHold={ctxHoldDur:F3}s > chordWin 但仍 Chord — 212.2 preserveMoveHold 或边界抖动");
        }

        if (isMotionMode && pulseAxis.sqrMagnitude > 0.0001f)
        {
            var pulseChord = InputChordResolver.Resolve(pulseAxis);
            if (pulseChord != DirectionalRouteType.Forward)
            {
                LogWarn("MODE_AXIS_CONFLICT",
                    $"isMotion=true 但 pulseAxis→{pulseChord} — 可能选 MotionForwardRoute 而非八向槽");
            }
        }
    }

    public static void LogPick(
        Player owner,
        SkillGroupDefinition group,
        InputSemanticType semantic,
        Vector2 pulseAxis,
        bool isMotionMode,
        DirectionalRouteType resolvedDir,
        string routeName,
        float ctxHoldDur,
        float chordWin)
    {
        if (!IsEnabled)
        {
            return;
        }

        var inputFrame = group != null ? group.DirectionalInputFrame : DirectionalInputFrame.BodyFixed;
        var motionBasis = group != null
            ? group.ResolveMotionCurveBasis(null)
            : MotionSpace.CharacterForward;

        Debug.Log(
            $"{Prefix} STAGE=PICK pulse#{s_pulseIndex} semantic={semantic} " +
            $"group={(group != null ? group.name : "(null)")} inputFrame={inputFrame} motionBasis={motionBasis} " +
            $"isMotion={isMotionMode} dir={resolvedDir} route={routeName ?? "(null)"} " +
            $"pulseAxis=({pulseAxis.x:F2},{pulseAxis.y:F2}) ctxHold={ctxHoldDur:F3}s");

        if (semantic != InputSemanticType.Directional && pulseAxis.sqrMagnitude > 0.0001f)
        {
            LogWarn("SHIFT_SEMANTIC_NEUTRAL",
                $"有方向输入但 semantic={semantic} — 易落 FallbackRoute（Shift 常见）");
        }

        if (semantic != InputSemanticType.Directional && pulseAxis.sqrMagnitude < 0.0001f)
        {
            LogWarn("NEUTRAL_FALLBACK",
                "无有效方向轴 — UseFallbackOnNeutral → FallbackRoute");
        }

        if (isMotionMode && group?.MotionForwardRoute != null)
        {
            LogWarn("MOTION_FORWARD_ROUTE",
                $"持续按住 ctxHold>{chordWin:F2}s → MotionForwardRoute，非当前 WASD 八向槽");
        }

        if (owner != null
            && inputFrame == DirectionalInputFrame.BodyFixed
            && motionBasis == MotionSpace.CharacterForward
            && !isMotionMode
            && !s_lastPulseReframed
            && pulseAxis.sqrMagnitude > 0.0001f)
        {
            var stickWorld = owner.ResolveCameraRelativeWorldDirection(pulseAxis);
            var charRight = Vector3.Cross(Vector3.up, owner.LogicForward).normalized;
            var angleStickVsCharRight = Vector3.Angle(stickWorld, charRight);
            if (resolvedDir == DirectionalRouteType.Right && angleStickVsCharRight > 60f)
            {
                LogWarn("BODYFIXED_MOTION_ORTHOGONAL",
                    $"BodyFixed Right 槽 + CharacterForward 基底：屏感右({stickWorld.x:F2},{stickWorld.z:F2}) " +
                    $"与角色右({charRight.x:F2},{charRight.z:F2}) 夹角 {angleStickVsCharRight:F0}° — 位移可能「反/偏」");
            }
        }
    }

    public static void LogPlay(
        Player player,
        ActionDataSO action,
        SkillGroupDefinition group,
        Vector3 burstFaceDir,
        DirectionalRouteType lastResolvedDir)
    {
        if (!IsEnabled || player == null || action == null)
        {
            return;
        }

        var profile = action.MotionProfile;
        var motionBasis = group != null
            ? group.ResolveMotionCurveBasis(profile)
            : profile != null ? profile.MotionSpace : MotionSpace.CharacterForward;
        var logicFwd = player.LogicForward;

        Debug.Log(
            $"{Prefix} STAGE=PLAY pulse#{s_pulseIndex} action={action.name} " +
            $"motionBasis={motionBasis} burstFwd=({burstFaceDir.x:F2},{burstFaceDir.z:F2}) " +
            $"logicFwd=({logicFwd.x:F2},{logicFwd.z:F2}) lastDir={lastResolvedDir} " +
            $"yawPolicy={(profile != null ? profile.YawPolicy.ToString() : "-")}");

        if (profile != null && profile.UsesActionYaw)
        {
            LogWarn("ACTION_YAW",
                "ActionYaw 开启 — 表现层可能先转身再位移，S/Backward 槽易「先转面向镜头再退」");
        }
    }

    public static void LogBufferState(
        float bufferAgeSec,
        Vector2 bufferedMove,
        Vector2 liveMove,
        float bufferMaxAgeSec = InputModifierBuffer.DefaultBufferSeconds)
    {
        if (!IsEnabled)
        {
            return;
        }

        Debug.Log(
            $"{Prefix} STAGE=BUFFER pulse#{s_pulseIndex} bufferAge={bufferAgeSec:F3}s " +
            $"buffered=({bufferedMove.x:F2},{bufferedMove.y:F2}) live=({liveMove.x:F2},{liveMove.y:F2}) " +
            $"maxAge={bufferMaxAgeSec:F2}s");

        if (bufferedMove.sqrMagnitude < 0.0001f && liveMove.sqrMagnitude > 0.0001f)
        {
            LogWarn("BUFFER_MISS_LIVE_OK",
                "ModifierBuffer 已过期但 liveMove 有效 — 依赖 Dispatch liveMoveInput_fallback");
        }

        if (bufferedMove.sqrMagnitude < 0.0001f && liveMove.sqrMagnitude < 0.0001f)
        {
            LogWarn("BUFFER_EMPTY",
                "Buffer 与 liveMove 均为零 — Directional 语义无法成立");
        }
    }

    static void LogWarn(string code, string detail)
    {
        Debug.LogWarning($"{Prefix} WARN={code} pulse#{s_pulseIndex} {detail}");
    }

    static Vector3 Planarize(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }
}

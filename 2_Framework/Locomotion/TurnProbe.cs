using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 184.1 / 162.1 — Turn-In-Place 四向子状态验收探针。
/// <para>Console 过滤：<c>[Turn][Sub]</c>（ENTER / PLAY / EXIT / WARN）。</para>
/// <para>开关：Tools/GameMain/Debug Settings → Turn Sub-State 或 Locomotion。</para>
/// </summary>
public static class TurnProbe
{
    public const string Prefix = "[Turn]";
    public const string SubPrefix = "[Turn][Sub]";

    struct Session
    {
        public int Id;
        public TurnDirection4 Direction;
        public TurnType Type;
        public float EnterTime;
        public bool PlayLogged;
        public string PlayClip;
        public string PlaySource;
    }

    static int s_nextSessionId = 1;
    static readonly Dictionary<int, Session> s_activeByPlayer = new Dictionary<int, Session>(4);
    static readonly Dictionary<int, float> s_nextVisualLagLogTime = new Dictionary<int, float>(4);

    public static bool IsEnabled(Player player) =>
        player != null && (GameMainDebugSettings.TurnSubState || GameMainDebugSettings.Locomotion);

    // ─── 184.1 通用 ───

    public static void LogFacingEdge(Player player, InputTense tense, Vector3 from, Vector3 to, string reason)
    {
        if (!IsEnabled(player))
        {
            return;
        }

        var signed = Vector3.SignedAngle(Planarize(from), Planarize(to), Vector3.up);
        LogNoStack(
            $"{Prefix} tense={tense} {reason} signed={signed:F1}° " +
            $"from=({from.x:F2},{from.z:F2}) to=({to.x:F2},{to.z:F2}) frame={Time.frameCount}",
            player);
    }

    public static void LogTapTurn(Player player, TurnType type, sbyte direction, float absAngle)
    {
        if (!IsEnabled(player))
        {
            return;
        }

        LogNoStack(
            $"{Prefix} Tense=Tap Kind={type} dir={(direction < 0 ? "L" : "R")} abs={absAngle:F1}° fired",
            player);
    }

    public static void LogInterrupt(Player player, string reason)
    {
        if (!IsEnabled(player))
        {
            return;
        }

        LogNoStack($"{Prefix} INTERRUPT reason={reason ?? "intent"} frame={Time.frameCount}", player);
    }

    /// <summary>184.3 — Recovery Action 被主动 Intent 打断。</summary>
    public static void LogRecoveryInterrupt(
        Player player,
        ActionDataSO recoveryAction,
        GameplayIntentKind intentKind,
        ActionCategory incomingCat)
    {
        if (!IsEnabled(player))
        {
            return;
        }

        LogNoStack(
            $"{Prefix} RECOVERY-INTERRUPT action={recoveryAction?.name ?? "?"} " +
            $"intent={intentKind} cat={incomingCat} ⇒ 立刻打断 Recovery → Route " +
            $"frame={Time.frameCount}",
            player);
    }

    /// <summary>184.3 — VisualRoot 大角差缓追（仅 delta≥30° 打一次）。</summary>
    public static void LogVisualLag(Player player, float deltaAngleDeg, float chosenSpeedDeg)
    {
        if (!IsEnabled(player) || deltaAngleDeg < 30f || chosenSpeedDeg <= 0f)
        {
            return;
        }

        var pid = player.GetInstanceID();
        var now = Time.unscaledTime;
        if (s_nextVisualLagLogTime.TryGetValue(pid, out var next) && now < next)
        {
            return;
        }

        s_nextVisualLagLogTime[pid] = now + 0.15f;
        LogNoStack(
            $"{Prefix} VISUAL-LAG delta={deltaAngleDeg:F1}° speed={chosenSpeedDeg:F0}°/s " +
            $"estCatchupTime={deltaAngleDeg / chosenSpeedDeg * 1000f:F0}ms frame={Time.frameCount}",
            player);
    }

    // ─── 四向子状态 ENTER / PLAY / EXIT ───

    /// <summary>Logic 层：TurnResolver 进入 LOCK（四向之一）。</summary>
    public static void LogSubEnter(
        Player player,
        TurnType type,
        sbyte direction,
        float absAngle,
        float signedAngle,
        string enterReason)
    {
        if (!IsEnabled(player))
        {
            return;
        }

        var dir4 = MapDirection4(type, direction);
        var sessionId = s_nextSessionId++;
        s_activeByPlayer[player.GetInstanceID()] = new Session
        {
            Id = sessionId,
            Direction = dir4,
            Type = type,
            EnterTime = Time.unscaledTime,
        };

        LogNoStack(
            $"{SubPrefix} ENTER id={sessionId} dir={dir4} type={type} layer=Logic " +
            $"reason={enterReason} tense={player.CurrentInputTense} " +
            $"abs={absAngle:F1}° signed={signedAngle:F1}° spd={player.PlanarVelocity.magnitude:F2} " +
            $"logicFwd=({player.LogicForward.x:F2},{player.LogicForward.z:F2}) frame={Time.frameCount}",
            player);
    }

    /// <summary>Anim 层：Turn 切片实际 Play。</summary>
    public static void LogSubPlay(
        Player player,
        TurnDirection4 dir4,
        string clipName,
        string source,
        float crossfade,
        float clipLength,
        float speed)
    {
        if (!IsEnabled(player))
        {
            return;
        }

        var pid = player.GetInstanceID();
        var sessionId = 0;
        if (s_activeByPlayer.TryGetValue(pid, out var session))
        {
            sessionId = session.Id;
            session.PlayLogged = true;
            session.PlayClip = clipName;
            session.PlaySource = source;
            s_activeByPlayer[pid] = session;
        }

        LogNoStack(
            $"{SubPrefix} PLAY id={sessionId} dir={dir4} clip={clipName} src={source} " +
            $"xf={crossfade:F3}s len={clipLength:F2}s speed={speed:F2} " +
            $"frame={Time.frameCount}",
            player);
    }

    /// <summary>Logic 层：TurnResolver 解锁。</summary>
    public static void LogSubExitLogic(
        Player player,
        TurnType type,
        sbyte direction,
        string reason,
        float lockDurationSec,
        float absAngleAtExit)
    {
        if (!IsEnabled(player))
        {
            return;
        }

        var dir4 = MapDirection4(type, direction);
        var pid = player.GetInstanceID();
        var sessionId = 0;
        var dt = lockDurationSec;
        var playLogged = false;
        if (s_activeByPlayer.TryGetValue(pid, out var session) && session.Direction == dir4)
        {
            sessionId = session.Id;
            dt = Time.unscaledTime - session.EnterTime;
            playLogged = session.PlayLogged;
            s_activeByPlayer.Remove(pid);
        }

        LogNoStack(
            $"{SubPrefix} EXIT id={sessionId} dir={dir4} layer=Logic reason={reason} " +
            $"dt={dt:F3}s abs={absAngleAtExit:F1}° playLogged={playLogged} frame={Time.frameCount}",
            player);

        if (sessionId > 0 && !playLogged)
        {
            LogNoStack(
                $"{SubPrefix} WARN id={sessionId} dir={dir4} Logic ENTER without PLAY before EXIT reason={reason}",
                player,
                LogType.Warning);
        }
    }

    /// <summary>Anim 层：Locomotion 子状态离开 Turn 切片。</summary>
    public static void LogSubExitAnim(
        Player player,
        TurnDirection4 dir4,
        string fromSub,
        string toSub,
        float crossfade,
        string reason = "sub_change")
    {
        if (!IsEnabled(player))
        {
            return;
        }

        var pid = player.GetInstanceID();
        var sessionId = s_activeByPlayer.TryGetValue(pid, out var session) ? session.Id : 0;
        var dt = sessionId > 0 ? Time.unscaledTime - session.EnterTime : 0f;

        LogNoStack(
            $"{SubPrefix} EXIT id={sessionId} dir={dir4} layer=Anim reason={reason} " +
            $"dt={dt:F3}s from={fromSub} to={toSub} xf={crossfade:F3}s frame={Time.frameCount}",
            player);
    }

    public static TurnDirection4 MapDirection4(TurnType type, sbyte direction)
    {
        if (type == TurnType.Turn180)
        {
            return direction < 0 ? TurnDirection4.Left180 : TurnDirection4.Right180;
        }

        if (type == TurnType.Turn90)
        {
            return direction < 0 ? TurnDirection4.Left90 : TurnDirection4.Right90;
        }

        return TurnDirection4.None;
    }

    static Vector3 Planarize(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }

    static void LogNoStack(string message, Object context, LogType type = LogType.Log) =>
        Debug.LogFormat(type, LogOption.NoStacktrace, context, "{0}", message);
}

/// <summary>
/// 235 — FreeLocomotion 即时逻辑转向后的 90/180 补偿性 Turn 表现专项探针。
/// 只记录方向边沿、裁决、选片、播放和结束；不逐帧采样，不改变 Gameplay/Presentation。
/// 与 TurnProbe 同文件以兼容 Unity 尚未刷新生成 csproj 的编辑会话。
/// </summary>
public static class LocomotionTurnPresentation235Probe
{
    const string Prefix = "[LocoTurn235]";
    const float DirectionEdgeThresholdDeg = 35f;

    struct Session
    {
        public int Id;
        public float StartTime;
        public bool DecisionLogged;
        public bool Selected;
        public bool Played;
        public string Decision;
        public string Clip;
        public uint Generation;
        public bool LeaseStarted;
        public bool LeaseEnded;
        public Vector3 StartPosition;
        public float MotionPath;
    }

    static int s_nextSessionId = 1;
    static readonly Dictionary<int, Session> s_sessions = new Dictionary<int, Session>(4);
    static readonly Dictionary<int, Vector3> s_lastCommand = new Dictionary<int, Vector3>(4);

    public static bool IsEnabled => GameMainDebugSettings.LocomotionTurnPresentation235Log;

    public static void ObserveInput(Player player, Vector3 worldCommand, bool hasInput, InputTense tense,
        bool wantsRun, string contextSource)
    {
        if (!IsEnabled || player == null) return;

        var pid = player.GetInstanceID();
        if (!hasInput || worldCommand.sqrMagnitude < 0.0001f)
        {
            if (s_sessions.TryGetValue(pid, out var active)
                && (!active.DecisionLogged
                    || string.IsNullOrEmpty(active.Decision)
                    || !active.Decision.StartsWith("fire", System.StringComparison.Ordinal)))
            {
                End(player, "input_release", in active);
                s_sessions.Remove(pid);
            }
            s_lastCommand.Remove(pid);
            return;
        }

        var command = Planarize(worldCommand);
        var logic = Planarize(player.LogicForward);
        var signed = Vector3.SignedAngle(logic, command, Vector3.up);
        var commandEdge = !s_lastCommand.TryGetValue(pid, out var previousCommand)
                          || Vector3.Angle(previousCommand, command) >= DirectionEdgeThresholdDeg;
        s_lastCommand[pid] = command;
        if (!commandEdge) return;

        if (s_sessions.TryGetValue(pid, out var superseded)) End(player, "command_superseded", in superseded);
        var session = new Session
        {
            Id = s_nextSessionId++,
            StartTime = Time.unscaledTime,
            StartPosition = player.transform.position,
        };
        s_sessions[pid] = session;

        var tuning = player.LocomotionProfile != null ? player.LocomotionProfile.Tuning : null;
        var turnSettings = player.States != null ? player.States.LocomotionTurnSettings : TurnSettings.Default;
        Log(player, "INPUT", session.Id,
            $"tense={tense} ctx={Safe(contextSource)} cmd={V(command)} logicPre={V(logic)} " +
            $"signed={signed:F1} abs={Mathf.Abs(signed):F1} speed={player.PlanarVelocity.magnitude:F3} " +
            $"run={wantsRun} feature={turnSettings.EnableTurnInPlacePresentation} " +
            $"t90={(tuning != null ? tuning.Turn90ThresholdDeg : 70f):F1} " +
            $"t180={(tuning != null ? tuning.Turn180ThresholdDeg : 135f):F1}");
    }

    public static void ObserveDecision(Player player, in TurnInfo info, string source)
    {
        if (!TryGet(player, out var pid, out var session) || session.DecisionLogged) return;
        session.DecisionLogged = true;
        session.Decision = info.IsTurning ? $"fire:{info.Type}:{info.Direction}" : "none";
        s_sessions[pid] = session;
        Log(player, "DECIDE", session.Id,
            $"result={session.Decision} src={Safe(source)} infoAbs={info.Angle:F1} " +
            $"logicNow={V(Planarize(player.LogicForward))} move={V(Planarize(player.MovementIntent))}");
    }

    public static void ObserveSelection(Player player, TurnDirection4 direction, string source,
        ActionDataSO action, AnimationClip clip, float crossfade, float speed, bool looping)
    {
        if (!TryGet(player, out var pid, out var session)) return;
        session.Selected = clip != null;
        session.Clip = clip != null ? clip.name : "null";
        s_sessions[pid] = session;
        Log(player, "SELECT", session.Id,
            $"dir={direction} src={Safe(source)} action={Name(action)} clip={Name(clip)} " +
            $"xf={crossfade:F3} speed={speed:F3} loop={looping}");
    }

    public static void ObservePlay(Player player, TurnDirection4 direction, AnimationClip clip, string source)
    {
        if (!TryGet(player, out var pid, out var session)) return;
        session.Played = clip != null;
        session.Clip = clip != null ? clip.name : session.Clip;
        s_sessions[pid] = session;
        Log(player, "PLAY", session.Id,
            $"dir={direction} clip={Name(clip)} src={Safe(source)} dt={(Time.unscaledTime - session.StartTime):F3}");
    }

    public static void ObserveLeaseBegin(
        Player player,
        in TurnCompensationCue cue,
        float leaseSeconds)
    {
        if (!TryGet(player, out var pid, out var session) || !cue.IsTurning) return;
        session.Generation = cue.Generation;
        session.LeaseStarted = true;
        session.LeaseEnded = false;
        s_sessions[pid] = session;
        Log(player, "LEASE_BEGIN", session.Id,
            $"gen={cue.Generation} type={cue.Type} duration={leaseSeconds:F3} " +
            $"pos={P(player.transform.position)} speed={player.PlanarVelocity.magnitude:F3}");
    }

    public static void ObserveMotion(Player player, Vector3 before, Vector3 after)
    {
        if (!TryGet(player, out var pid, out var session)
            || string.IsNullOrEmpty(session.Decision)
            || !session.Decision.StartsWith("fire", System.StringComparison.Ordinal))
        {
            return;
        }

        var delta = after - before;
        delta.y = 0f;
        session.MotionPath += delta.magnitude;
        s_sessions[pid] = session;
    }

    public static void ObserveHandoff(Player player, uint generation, string reason)
    {
        if (!TryGet(player, out var pid, out var session)
            || !session.LeaseStarted
            || session.Generation != generation)
        {
            return;
        }

        session.LeaseEnded = true;
        s_sessions[pid] = session;
        var delta = player.transform.position - session.StartPosition;
        delta.y = 0f;
        Log(player, "HANDOFF", session.Id,
            $"gen={generation} reason={Safe(reason)} net={delta.magnitude:F4} " +
            $"path={session.MotionPath:F4} exitBlend=0.000 pos={P(player.transform.position)}");
    }

    public static void ObserveEnd(Player player, string reason)
    {
        if (!TryGet(player, out var pid, out var session)) return;
        End(player, reason, in session);
        s_sessions.Remove(pid);
    }

    static void End(Player player, string reason, in Session session)
    {
        var delta = player.transform.position - session.StartPosition;
        delta.y = 0f;
        Log(player, "END", session.Id,
            $"reason={Safe(reason)} dt={(Time.unscaledTime - session.StartTime):F3} " +
            $"decision={Safe(session.Decision)} selected={session.Selected} played={session.Played} clip={Safe(session.Clip)} " +
            $"lease={session.LeaseStarted}/{session.LeaseEnded} motionNet={delta.magnitude:F4} " +
            $"motionPath={session.MotionPath:F4}");
    }

    static bool TryGet(Player player, out int pid, out Session session)
    {
        pid = player != null ? player.GetInstanceID() : 0;
        session = default;
        return IsEnabled && player != null && s_sessions.TryGetValue(pid, out session);
    }

    static void Log(Player player, string evt, int sessionId, string payload) =>
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, player,
            "{0} event={1} sid={2} entity={3} frame={4} t={5:F3} | {6}",
            Prefix, evt, sessionId, player != null ? player.GetInstanceID() : 0,
            Time.frameCount, Time.unscaledTime, payload);

    static Vector3 Planarize(Vector3 value)
    {
        value.y = 0f;
        return value.sqrMagnitude > 0.0001f ? value.normalized : Vector3.forward;
    }

    static string V(Vector3 value) => $"({value.x:F2},{value.z:F2})";
    static string P(Vector3 value) => $"({value.x:F3},{value.y:F3},{value.z:F3})";
    static string Name(Object value) => value != null ? value.name : "null";
    static string Safe(string value) => string.IsNullOrEmpty(value) ? "-" : value.Replace(' ', '_');
}

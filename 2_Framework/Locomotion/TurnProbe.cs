using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 184.1 / 162.1 — Turn-In-Place 四向子状态验收探针。
/// <para>Console 过滤：<c>[Turn][Sub]</c>（ENTER / PLAY / EXIT / WARN）。</para>
/// <para>开关：Player <see cref="Player.DebugTurnSubState"/>（推荐）或 <see cref="Player.DebugLocomotion"/>。</para>
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
        player != null && (player.DebugTurnSubState || player.DebugLocomotion);

    // ─── 184.1 通用 ───

    public static void LogFacingEdge(Player player, InputTense tense, Vector3 from, Vector3 to, string reason)
    {
        if (!IsEnabled(player))
        {
            return;
        }

        var signed = Vector3.SignedAngle(Planarize(from), Planarize(to), Vector3.up);
        Debug.Log(
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

        Debug.Log(
            $"{Prefix} Tense=Tap Kind={type} dir={(direction < 0 ? "L" : "R")} abs={absAngle:F1}° fired",
            player);
    }

    public static void LogInterrupt(Player player, string reason)
    {
        if (!IsEnabled(player))
        {
            return;
        }

        Debug.Log($"{Prefix} INTERRUPT reason={reason ?? "intent"} frame={Time.frameCount}", player);
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

        Debug.Log(
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
        Debug.Log(
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

        Debug.Log(
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

        Debug.Log(
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

        Debug.Log(
            $"{SubPrefix} EXIT id={sessionId} dir={dir4} layer=Logic reason={reason} " +
            $"dt={dt:F3}s abs={absAngleAtExit:F1}° playLogged={playLogged} frame={Time.frameCount}",
            player);

        if (sessionId > 0 && !playLogged)
        {
            Debug.LogWarning(
                $"{SubPrefix} WARN id={sessionId} dir={dir4} Logic ENTER without PLAY before EXIT reason={reason}",
                player);
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

        Debug.Log(
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
}

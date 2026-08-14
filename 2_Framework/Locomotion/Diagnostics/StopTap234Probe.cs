using UnityEngine;

/// <summary>
/// 234.6.3 — Stop 终态探针。要证伪：点按仍吃 v_entry；tPhys≪tailSec；无限连点仍被 chain-max 截断。
/// 边沿最多 3 行/次停止，无堆栈。Console 过滤：[StopTap234]
/// </summary>
public static class StopTap234Probe
{
    public const string Prefix = "[StopTap234]";
    const float ExtraPathRatio = 1.15f;
    const float HighRunSpeedRatio = 0.50f;
    const float TurnYawDegrees = 8f;

    static Player s_player;
    static int s_sessionId;
    static int s_eventId;
    static ActionDataSO s_action;
    static uint s_lease;
    static bool s_active;
    static bool s_loggedPhysComplete;
    static Vector3 s_startPos;
    static Vector3 s_lastPos;
    static float s_path;
    static float s_startLogicYaw;
    static float s_startVisualYaw;
    static float s_integrateDist;
    static float s_tPhys;
    static float s_tLease;

    static bool Enabled => GameMainDebugSettings.StopTap234Log;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_player = null;
        s_sessionId = 0;
        s_eventId = 0;
        s_action = null;
        s_active = false;
    }

    public static void ObserveBegin(Player player, ActionDataSO action, in StopRuntimeContext stop)
    {
        if (!Enabled || player == null || action == null || !stop.IsActive)
        {
            return;
        }

        EnsureSession(player);
        s_action = action;
        s_lease = 0;
        s_active = true;
        s_loggedPhysComplete = stop.PhysicsComplete;
        s_startPos = Planar(player.transform.position);
        s_lastPos = s_startPos;
        s_path = 0f;
        s_startLogicYaw = Yaw(player.LogicForward);
        s_startVisualYaw = Yaw(player.VisualRotation * Vector3.forward);
        s_integrateDist = stop.RuntimeDistance;
        s_tPhys = StopIntegrator.PredictDuration(stop.RemainingSpeed, stop.BrakeDeceleration);
        s_tLease = stop.RuntimeDuration;

        var snapshot = player.LastStopSessionSnapshot;
        var walk = player.RuntimeStats.WalkSpeed;
        var run = player.RuntimeStats.RunSpeed;
        var vRatio = run > 0.01f ? stop.EntrySpeed / run : 0f;
        var inherit = action.InheritPhysics;
        var dTap = StopTierResolver.ResolveTapStopDistance(inherit);
        var tapWindow = StopTierResolver.ResolveTapWindow(inherit);
        var tailSec = StopTierResolver.ResolveTapTailSeconds(inherit);
        var isTap = StopTierResolver.IsTapTier(stop.SessionTier);
        var chainUnlimited = StopTierResolver.IsTapChainUnlimited(inherit);
        var vUsed = stop.RemainingSpeed;
        var v0plan = isTap && tailSec > 0.0001f ? 2f * dTap / Mathf.Max(0.001f, tailSec) : vUsed;
        var vClamped = !isTap && stop.EntrySpeed > stop.ReferenceGaitSpeed + 0.01f;
        var dRef = inherit.FullSpeedStopDistance > 0.0001f ? inherit.FullSpeedStopDistance : inherit.MaxDistance;
        var dCapped = vClamped && stop.RuntimeDistance <= dRef + 0.02f;
        var highRunTap = isTap && vRatio >= HighRunSpeedRatio;
        var segWall = ActionTimeAuthority.ResolveSegmentWallSeconds(action);
        var tailMode = stop.AuthorTail ? "Author" : "Auto";

        var recipe = stop.SessionTier switch
        {
            StopSessionTier.MicroTap => "Tap",
            StopSessionTier.TapChain => "TapChain",
            StopSessionTier.StartAbort => "StartAbort",
            StopSessionTier.LoopStop => "Loop",
            StopSessionTier.HardBrake => "HardBrake",
            _ => "None",
        };
        Log(
            "STOP_BEGIN",
            $"action={action.name} strategy={stop.Strategy} tier={stop.SessionTier} recipe={recipe} isTap={isTap} " +
            $"chainIndex={stop.ChainIndex} chained={stop.Chained} chainUnlimited={chainUnlimited} tailMode={tailMode} " +
            $"heldTicks={(snapshot.IsValid ? snapshot.HeldTicks : -1)} heldSec={(snapshot.IsValid ? snapshot.HeldSeconds : -1f):F3} " +
            $"tapWindow={tapWindow:F3} reachedLoop={(snapshot.IsValid && snapshot.ReachedLoop)} wantsRun={(snapshot.IsValid && snapshot.WantsRunAtRelease)} " +
            $"vEntry={stop.EntrySpeed:F3} vUsed={vUsed:F3} v0exec={vUsed:F3} v0plan={v0plan:F3} discardEntry={isTap} vClamped={vClamped} walk={walk:F2} run={run:F2} vRunRatio={vRatio:F2} vRef={stop.ReferenceGaitSpeed:F2} " +
            $"a={stop.BrakeDeceleration:F3} Dplan={stop.RuntimeDistance:F4} Dtap={dTap:F3} dRef={dRef:F3} dCapped={dCapped} Dplan/dRef={(dRef > 0.01f ? stop.RuntimeDistance / dRef : 0f):F2} " +
            $"tPhys={s_tPhys:F3} tLease={s_tLease:F3} segWall={segWall:F3} startNt={stop.PresentationStartNormalized:F3} animSpeed={stop.BaseAnimSpeed:F3} " +
            $"logicLocked={player.IsLogicForwardLocked} h1_highRunTap={highRunTap} physicsComplete0={stop.PhysicsComplete}",
            player);
    }

    public static void ObserveFrame(
        Player player,
        ActionDataSO action,
        uint lease,
        float nt,
        in StopRuntimeContext stop,
        bool physicsComplete,
        float remainingSpeed,
        Vector3 executorWorldDelta,
        bool appliedMotor,
        Vector3 before,
        Vector3 after)
    {
        if (!Enabled || !s_active || player == null || action != s_action || !stop.IsActive)
        {
            return;
        }

        s_lease = lease;
        var planar = Planar(after);
        s_path += Planar(after - before).magnitude;
        s_lastPos = planar;

        if (s_loggedPhysComplete || !physicsComplete)
        {
            return;
        }

        s_loggedPhysComplete = true;
        var net = (planar - s_startPos).magnitude;
        Log(
            "PHYS_COMPLETE",
            $"action={action.name} lease={lease} nt={nt:F3} vRemain={remainingSpeed:F3} " +
            $"path={s_path:F4} net={net:F4} Dplan={s_integrateDist:F4} execDelta={Planar(executorWorldDelta).magnitude:F4} " +
            $"motor={appliedMotor} tLeaseLeft={Mathf.Max(0f, s_tLease - s_tPhys):F3}",
            player);
    }

    public static void ObserveEnd(
        Player player,
        ActionDataSO action,
        in StopRuntimeContext stop,
        float wallElapsed,
        float actualDistance)
    {
        if (!Enabled || !s_active || player == null || action != s_action)
        {
            return;
        }

        var logicYaw = Yaw(player.LogicForward);
        var visualYaw = Yaw(player.VisualRotation * Vector3.forward);
        var logicDelta = Mathf.DeltaAngle(s_startLogicYaw, logicYaw);
        var visualDelta = Mathf.DeltaAngle(s_startVisualYaw, visualYaw);
        var net = (s_lastPos - s_startPos).magnitude;
        var extra = s_integrateDist > 0.001f && s_path > s_integrateDist * ExtraPathRatio;
        var logicTurn = Mathf.Abs(logicDelta) >= TurnYawDegrees;
        var visualYawOnly = !extra && !logicTurn && Mathf.Abs(visualDelta) >= TurnYawDegrees;
        var warn = extra || logicTurn;
        var isTap = StopTierResolver.IsTapTier(stop.SessionTier);
        Log(
            warn ? "STOP_END_WARN" : "STOP_END",
            $"action={action.name} lease={s_lease} tier={stop.SessionTier} isTap={isTap} wall={wallElapsed:F3} tPhys={s_tPhys:F3} tLease={s_tLease:F3} " +
            $"Dplan={s_integrateDist:F4} actualNet={actualDistance:F4} path={s_path:F4} net={net:F4} " +
            $"logicYawDelta={logicDelta:F1} visualYawDelta={visualDelta:F1} logicLocked={player.IsLogicForwardLocked} " +
            $"h2_turn={logicTurn} visualYawOnly={visualYawOnly} h3_extraPath={extra} vEnd={player.PlanarVelocity.magnitude:F3}",
            player);
        s_active = false;
        s_action = null;
    }

    public static void ObservePromote(Player player, ActionDataSO action, in StopRuntimeContext stop, string reason)
    {
        if (!Enabled || player == null)
        {
            return;
        }

        EnsureSession(player);
        Log(
            "STOP_PROMOTE",
            $"action={(action != null ? action.name : "?")} recipe=promoted-start chained=false " +
            $"reason={reason} chainIndex={stop.ChainIndex} vEnd={player.PlanarVelocity.magnitude:F3}",
            player);
        s_active = false;
        s_action = null;
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
        Log(
            "SESSION_BEGIN",
            $"format=compact-v3 stack=off purpose=StopFinal_TapCreep_UnlimitedChain player={player.name}",
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
            $"{Prefix} sid={s_sessionId} e={evt} eid={s_eventId} {body}");
    }

    static Vector3 Planar(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    static float Yaw(Vector3 forward)
    {
        var planar = Planar(forward);
        if (planar.sqrMagnitude < 0.0001f)
        {
            return 0f;
        }

        return Mathf.Atan2(planar.x, planar.z) * Mathf.Rad2Deg;
    }
}

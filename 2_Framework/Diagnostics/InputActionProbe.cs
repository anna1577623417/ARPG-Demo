using System;
using System.Text;
using UnityEngine;

/// <summary>
/// 输入-Action 诊断通道（独立于其他 Log，prefix = [ActInpDiag]）。
///
/// 设计目标：
///   ① 不刷屏：所有"非边沿"事件按 <see cref="HeartbeatIntervalSec"/> 节流；
///      边沿事件（输入按下/松开、Intent 派发、Action 进入/退出、Yaw 应用、Slide 突进）总是 1 行。
///   ② 独立：唯一前缀；不读项目里其它诊断开关。
///   ③ 只读：不修改逻辑，所有 Log 方法都是 if(!Enabled) return 短路。
///   ④ 关联：每条都带 Frame + Time + ActiveAction + 关键状态，便于跨日志对账。
///
/// 用法：
///   - 运行时打开总开关：<see cref="Enabled"/> = true（建议挂个 MonoBehaviour 在测试场景里开/关）。
///   - 只跟踪一个 Player（避免多角色刷屏）：<see cref="SetTrackedPlayer"/>。
///   - 想看 Slide 突进多少米才算异常：调整 <see cref="SlideBurstThresholdMeters"/>。
/// </summary>
public static class InputActionProbe
{
    // ─── 配置 ───────────────────────────────────────────────────
    /// <summary>总开关。关闭时所有 Log 方法零成本短路。</summary>
    public static bool Enabled = false;

    /// <summary>仅跟踪指定 Player，避免多实例刷屏。null = 全跟。</summary>
    static Player s_tracked;

    /// <summary>心跳/状态类日志的节流间隔（秒）；边沿事件不受此限制。</summary>
    public static float HeartbeatIntervalSec = 0.25f;

    /// <summary>Slide 单帧位移超过此值（米）即报警；典型期望 0.02~0.05/帧；&gt;0.30 视为"闪现"。</summary>
    public static float SlideBurstThresholdMeters = 0.30f;

    /// <summary>同一类心跳事件下次允许打印的时刻（Time.unscaledTime）。</summary>
    static float s_nextHeartbeatAllowed;

    const string Prefix = "[ActInpDiag]";

    public static void SetTrackedPlayer(Player player) => s_tracked = player;

    static bool ShouldSkip(Player player)
    {
        if (!Enabled) return true;
        if (s_tracked != null && !ReferenceEquals(s_tracked, player)) return true;
        return false;
    }

    static string Header(Player p)
    {
        var sb = new StringBuilder(160);
        sb.Append('[').Append(Time.frameCount).Append('|').Append(Time.unscaledTime.ToString("F3")).Append("s] ");
        if (p != null)
        {
            sb.Append("state=").Append(p.States?.Current?.GetType().Name ?? "?");
            var action = ResolveActiveAction(p);
            sb.Append(" action=").Append(action != null ? action.name : "-");
            sb.Append(" grnd=").Append(p.IsGrounded ? "1" : "0");
        }
        return sb.ToString();
    }

    static ActionDataSO ResolveActiveAction(Player p)
    {
        if (p?.States?.Current is PlayerActionState act) return act.CurrentAction;
        return null;
    }

    // ─── 1. 输入沿（按下 / 松开 / 离散脉冲）─────────────────────
    public static void LogInputEdge(Player p, string slot, string edge, float holdSec)
    {
        if (ShouldSkip(p)) return;
        Debug.Log($"{Prefix} INPUT {Header(p)} slot={slot} edge={edge} hold={holdSec:F3}s", p);
    }

    // ─── 2. 语义派发（Tap/Charge/Combo/Release/Directional）──────
    public static void LogSemanticDispatched(Player p, string slot, string semantic, int comboIndex, float holdSec)
    {
        if (ShouldSkip(p)) return;
        Debug.Log($"{Prefix} SEMANTIC {Header(p)} slot={slot} sem={semantic} comboIdx={comboIndex} hold={holdSec:F3}s", p);
    }

    // ─── 3. Intent 路由（成功/失败）─────────────────────────────
    public static void LogRoute(Player p, string intentKind, bool routed, string note)
    {
        if (ShouldSkip(p)) return;
        Debug.Log($"{Prefix} ROUTE {Header(p)} kind={intentKind} routed={(routed ? "Y" : "N")} note={note}", p);
    }

    static string FormatWindowInterruptMask(ActionDataSO action)
    {
        if (action?.Windows == null || action.Windows.Count == 0)
        {
            return ActionCategory.None.ToString();
        }

        var union = ActionCategory.None;
        for (var i = 0; i < action.Windows.Count; i++)
        {
            union |= action.Windows[i].InterruptibleByCategories;
        }

        return union.ToString();
    }

    // ─── 4. Action 进入 / 退出（带 Recovery / InterruptibleMask） ─
    public static void LogActionEnter(Player p, ActionDataSO action, string fromState)
    {
        if (ShouldSkip(p) || action == null) return;
        var locoRecovery = action.IsLocomotionRecovery ? "Y" : "N";
        var interruptMask = FormatWindowInterruptMask(action);
        Debug.Log($"{Prefix} ACTION_ENTER {Header(p)} from={fromState} " +
                  $"isLocoRecovery={locoRecovery} interruptibleBy={interruptMask} " +
                  $"cat={action.IntentCategory} duration={action.Duration:F2}s", p);
    }

    public static void LogActionExit(Player p, ActionDataSO action, string reason, float elapsed, float duration)
    {
        if (ShouldSkip(p) || action == null) return;
        var nt = duration > 0.0001f ? elapsed / duration : 0f;
        Debug.Log($"{Prefix} ACTION_EXIT  {Header(p)} reason={reason} elapsed={elapsed:F3} nt={nt:F2}", p);
    }

    // ─── 5. Intent 被吞 / 拒绝（拒绝点的 reason 必须填）──────────
    public static void LogIntentDropped(Player p, string intentKind, string gate, string reason)
    {
        if (ShouldSkip(p)) return;
        Debug.LogWarning($"{Prefix} DROP {Header(p)} kind={intentKind} gate={gate} reason={reason}", p);
    }

    // ─── 6. Action 期间 Yaw / Facing 被改动 ─────────────────────
    /// <summary>SetLogicForward 在 Action 状态期间触发时调用 —— 用于核对"动作中异常转向"。</summary>
    public static void LogFacingApplied(Player p, ActionDataSO action, Vector3 fromForward, Vector3 toForward, string source)
    {
        if (ShouldSkip(p)) return;
        var signed = Vector3.SignedAngle(fromForward, toForward, Vector3.up);
        if (Mathf.Abs(signed) < 1f) return; // 微小变化忽略
        Debug.Log($"{Prefix} FACING {Header(p)} action={(action != null ? action.name : "-")} " +
                  $"Δyaw={signed:+0.0;-0.0}° src={source}", p);
    }

    // ─── 7. Slide / Action 期间单帧位移异常突进 ─────────────────
    public static void LogMotionBurst(Player p, string motionTag, Vector3 delta, float dt)
    {
        if (ShouldSkip(p)) return;
        var planar = new Vector3(delta.x, 0f, delta.z).magnitude;
        if (planar < SlideBurstThresholdMeters) return;
        Debug.LogWarning($"{Prefix} MOTION_BURST {Header(p)} tag={motionTag} " +
                         $"Δxyz=({delta.x:F3},{delta.y:F3},{delta.z:F3}) planar={planar:F3}m dt={dt:F3}s " +
                         $"(threshold={SlideBurstThresholdMeters:F2}m) ⚠ 可能闪现", p);
    }

    // ─── 8. 周期心跳（仅 Action 状态打；定位"没进 Action / 一直卡 Action"）
    public static void HeartbeatActionState(Player p, ActionDataSO action, float elapsed, float duration, float baseDuration)
    {
        if (ShouldSkip(p) || action == null) return;
        var now = Time.unscaledTime;
        if (now < s_nextHeartbeatAllowed) return;
        s_nextHeartbeatAllowed = now + Mathf.Max(0.05f, HeartbeatIntervalSec);
        var nt = duration > 0.0001f ? elapsed / duration : 0f;
        Debug.Log($"{Prefix} HB_ACTION {Header(p)} elapsed={elapsed:F3} nt={nt:F2} dur={duration:F2}", p);
    }

    /// <summary>开关帮助：把 Inspector / ContextMenu / 测试场景里挂的开关收口到一处。</summary>
    public static string DescribeState()
    {
        var tracked = s_tracked != null ? s_tracked.name : "(all)";
        return $"{Prefix} Enabled={Enabled} tracked={tracked} hbInterval={HeartbeatIntervalSec:F2}s slideBurstThreshold={SlideBurstThresholdMeters:F2}m";
    }
}

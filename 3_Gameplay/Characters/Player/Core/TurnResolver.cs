using System;
using UnityEngine;

/// <summary>
/// 原地转身（Turn-In-Place）系统的"轻量解析器"——表现增强桥接层。
///
/// ═══ 系统定位（v3.3.3 模块 14）═══
///
/// · 不属于 Motion（不写位移）
/// · 不属于 Action（不参与意图仲裁）
/// · 仅是 Locomotion 状态下的"表现观察者"——把"逻辑被强制旋转"翻译成"动画层应播什么"
///
/// 核心信条：「代码负责转，动画负责骗」（184.1：LogicForward 即时；TurnResolver 只输出表现 TurnInfo）。
/// KCC / Gameplay 读 <see cref="Player.LogicForward"/>；本解析器读 VisualForward↔Intent 夹角。
/// 供 PlayerAnimController 决定是否切到 Turn 动画切片。
///
/// ═══ 算法骨架 ═══
///
/// 触发判定（同时满足）：
///   ① currentSpeed &lt; LockSpeedThreshold       （被对齐惩罚压制 / 静止起步）
///   ② |MovementIntent| 有清晰输入
///   ③ |angleDiff| ≥ LocomotionTuning.Turn90ThresholdDeg（超过防抖角度）
///
/// 状态锁定：一旦触发即锁定，屏蔽后续微小角度扰动；
/// 解锁条件：|angleDiff| &lt; UnlockAngleThreshold 即视为收敛完成。
///
/// 类型分类（162.1 唯一权威：<see cref="LocomotionTuningSO"/>）：
///   |angleDiff| ≥ Turn180ThresholdDeg（135° 默认）→ Turn180
///   |angleDiff| ≥ Turn90ThresholdDeg               → Turn90
///
/// 方向：SignedAngle 正值 = 右转，负值 = 左转。
/// </summary>
[Serializable]
public struct TurnSettings
{
    [Header("Feature")]
    [Tooltip("关闭后：不进入转身锁定、不输出 Turn 表现（仅保持 KCC/逻辑旋转 + Idle/Walk/Run）。可在关卡或角色体上快速关转身切片。")]
    public bool EnableTurnInPlacePresentation;

    [Tooltip("低于此速度才允许触发转身（避免跑动中误触发）。默认 0.2 m/s。")]
    [Range(0f, 2f)] public float LockSpeedThreshold;

    [Tooltip("锁定状态下，角度差收敛到此值以内即解锁，回到 Idle/Walk/Run。默认 10°。")]
    [Range(1f, 30f)] public float UnlockAngleThreshold;

    [Tooltip("触发后最短锁定时长（秒）。在该时间内即便角度已收敛，也保持 Turn 表现，避免瞬时闪断。默认 0.12s。")]
    [Range(0f, 0.5f)] public float MinLockDuration;

    [Header("Post-Turn Blend (162.1)")]
    [Tooltip("90° 转身解锁后，叠加到 Turn Binding TransitionDuration 的回 Locomotion 混合时长（秒）。")]
    [Range(0f, 0.5f)] public float PostTurnBlendTurn90;

    [Tooltip("180° 转身解锁后，叠加到 Turn Binding TransitionDuration 的回 Locomotion 混合时长（秒）；通常略长于 90°。")]
    [Range(0f, 0.5f)] public float PostTurnBlendTurn180;

    [Obsolete("162.1：角度阈值改读 LocomotionTuningSO.Turn90ThresholdDeg。")]
    [Tooltip("已废弃 —— 请改 LocomotionTuning.Turn90ThresholdDeg。")]
    [Range(15f, 120f)] public float TriggerAngleThreshold;

    [Obsolete("162.1：角度阈值改读 LocomotionTuningSO.Turn180ThresholdDeg。")]
    [Tooltip("已废弃 —— 请改 LocomotionTuning.Turn180ThresholdDeg。")]
    [Range(60f, 180f)] public float Type180AngleThreshold;

    [Header("Turn debugger (Console)")]
    [Tooltip("开启后在 Console 输出闸门判定、锁定/解锁与节流心跳（便于对齐转身触发与动画）。")]
    public bool EnableTriggerDebugger;

    [Tooltip("触发调试日志节流间隔（秒）。未锁定时的「未触发」与锁定中的心跳共用该间隔。")]
    [Range(0.05f, 1f)] public float TriggerDebuggerLogInterval;

    [Tooltip("Scene 视图绘制 forward（青）与 MovementIntent（黄）的水平射线，仅用于肉眼对齐角度 gate。")]
    public bool DrawTurnDebugRays;

    public static TurnSettings Default => new TurnSettings
    {
        EnableTurnInPlacePresentation = true,
        LockSpeedThreshold = 0.2f,
        UnlockAngleThreshold = 10f,
        MinLockDuration = 0.12f,
        PostTurnBlendTurn90 = 0.08f,
        PostTurnBlendTurn180 = 0.12f,
#pragma warning disable CS0618
        TriggerAngleThreshold = 60f,
        Type180AngleThreshold = 135f,
#pragma warning restore CS0618
        EnableTriggerDebugger = false,
        TriggerDebuggerLogInterval = 0.15f,
        DrawTurnDebugRays = false,
    };
}

public enum TurnType : byte
{
    None = 0,
    Turn90 = 1,
    Turn180 = 2,
}

public struct TurnInfo
{
    public bool IsTurning;
    /// <summary>当前帧 forward 与 intent 的绝对夹角（度）。</summary>
    public float Angle;
    /// <summary>仅在 IsTurning=true 时有意义。</summary>
    public TurnType Type;
    /// <summary>+1 = 右转，-1 = 左转，0 = 未转身。</summary>
    public sbyte Direction;
    /// <summary>当前帧 transform.forward 与 MovementIntent 的有符号角度差（仅诊断用）。</summary>
    public float SignedAngle;
}

/// <summary>
/// 状态化的转身解析器。每个 PlayerLocomotionState 持一份；逻辑帧调用 <see cref="Tick"/>。
/// </summary>
public sealed class TurnResolver
{
    private TurnSettings m_settings;

    private bool m_locked;
    private TurnType m_lockedType;
    private sbyte m_lockedDirection;
    private float m_lockTimer;

    private float m_nextDebuggerLogTime;

    Player m_tracePlayer;
    float m_lastExitAbsAngle;

    public TurnResolver(in TurnSettings settings)
    {
        m_settings = settings;
    }

    /// <summary>
    /// 计算本帧 TurnInfo。调用方须传入 pre-rotation 的 forward 与 MovementIntent，
    /// 即在 <see cref="Player.MoveByLocomotionIntent"/>（含 LookAtDirection）之前调用。
    /// </summary>
    /// <param name="settings">每帧传入以保证 Inspector 中表现开关/调试即时生效。</param>
    /// <param name="tuning">转身角度阈值唯一权威（Turn90/180ThresholdDeg）。</param>
    public TurnInfo Tick(Player player, float deltaTime, in TurnSettings settings, LocomotionTuningSO tuning)
    {
        m_settings = settings;
        m_tracePlayer = player;
        var triggerThreshold = tuning != null ? tuning.Turn90ThresholdDeg : 70f;
        var type180Threshold = tuning != null ? tuning.Turn180ThresholdDeg : 135f;

        if (player == null)
        {
            ClearLock("no_player");
            return default;
        }

        if (!m_settings.EnableTurnInPlacePresentation)
        {
            ClearLock("feature_off");
            return BuildNonTurningAngleSnapshot(player);
        }

        // 183.1 §0.4：Run 意图略过 Turn 表现（A+跑不播 pivot）
        if (ShouldSkipTurnPresentationForRun(player, tuning))
        {
            if (LocomotionDebug.IsEnabled(player))
            {
                LocomotionDebug.LogTurnPhase(
                    player,
                    "SKIP",
                    $"reason=wantsRun wantsRun={player.WantsRun} spd={player.PlanarVelocity.magnitude:F3}");
            }

            if (m_locked && (m_settings.EnableTriggerDebugger || LocomotionDebug.IsTraceEnabled(player)))
            {
                LocomotionDebug.Log(
                    player,
                    LocomotionDebug.CatTurn,
                    $"UNLOCK wants_run skipTurnPresentation wantsRun={player.WantsRun} " +
                    $"had type={m_lockedType} dir={(m_lockedDirection < 0 ? "L" : "R")}");
            }

            ClearLock("wants_run");
            return BuildNonTurningAngleSnapshot(player);
        }
        if (player.TryConsumeTapTurnArm(out var tapFromLogic))
        {
            if (player.HasPendingFacing)
            {
                return BuildNonTurningAngleSnapshot(player);
            }

            return TryEnterTurnFromTap(player, tapFromLogic, triggerThreshold, type180Threshold);
        }

        var forward = ResolvePresentationForward(player);
        forward.y = 0f;
        var intent = player.MovementIntent;
        intent.y = 0f;

        // 没有移动意图 → 立即解锁（避免松手后还卡在转身锁定里）
        if (intent.sqrMagnitude < 0.0001f)
        {
            if (m_locked && m_settings.EnableTriggerDebugger)
            {
                Debug.Log(
                    $"[TurnDbg] unlock | reason=input_release | was locked type={m_lockedType} dir={m_lockedDirection}",
                    player);
            }

            ClearLock("input_release");
            return default;
        }

        var signedAngle = Vector3.SignedAngle(forward, intent, Vector3.up);
        var absAngle = Mathf.Abs(signedAngle);
        m_lastExitAbsAngle = absAngle;
        var currentSpeed = player.PlanarVelocity.magnitude;

        // 162.1：移动中强制退出原地转身锁定 — Turn-In-Place 仅用于站立起步，避免锁死相机相对移动。
        if (m_locked && currentSpeed >= m_settings.LockSpeedThreshold)
        {
            if (m_settings.EnableTriggerDebugger || LocomotionDebug.IsTraceEnabled(player))
            {
                LocomotionDebug.Log(
                    player,
                    LocomotionDebug.CatTurn,
                    $"UNLOCK speed_gate spd={currentSpeed:F3}≥{m_settings.LockSpeedThreshold:F3} " +
                    $"had type={m_lockedType} dir={(m_lockedDirection < 0 ? "L" : "R")}");
            }

            ClearLock("speed_gate", absAngle);
        }

        // === 已锁定：用解锁阈值判定收敛 ===
        if (m_locked)
        {
            m_lockTimer += Mathf.Max(0f, deltaTime);
            var lockElapsedEnough = m_lockTimer >= m_settings.MinLockDuration;
            if (lockElapsedEnough && absAngle < m_settings.UnlockAngleThreshold)
            {
                if (m_settings.EnableTriggerDebugger)
                {
                    Debug.Log(
                        $"[TurnDbg] unlock | reason=angle_converged | absAngle={absAngle:F1}° (< unlock {m_settings.UnlockAngleThreshold:F1}°) " +
                        $"minLock={m_settings.MinLockDuration:F2}s elapsed lockTimer={m_lockTimer:F3}s | had type={m_lockedType} dir={m_lockedDirection}",
                        player);
                }

                ClearLock("angle_converged", absAngle);
                return new TurnInfo
                {
                    IsTurning = false,
                    Angle = absAngle,
                    SignedAngle = signedAngle,
                };
            }

            MaybeLogLockedHeartbeat(player, absAngle, signedAngle);

            return new TurnInfo
            {
                IsTurning = true,
                Angle = absAngle,
                Type = m_lockedType,
                Direction = m_lockedDirection,
                SignedAngle = signedAngle,
            };
        }

        // === 未锁定：用触发阈值判定是否进入锁定 ===
        var speedOk = currentSpeed < m_settings.LockSpeedThreshold;
        var angleOk = absAngle >= triggerThreshold;
        var shouldTurn = speedOk && angleOk;

        if (!shouldTurn)
        {
            MaybeLogTurnSkipped(player, currentSpeed, absAngle, signedAngle, speedOk, angleOk, triggerThreshold, type180Threshold);
            return TurnIntentBuilder.CreateNonTurning(absAngle, signedAngle);
        }

        if (!TurnIntentBuilder.TryClassify(signedAngle, triggerThreshold, type180Threshold, out m_lockedType, out m_lockedDirection))
        {
            return TurnIntentBuilder.CreateNonTurning(absAngle, signedAngle);
        }

        m_locked = true;
        m_lockTimer = 0f;
        TurnProbe.LogSubEnter(
            player,
            m_lockedType,
            m_lockedDirection,
            absAngle,
            signedAngle,
            ResolveEnterReason(player));

        if (m_settings.EnableTriggerDebugger || LocomotionDebug.IsTraceEnabled(player))
        {
            LocomotionDebug.Log(
                player,
                LocomotionDebug.CatTurn,
                $"LOCK type={m_lockedType} dir={(m_lockedDirection < 0 ? "L" : "R")} abs={absAngle:F1}° " +
                $"signed={signedAngle:F1}° spd={currentSpeed:F3} (<{m_settings.LockSpeedThreshold}) " +
                $"trigger≥{triggerThreshold:F1}° 180≥{type180Threshold:F1}°");
        }

        if (LocomotionDebug.IsEnabled(player))
        {
            var turnDir = m_lockedDirection < 0 ? TurnDirection4.Left90 : TurnDirection4.Right90;
            if (m_lockedType == TurnType.Turn180)
            {
                turnDir = m_lockedDirection < 0 ? TurnDirection4.Left180 : TurnDirection4.Right180;
            }

            LocomotionDebug.LogTurnPhase(
                player,
                "Detect",
                $"signed={signedAngle:F1}° abs={absAngle:F1}° turn90={triggerThreshold:F1} turn180={type180Threshold:F1} " +
                $"→ type={m_lockedType} dir={turnDir}");
            var fwd = forward.normalized;
            var inp = intent.normalized;
            LocomotionDebug.LogTurnPhase(
                player,
                "FIRE",
                $"type={m_lockedType} dir={turnDir} " +
                $"forward=({fwd.x:F2},{fwd.z:F2}) inputWorld=({inp.x:F2},{inp.z:F2})");
        }

        return new TurnInfo
        {
            IsTurning = true,
            Angle = absAngle,
            Type = m_lockedType,
            Direction = m_lockedDirection,
            SignedAngle = signedAngle,
        };
    }

    /// <summary>状态切换离开 Locomotion 时务必调用，避免下次回到 Locomotion 时残留锁定态。</summary>
    public void ClearLock(string reason = "cleared", float absAngleAtExit = -1f)
    {
        if (m_locked && m_tracePlayer != null)
        {
            var abs = absAngleAtExit >= 0f ? absAngleAtExit : m_lastExitAbsAngle;
            TurnProbe.LogSubExitLogic(
                m_tracePlayer,
                m_lockedType,
                m_lockedDirection,
                reason,
                m_lockTimer,
                abs);
        }

        m_locked = false;
        m_lockTimer = 0f;
        m_lockedType = TurnType.None;
        m_lockedDirection = 0;
    }

    static string ResolveEnterReason(Player player)
    {
        if (player == null)
        {
            return "angle_gate";
        }

        return player.CurrentInputTense == InputTense.Tap ? "tap" : "angle_gate";
    }

    /// <summary>184.1 — 表现层 forward（VisualRoot）；无拆分时回落 LogicForward。</summary>
    private static Vector3 ResolvePresentationForward(Player player)
    {
        if (player != null
            && player.VisualRoot != null
            && player.VisualRoot != player.transform)
        {
            var visualFwd = player.VisualRotation * Vector3.forward;
            visualFwd.y = 0f;
            if (visualFwd.sqrMagnitude > 0.0001f)
            {
                return visualFwd.normalized;
            }
        }

        var logic = player != null ? player.LogicForward : Vector3.forward;
        logic.y = 0f;
        return logic.sqrMagnitude > 0.0001f ? logic.normalized : Vector3.forward;
    }

    TurnInfo TryEnterTurnFromTap(
        Player player,
        Vector3 fromLogicForward,
        float triggerThreshold,
        float type180Threshold)
    {
        fromLogicForward.y = 0f;
        var toForward = player.LogicForward;
        toForward.y = 0f;
        if (fromLogicForward.sqrMagnitude < 0.0001f || toForward.sqrMagnitude < 0.0001f)
        {
            return BuildNonTurningAngleSnapshot(player);
        }

        fromLogicForward.Normalize();
        toForward.Normalize();
        var signedAngle = Vector3.SignedAngle(fromLogicForward, toForward, Vector3.up);
        var absAngle = Mathf.Abs(signedAngle);
        if (!TurnIntentBuilder.TryClassify(signedAngle, triggerThreshold, type180Threshold, out m_lockedType, out m_lockedDirection))
        {
            return TurnIntentBuilder.CreateNonTurning(absAngle, signedAngle);
        }

        m_locked = true;
        m_lockTimer = 0f;
        TurnProbe.LogTapTurn(player, m_lockedType, m_lockedDirection, absAngle);
        TurnProbe.LogSubEnter(player, m_lockedType, m_lockedDirection, absAngle, signedAngle, "tap");

        if (m_settings.EnableTriggerDebugger || LocomotionDebug.IsTraceEnabled(player))
        {
            LocomotionDebug.Log(
                player,
                LocomotionDebug.CatTurn,
                $"LOCK tap type={m_lockedType} dir={(m_lockedDirection < 0 ? "L" : "R")} abs={absAngle:F1}°");
        }

        return TurnIntentBuilder.Create(true, m_lockedType, m_lockedDirection, absAngle, signedAngle);
    }

    /// <summary>183.1 §0.4：WantsRun 时略过 Turn-In-Place 表现（与 speed 门槛 OR）。</summary>
    private static bool ShouldSkipTurnPresentationForRun(Player player, LocomotionTuningSO tuning)
    {
        if (player == null || !player.WantsRun)
        {
            return false;
        }

        return tuning == null || tuning.SkipTurnPresentationWhenWantsRun;
    }

    /// <summary>功能关闭时仍返回 forward↔intent 夹角，供潜在 UI/诊断；<see cref="TurnInfo.IsTurning"/> 恒为 false。</summary>
    private static TurnInfo BuildNonTurningAngleSnapshot(Player player)
    {
        var forward = ResolvePresentationForward(player);
        forward.y = 0f;
        var intent = player.MovementIntent;
        intent.y = 0f;
        if (intent.sqrMagnitude < 0.0001f)
        {
            return default;
        }

        var signedAngle = Vector3.SignedAngle(forward, intent, Vector3.up);
        var abs = Mathf.Abs(signedAngle);
        return new TurnInfo
        {
            IsTurning = false,
            Angle = abs,
            SignedAngle = signedAngle,
        };
    }

    private void MaybeLogLockedHeartbeat(Player player, float absAngle, float signedAngle)
    {
        if (!m_settings.EnableTriggerDebugger || player == null)
        {
            return;
        }

        var now = Time.unscaledTime;
        if (now < m_nextDebuggerLogTime)
        {
            return;
        }

        m_nextDebuggerLogTime = now + Mathf.Max(0.05f, m_settings.TriggerDebuggerLogInterval);
        Debug.Log(
            $"[TurnDbg] locked | absAngle={absAngle:F1}° signed={signedAngle:F1}° | lockTimer={m_lockTimer:F3}s " +
            $"type={m_lockedType} dir={(m_lockedDirection < 0 ? "L" : "R")} | need unlock <{m_settings.UnlockAngleThreshold:F1}° after minLock",
            player);
    }

    private void MaybeLogTurnSkipped(Player player, float currentSpeed, float absAngle, float signedAngle,
        bool speedOk, bool angleOk, float triggerThreshold, float type180Threshold)
    {
        if (player == null)
        {
            return;
        }

        var useBlueprintLog = LocomotionDebug.IsEnabled(player);
        if (!useBlueprintLog && !m_settings.EnableTriggerDebugger)
        {
            return;
        }

        var now = Time.unscaledTime;
        if (now < m_nextDebuggerLogTime)
        {
            return;
        }

        m_nextDebuggerLogTime = now + Mathf.Max(0.05f, m_settings.TriggerDebuggerLogInterval);

        if (useBlueprintLog)
        {
            var reason = !speedOk ? "speed" : "noAngle";
            LocomotionDebug.LogTurnPhase(
                player,
                "SKIP",
                $"reason={reason} abs={absAngle:F1}° signed={signedAngle:F1}° " +
                $"spd={currentSpeed:F3} trigger90={triggerThreshold:F1} trigger180={type180Threshold:F1}");
            return;
        }

        var reasons = "";
        if (!speedOk)
        {
            reasons += $"speed_gate (spd={currentSpeed:F3} ≥ lockSpd={m_settings.LockSpeedThreshold}) ";
        }

        if (!angleOk)
        {
            reasons += $"angle_gate (|θ|={absAngle:F1}° < trigger={triggerThreshold:F1}°) ";
        }

        Debug.Log($"[TurnDbg] no_turn | signed={signedAngle:F1}° abs={absAngle:F1}° | {reasons}".TrimEnd(),
            player);
    }
}

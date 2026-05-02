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
/// 核心信条："代码负责转，动画负责骗"。
/// KCC 仍由 Player.LookAtDirection 驱动 RotateTowards；本解析器只输出 TurnInfo
/// 供 PlayerAnimController 决定是否切到 Turn 动画切片。
///
/// ═══ 算法骨架 ═══
///
/// 触发判定（同时满足）：
///   ① currentSpeed &lt; LockSpeedThreshold       （被对齐惩罚压制 / 静止起步）
///   ② |MovementIntent| 有清晰输入
///   ③ |angleDiff| ≥ TriggerAngleThreshold       （超过防抖角度）
///
/// 状态锁定：一旦触发即锁定，屏蔽后续微小角度扰动；
/// 解锁条件：|angleDiff| &lt; UnlockAngleThreshold 即视为收敛完成。
///
/// 类型分类：
///   |angleDiff| ≥ Type180AngleThreshold（135° 默认）→ Turn180
///   否则                                              → Turn90
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

    [Tooltip("角度差超过此值才触发转身（防抖动）。默认 60°。")]
    [Range(15f, 120f)] public float TriggerAngleThreshold;

    [Tooltip("锁定状态下，角度差收敛到此值以内即解锁，回到 Idle/Walk/Run。默认 10°。")]
    [Range(1f, 30f)] public float UnlockAngleThreshold;

    [Tooltip("触发后最短锁定时长（秒）。在该时间内即便角度已收敛，也保持 Turn 表现，避免瞬时闪断。默认 0.12s。")]
    [Range(0f, 0.5f)] public float MinLockDuration;

    [Tooltip("角度差大于等于此值视为 180° 转身，否则视为 90° 转身。默认 135°。")]
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
        TriggerAngleThreshold = 60f,
        UnlockAngleThreshold = 10f,
        MinLockDuration = 0.12f,
        Type180AngleThreshold = 135f,
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

    public TurnResolver(in TurnSettings settings)
    {
        m_settings = settings;
    }

    /// <summary>
    /// 计算本帧 TurnInfo。调用方须传入 pre-rotation 的 forward 与 MovementIntent，
    /// 即在 <see cref="Player.MoveByLocomotionIntent"/>（含 LookAtDirection）之前调用。
    /// </summary>
    /// <param name="settings">每帧传入以保证 Inspector 中阈值/调试开关即时生效。</param>
    public TurnInfo Tick(Player player, float deltaTime, in TurnSettings settings)
    {
        m_settings = settings;

        if (player == null)
        {
            ClearLock();
            return default;
        }

        if (!m_settings.EnableTurnInPlacePresentation)
        {
            ClearLock();
            return BuildNonTurningAngleSnapshot(player);
        }

        var forward = player.transform.forward;
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

            ClearLock();
            return default;
        }

        var signedAngle = Vector3.SignedAngle(forward, intent, Vector3.up);
        var absAngle = Mathf.Abs(signedAngle);

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

                ClearLock();
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
        var currentSpeed = player.PlanarVelocity.magnitude;
        var speedOk = currentSpeed < m_settings.LockSpeedThreshold;
        var angleOk = absAngle >= m_settings.TriggerAngleThreshold;
        var shouldTurn = speedOk && angleOk;

        if (!shouldTurn)
        {
            MaybeLogGateBlocked(player, currentSpeed, absAngle, signedAngle, speedOk, angleOk);

            return new TurnInfo
            {
                IsTurning = false,
                Angle = absAngle,
                SignedAngle = signedAngle,
            };
        }

        m_locked = true;
        m_lockTimer = 0f;
        m_lockedDirection = (sbyte)(signedAngle > 0f ? 1 : -1);
        m_lockedType = absAngle >= m_settings.Type180AngleThreshold
            ? TurnType.Turn180
            : TurnType.Turn90;

        if (m_settings.EnableTriggerDebugger)
        {
            Debug.Log(
                $"[TurnDbg] LOCK | type={m_lockedType} dir={(m_lockedDirection < 0 ? "L" : "R")} " +
                $"abs={absAngle:F1}° signed={signedAngle:F1}° | speed={currentSpeed:F3} (< {m_settings.LockSpeedThreshold}) " +
                $"trigger≥{m_settings.TriggerAngleThreshold}° | 180阈值≥{m_settings.Type180AngleThreshold}°",
                player);
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
    public void ClearLock()
    {
        m_locked = false;
        m_lockTimer = 0f;
        m_lockedType = TurnType.None;
        m_lockedDirection = 0;
    }

    /// <summary>功能关闭时仍返回 forward↔intent 夹角，供潜在 UI/诊断；<see cref="TurnInfo.IsTurning"/> 恒为 false。</summary>
    private static TurnInfo BuildNonTurningAngleSnapshot(Player player)
    {
        var forward = player.transform.forward;
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

    private void MaybeLogGateBlocked(Player player, float currentSpeed, float absAngle, float signedAngle,
        bool speedOk, bool angleOk)
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

        var reasons = "";
        if (!speedOk)
        {
            reasons += $"speed_gate (spd={currentSpeed:F3} ≥ lockSpd={m_settings.LockSpeedThreshold}) ";
        }

        if (!angleOk)
        {
            reasons += $"angle_gate (|θ|={absAngle:F1}° < trigger={m_settings.TriggerAngleThreshold}°) ";
        }

        Debug.Log($"[TurnDbg] no_turn | signed={signedAngle:F1}° abs={absAngle:F1}° | {reasons}".TrimEnd(),
            player);
    }
}

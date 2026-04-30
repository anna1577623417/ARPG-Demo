using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单 Clip 蓄力：慢速起手 → 在归一化点定格 → 松键或超时后加速播完劈砍。逻辑归一化时间与 Playable 速度由事件对齐。
/// </summary>
[Serializable]
public sealed class ActionChargeConfig
{
    [Tooltip("开启后本动作为轻/重普攻时走蓄力微阶段，而非线性 Duration。")]
    public bool CanCharge;

    [Tooltip("举刀至「可蓄力滞留」的归一化时刻（须小于 1）。")]
    [Range(0.02f, 0.95f)]
    public float ChargeHoldPoint = 0.32f;

    [Tooltip("按住时趋近滞留点的逻辑/动画倍率（小于 1 变慢）。")]
    [Range(0.05f, 2f)]
    public float ChargeStartupSpeed = 0.45f;

    [Tooltip("在滞留点最长保持（秒），超时自动进入劈砍。")]
    [Range(0f, 12f)]
    public float MaxChargeHoldTime = 2.5f;

    [Tooltip("释放后劈砍段逻辑/动画倍率（可大于 1 更利落）。")]
    [Range(0.1f, 4f)]
    public float ExecutionSpeed = 1.35f;

    [Tooltip("在滞留点至少经过多久才打上蓄力标签；0 表示只要进入过滞留即算蓄力完成。")]
    [Range(0f, 5f)]
    public float MinHoldTimeForChargedTag;

    [Tooltip("满足蓄力条件时叠加的标签（如 AttackCharged）；ulong 位掩码，Inspector 以 StateTag 勾选编辑。")]
    public ulong ChargedPayloadTags = (ulong)StateTag.AttackCharged;
}

/// <summary>
/// 数据驱动动作资产 — 逻辑与表现的单一数据源（翻滚 / 剑冲 / 普攻等）。
/// 爆发位移：用 <see cref="BurstMovementSeconds"/> 与动画墙钟对齐，用 <see cref="BurstTravelDistance"/> 反算速度，避免「动画停、人还在滑」。
/// </summary>
[CreateAssetMenu(fileName = "NewAction", menuName = "GameMain/Action/Action Data")]
public class ActionDataSO : ScriptableObject
{
    [Tooltip("主表现用片段；复杂动作可后续扩展多轨道。")]
    public AnimationClip MainClip;

    [Tooltip("动画过渡时长（秒）。从上一个动画混合到此动画的 Crossfade 时间。")]
    [Range(0f, 0.5f)]
    public float CrossfadeTime = 0.08f;

    [Tooltip("动画播放速度倍率。")]
    [Range(0.1f, 20f)]
    public float AnimSpeed = 1f;

    [Tooltip("逻辑时长（秒）。与动画长度可不同，用于先行手感调参。")]
    public float Duration = 0.4f;

    [Header("Motion (new pipeline)")]
    [Tooltip("为空时走现有 legacy 逻辑；不为空时由 MotionExecutor 驱动位移。")]
    public MotionProfileSO MotionProfile;

    [Tooltip("归一化时间轴上的标签切片。")]
    public List<ActionWindow> Windows = new List<ActionWindow>();

    [Header("Charge attack (single MainClip — light tap vs hold-release)")]
    public ActionChargeConfig Charge = new ActionChargeConfig();

    [Header("Burst movement (Dodge / SwordDash — authoritative)")]
    [Tooltip("爆发位移逻辑时长（秒）。0 = 与动画墙钟一致：MainClip.length / AnimSpeed（推荐，避免滑步）。")]
    public float BurstMovementSeconds;

    [Tooltip("爆发段总水平位移（米）。>0 时 速度 = 距离/时长，总位移与时长严格一致；0 则用 BurstPlanarSpeed 常量速度。")]
    public float BurstTravelDistance;

    [Tooltip("当 BurstTravelDistance 为 0 时使用的恒定平面速度（m/s）。")]
    public float BurstPlanarSpeed = 8f;

    [Tooltip("剑冲等是否沿角色 Forward；翻滚等用输入方向时在状态里覆盖。")]
    public bool BurstUseFacingForward = true;

    [Header("Curve-driven burst (optional)")]
    [Tooltip("为真时，爆发段平面速度 = DisplacementPeakForwardSpeed × 曲线采样（与归一化爆发时间共用同一时钟）。")]
    public bool UseDisplacementVelocityCurve;

    [Tooltip("横轴：爆发段归一化时间 0~1；纵轴：速度乘数 0~1+（再乘以峰值速度）。")]
    public AnimationCurve DisplacementVelocityMultiplier = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(1f, 0f));

    [Tooltip("与曲线相乘得到本帧前进速度（m/s）。曲线为 0 时原地。")]
    public float DisplacementPeakForwardSpeed = 12f;

    [Header("Legacy flag (optional)")]
    [Tooltip("预留：通用「由 SO 驱动爆发」标记；Dodge/SwordDash 由意图种类决定，可不勾选。")]
    public bool DrivesBurstMovement;

    /// <summary>
    /// 爆发段逻辑时长：优先 BurstMovementSeconds，否则与播放墙钟对齐，最后回退 Duration。
    /// </summary>
    public float ResolveBurstMovementSeconds()
    {
        if (BurstMovementSeconds > 0.001f)
        {
            return BurstMovementSeconds;
        }

        if (MainClip != null)
        {
            return MainClip.length / Mathf.Max(0.01f, AnimSpeed);
        }

        if (Duration > 0.001f)
        {
            return Duration;
        }

        return 0.25f;
    }

    /// <summary>
    /// 爆发平面速度：若配置了行程则 distance/time，否则用 BurstPlanarSpeed。
    /// </summary>
    public float ResolveBurstPlanarSpeed(float movementSeconds)
    {
        if (movementSeconds < 0.0001f)
        {
            return BurstPlanarSpeed;
        }

        if (BurstTravelDistance > 0.001f)
        {
            return BurstTravelDistance / movementSeconds;
        }

        return BurstPlanarSpeed;
    }

    /// <summary>
    /// 曲线驱动爆发：返回本帧平面速度大小（m/s）。未启用曲线时返回 -1，由调用方使用预先算好的恒定爆发速度。
    /// </summary>
    public float EvaluateDisplacementBurstSpeed(float normalizedBurstTime)
    {
        if (!UseDisplacementVelocityCurve || DisplacementVelocityMultiplier == null)
        {
            return -1f;
        }

        var keys = DisplacementVelocityMultiplier.keys;
        if (keys == null || keys.Length == 0)
        {
            return -1f;
        }

        var mult = Mathf.Max(0f, DisplacementVelocityMultiplier.Evaluate(Mathf.Clamp01(normalizedBurstTime)));
        return DisplacementPeakForwardSpeed * mult;
    }

    /// <summary>蓄力/普攻逻辑用：优先 <see cref="Duration"/>，否则按 Clip 墙钟。</summary>
    public float ResolveLogicalDurationSeconds()
    {
        if (Duration > 0.001f)
        {
            return Duration;
        }

        if (MainClip != null)
        {
            return MainClip.length / Mathf.Max(0.01f, AnimSpeed);
        }

        return 0.4f;
    }

    /// <summary>
    /// Motion 新管线统一时长入口。
    /// Why: 保持旧字段兼容的同时，给 ActionState 明确语义的调用点。
    /// </summary>
    public float ResolveMotionDurationSeconds()
    {
        return ResolveLogicalDurationSeconds();
    }

    /// <summary>按归一化进度计算本帧应叠加的标签并写入 mask（先清除 Phase 再叠加）。</summary>
    public void EvaluatePhaseTags(float normalizedTime, ref GameplayTagMask mask)
    {
        var phaseMask = (ulong)(StateTag.PhaseStartup | StateTag.PhaseActive | StateTag.PhaseRecovery);
        mask.Remove(phaseMask);

        if (Windows == null || Windows.Count == 0)
        {
            return;
        }

        var t = Mathf.Clamp01(normalizedTime);
        for (int i = 0; i < Windows.Count; i++)
        {
            var w = Windows[i];
            if (t >= w.NormalizedStart && t <= w.NormalizedEnd)
            {
                mask.Add(w.ToInternalTagMask());
            }
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Windows == null)
        {
            return;
        }

        var dirty = false;
        for (var i = 0; i < Windows.Count; i++)
        {
            var w = Windows[i];
            if (w.TryMigrateLegacySerializedTags())
            {
                Windows[i] = w;
                dirty = true;
            }
        }

        if (dirty)
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}

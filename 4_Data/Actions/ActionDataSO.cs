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
/// 动作归一化时间轴上的离散瞬移触发点。
/// Why: 瞬移属于单帧事件，不应塞进连续位移曲线。
/// </summary>
[Serializable]
public struct TeleportTrigger
{
    [Tooltip("触发时刻（归一化 0~1）。")]
    [Range(0f, 1f)]
    public float TriggerTime;

    [Tooltip("沿角色前向的瞬移距离（米，可为负表示后撤）。")]
    public float Distance;
}

/// <summary>
/// 数据驱动动作资产 — 意图、时间轴切片、离散事件（如瞬移触发）及动画/剪辑元数据。
/// <para><see cref="MotionProfile"/> 非空则由 MotionExecutor 施加程序化位移；为空则<strong>只做表现播放</strong>（无脚本层位移语义）。</para>
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

    [Header("Motion")]
    [Tooltip(
        "非空：由 MotionExecutor 施加程序化位移（连续曲线等）。为空：不写 Transform，仅凭 MainClip/Duration 由表现层驱动动画（Gameplay 仍可跑标签与时间轴）。Dodge/SwordDash 同上。")]
    public MotionProfileSO MotionProfile;

    [Tooltip("归一化时间轴上的标签切片。")]
    public List<ActionWindow> Windows = new List<ActionWindow>();

    [Header("Charge attack (single MainClip — light tap vs hold-release)")]
    public ActionChargeConfig Charge = new ActionChargeConfig();

    [Header("Teleport (discrete events)")]
    [Tooltip("离散瞬移触发点；仅在归一化时间跨过触发点时执行一次。")]
    public List<TeleportTrigger> TeleportTriggers = new List<TeleportTrigger>();

    /// <summary>
    /// Dodge/SwordDash 等「无 MotionProfile」时：<strong>不与 Duration 争抢</strong>，优先 MainClip 墙钟，语义为按动画实况播完再走逻辑。
    /// </summary>
    public float ResolveAnimWallClockSeconds()
    {
        if (MainClip != null)
        {
            return MainClip.length / Mathf.Max(0.01f, AnimSpeed);
        }

        return ResolveLogicalDurationSeconds();
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

    /// <summary>MotionExecutor 时钟：优先 <see cref="Duration"/>，否则主 Clip 墙钟。</summary>
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

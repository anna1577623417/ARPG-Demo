using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 动作抽象语义类别（用于打断判定）。
/// 不再依赖“轻击/重击/翻滚”等动作名匹配。
/// </summary>
[Flags]
public enum ActionCategory : ushort
{
    None = 0,
    Movement = 1 << 0,
    Offense = 1 << 1,
    Defensive = 1 << 2,
    Utility = 1 << 3,
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

    [Header("Interrupt Semantics (abstract)")]
    [Tooltip("Identity：该动作属于哪类语义（Movement / Offense / Defensive / Utility）。")]
    public ActionCategory Category = ActionCategory.Offense;

    [Tooltip("动作优先级（越大越高）。用于跨技能硬打断比较。")]
    public int InterruptPriority = 10;

    [Tooltip("动作强韧度（Stability）。当来袭优先级 > 本值时，可硬打断。")]
    public int InterruptStability = 10;

    [Tooltip("动作级别自打断开关。窗口未单独允许时，可用它统一放行同动作重入。")]
    public bool AllowSelfInterrupt;

    [Header("Motion")]
    [Tooltip(
        "非空：由 MotionExecutor 施加程序化位移（连续曲线等）。为空：不写 Transform，仅凭 MainClip/Duration 由表现层驱动动画（Gameplay 仍可跑标签与时间轴）。Dodge/SwordDash 同上。")]
    public MotionProfileSO MotionProfile;

    [Tooltip("归一化时间轴上的标签切片。")]
    public List<ActionWindow> Windows = new List<ActionWindow>();

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

    /// <summary>普攻等逻辑用：优先 <see cref="Duration"/>，否则按 Clip 墙钟。</summary>
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

    /// <summary>按归一化进度更新 Phase 位并叠加各 <see cref="ActionWindow"/>；窗口侧贡献 <see cref="ActionWindowTimelineMask"/>（打断 + invulnerable / combo_input_Window）。</summary>
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
                var slice = w.ToInternalTagMask() & ActionWindowTimelineMask.AllContributableBits;
                mask.Add(slice);
            }
        }

        mask.Remove(ActionWindowMergePolicy.StripLegacyCapabilityStateBits);
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

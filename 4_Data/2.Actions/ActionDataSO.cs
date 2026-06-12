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
    /// <summary>战斗位移（翻滚、突进等 SkillEntry 位移）。</summary>
    Movement = 1 << 0,
    Offense = 1 << 1,
    Defensive = 1 << 2,
    Utility = 1 << 3,
    /// <summary>基础 Locomotion（WASD 走/跑、Jump 等全局移动能力）。157.2 B 轴。</summary>
    Locomotion = 1 << 4,
}

/// <summary>A 轴：仲裁车道 — 决定走 Combat Graph 路由还是全局 Action 仲裁。</summary>
public enum ActionIntentCategory : byte
{
    Locomotion = 0,
    Combat = 1,
    Reaction = 2,
    Interaction = 3,
}

/// <summary>C 轴：Action 在 Combat Flow Graph 中的参与身份（157.2 + 157.3）。</summary>
public enum GraphParticipation : sbyte
{
    Auto = -1,
    None = 0,
    SourceOnly = 1,
    Full = 2,
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

    [Tooltip("Clip 播放倍率。(Clip×Ratio)÷Duration；AutoSync 开启时由 MainClip.length÷Duration 自动写入。")]
    [Range(0.1f, 20f)]
    public float AnimSpeed = 1f;

    [Tooltip("勾选 = 自动算 AnimSpeed 让 MainClip 在 Duration 内播完（AnimSpeed = Clip.length ÷ Duration）。")]
    public bool AutoSyncAnimSpeedToDuration = true;

    [Tooltip("逻辑时长（秒）。与动画长度可不同，用于先行手感调参。")]
    public float Duration = 0.4f;

    [Tooltip("Clip 完成比例（仅动画）：Action 结束时 Clip 进度 = nt×Ratio。不裁 Motion 位移。")]
    [Range(0.05f, 2f)]
    public float AnimationEndRatio = 1f;

    [Tooltip("属性缩放逻辑时长：FinalDuration = Duration ÷ GetDurationScale。None = 不缩放。")]
    public MotionScaleType DurationStatScaling = MotionScaleType.None;

    [Tooltip("编舞/速率计算用的主轴位移（米），取自 MotionProfile.AxisCurves t=0→1。")]
    public MotionPrincipalAxis PrincipalAxis = MotionPrincipalAxis.Z;

    [Header("Intent Lane (157.2 A-axis)")]
    [Tooltip("仲裁车道：Combat 走 SkillEntry→Graph；Locomotion/Reaction/Interaction 走全局仲裁。")]
    public ActionIntentCategory IntentCategory = ActionIntentCategory.Combat;

    [Header("Graph Participation (157.2 C-axis)")]
    [Tooltip("Auto：按 IntentCategory 派生（Combat→Full，Locomotion→SourceOnly，其余→None）。")]
    public GraphParticipation GraphParticipation = GraphParticipation.Auto;

    [Header("Interrupt Semantics (abstract)")]
    [Tooltip("Identity：该动作属于哪类语义（Movement / Offense / Defensive / Utility / Locomotion）。")]
    public ActionCategory Category = ActionCategory.Offense;

    [Tooltip("动作优先级（越大越高）。用于跨技能硬打断比较。")]
    public int InterruptPriority = 10;

    [Tooltip("动作强韧度（Stability）。当来袭优先级 > 本值时，可硬打断。")]
    public int InterruptStability = 10;

    [Tooltip("动作级别自打断开关。窗口未单独允许时，可用它统一放行同动作重入。")]
    public bool AllowSelfInterrupt;

    [Header("Locomotion Mode (164.1)")]
    [Tooltip("勾选 = Locomotion State 内循环播放（不切 ActionState）；隐藏离散时长/窗口字段。")]
    public bool IsContinuousLocomotion;

    [Tooltip("L6：本 Action 使用 Clip RootMotion 驱动 transform（与 MotionProfile 曲线二选一，默认关）。")]
    public bool UseClipRootMotion;

    [Tooltip("Continuous Locomotion 期间是否允许 LookAtDirection 逻辑旋转。")]
    public bool CanRotateDuringLocomotion = true;

    [Tooltip("Continuous Locomotion 期间是否允许 Locomotion 程序位移（Walk/Run Loop 通常为 true）。")]
    public bool CanMoveDuringLocomotion = true;

    [Header("Phase Variants (164.1 L10 — 设施就位，默认未通电)")]
    [Tooltip("左脚支撑相位急停变体；空 = MainClip。需 Tuning.EnableFootPhasedStopVariants。")]
    public AnimationClip LeftFootSupportClip;

    [Tooltip("右脚支撑相位急停变体；空 = MainClip。")]
    public AnimationClip RightFootSupportClip;

    [Header("Motion")]
    [Tooltip(
        "非空：由 MotionExecutor 施加程序化位移（连续曲线等）。为空：不写 Transform，仅凭 MainClip/Duration 由表现层驱动动画（Gameplay 仍可跑标签与时间轴）。Dodge/SwordDash 同上。")]
    public MotionProfileSO MotionProfile;

    [Tooltip("归一化时间轴上的标签切片。")]
    public List<ActionWindow> Windows = new List<ActionWindow>();

    [Header("Teleport (discrete events)")]
    [Tooltip("离散瞬移触发点；仅在归一化时间跨过触发点时执行一次。")]
    public List<TeleportTrigger> TeleportTriggers = new List<TeleportTrigger>();

    [Header("Presentation Timeline (139.2 P2/P3)")]
    [Tooltip("FX / Audio / Camera / TimeScale 标记；在时间轴编辑器中配置。")]
    public List<ActionTimelineMarker> TimelineMarkers = new List<ActionTimelineMarker>();

    /// <summary>运行时 AnimSpeed：AutoSync 时让 Clip 墙钟对齐 <see cref="Duration"/>。</summary>
    public float ResolveEffectiveAnimSpeed()
    {
        if (AutoSyncAnimSpeedToDuration && MainClip != null && Duration > 0.001f)
        {
            return Mathf.Max(0.01f, MainClip.length / Duration);
        }

        return Mathf.Max(0.01f, AnimSpeed);
    }

    /// <summary>
    /// Dodge/SwordDash 等「无 MotionProfile」时：AutoSync 下墙钟 = Duration；否则 Clip÷AnimSpeed。
    /// </summary>
    public float ResolveAnimWallClockSeconds()
    {
        if (MainClip != null)
        {
            if (AutoSyncAnimSpeedToDuration && Duration > 0.001f)
            {
                return Duration;
            }

            return MainClip.length / Mathf.Max(0.01f, AnimSpeed);
        }

        return ActionTimeAuthority.ResolveAuthoredLogicDurationSeconds(this);
    }

    /// <summary>普攻等逻辑用：优先 <see cref="Duration"/>，否则 Clip÷AnimSpeed。</summary>
    public float ResolveLogicalDurationSeconds() =>
        ActionTimeAuthority.ResolveAuthoredLogicDurationSeconds(this);

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
        if (IsContinuousLocomotion)
        {
            IntentCategory = ActionIntentCategory.Locomotion;
            GraphParticipation = GraphParticipation.SourceOnly;
            Category = ActionCategory.Locomotion;
            Duration = 0f;
            UseClipRootMotion = false;
            AutoSyncAnimSpeedToDuration = false;
        }
        else if (!AutoSyncAnimSpeedToDuration
                 && MainClip != null
                 && Duration > 0.001f)
        {
            var expectedWall = MainClip.length / Mathf.Max(0.01f, AnimSpeed);
            if (Mathf.Abs(expectedWall - Duration) > 0.05f)
            {
                Debug.LogWarning(
                    $"[ActionData] Duration({Duration:F3}s) 与 Clip÷AnimSpeed({expectedWall:F3}s) 偏差 >0.05s；" +
                    $"建议勾选 AutoSyncAnimSpeedToDuration 或手调 AnimSpeed。 asset={name}",
                    this);
            }
        }

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

        if (dirty || IsContinuousLocomotion)
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}

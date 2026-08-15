using UnityEngine;

/// <summary>
/// 237 L1 — 方向意图时间窗。PreTrigger 默认 0.10s；FacingCommitDelay 默认同 Pre。
/// PostTrigger / ReleaseGrace 本落位可序列化，Tick 忽略。不是 ChordWindowSec。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Locomotion/Directional Timing Profile", fileName = "DirectionalTiming_")]
public sealed class DirectionalTimingProfileSO : ScriptableObject
{
    [Header("Pre-Trigger")]
    [Tooltip("按下方向后、允许提交 CommittedFacing 之前的窗口（秒）。默认 0.10。")]
    [Min(0f)] public float PreTriggerWindowSec = 0.10f;

    [Header("Facing Commit Delay")]
    [Tooltip("勾选后 FacingCommitDelay 跟随 PreTriggerWindow，不再使用下面的独立秒数。")]
    public bool FacingCommitDelayFollowsPreWindow = true;

    [Tooltip("独立延迟（秒）。仅当 FollowPre 关闭时生效。")]
    [Min(0f)] public float FacingCommitDelaySec = 0.10f;

    [Header("Reserved (L1 Tick 忽略)")]
    [Tooltip("保留：技能触发后的补窗口。L1 不接线。")]
    [Min(0f)] public float PostTriggerWindowSec = 0.02f;

    [Tooltip("保留：松手宽限。L1 不接线。")]
    [Min(0f)] public float ReleaseGraceSec = 0.04f;

    [Header("Turn Tap")]
    [Tooltip("Gate 到期时，按住不超过该秒数则定性为 TurnTap；更长的持续按住为 Locomotion。默认 0.14。")]
    [Min(0f)] public float TurnTapMaxDurationSec = 0.14f;

    [Header("Hold Redirect")]
    [Tooltip("持续移动中 DesiredFacing 与已提交朝向的最小夹角（度）。小于该值不发 HoldRedirect。默认 1。")]
    [Min(0f)] public float RedirectFacingMinDeltaDeg = 1f;

    public DirectionalTimingSnapshot Resolve()
    {
        var pre = Mathf.Max(0f, PreTriggerWindowSec);
        var delay = FacingCommitDelayFollowsPreWindow
            ? pre
            : Mathf.Max(0f, FacingCommitDelaySec);
        return new DirectionalTimingSnapshot(
            pre,
            Mathf.Max(0f, PostTriggerWindowSec),
            Mathf.Max(0f, ReleaseGraceSec),
            delay,
            Mathf.Max(0f, TurnTapMaxDurationSec),
            Mathf.Max(0f, RedirectFacingMinDeltaDeg));
    }

    public static DirectionalTimingSnapshot Standard { get; } =
        new DirectionalTimingSnapshot(0.10f, 0.02f, 0.04f, 0.10f, 0.14f, 1f);

    public static DirectionalTimingSnapshot Resolve(
        SkillContextGroupDefinition contextGroup,
        LocomotionTuningSO tuning)
    {
        if (contextGroup != null && contextGroup.TimingProfile != null)
        {
            return contextGroup.TimingProfile.Resolve();
        }

        if (tuning != null && tuning.DirectionalTimingProfile != null)
        {
            return tuning.DirectionalTimingProfile.Resolve();
        }

        return Standard;
    }
}

/// <summary>运行时只读时间窗。由 Profile 或代码默认产生。</summary>
public readonly struct DirectionalTimingSnapshot
{
    public readonly float PreTriggerWindowSec;
    public readonly float PostTriggerWindowSec;
    public readonly float ReleaseGraceSec;
    public readonly float FacingCommitDelaySec;
    public readonly float TurnTapMaxDurationSec;
    public readonly float RedirectFacingMinDeltaDeg;

    public DirectionalTimingSnapshot(
        float preTriggerWindowSec,
        float postTriggerWindowSec,
        float releaseGraceSec,
        float facingCommitDelaySec,
        float turnTapMaxDurationSec,
        float redirectFacingMinDeltaDeg = 1f)
    {
        PreTriggerWindowSec = preTriggerWindowSec;
        PostTriggerWindowSec = postTriggerWindowSec;
        ReleaseGraceSec = releaseGraceSec;
        FacingCommitDelaySec = facingCommitDelaySec;
        TurnTapMaxDurationSec = turnTapMaxDurationSec;
        RedirectFacingMinDeltaDeg = redirectFacingMinDeltaDeg;
    }
}

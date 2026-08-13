using System.Text;
using UnityEngine;

/// <summary>
/// 227.5.1 L0/M1 — Idle/连续表现与双端口 Mixer 专项探针。
/// 仅观测；不改变播放语义。开关：<see cref="GameMainDebugSettings.AnimTransition227Log"/>。
/// </summary>
public static class AnimTransition227Probe
{
    public const string LogPrefix = "[AnimTransition227]";
    public const string SourceDesign = "227";

    static int s_nextTransitionId = 1;
    static int s_activeTransitionId;
    static float s_nextSampleMark = 1f;
    static string s_activeToClip = "";
    static int s_activeInstanceId;

    public static bool IsEnabled => GameMainDebugSettings.AnimTransition227Log;

    public static int PeekNextTransitionId() => s_nextTransitionId;

    public static void LogResolve(
        int instanceId,
        LocomotionStateId resolvedState,
        LocomotionExecutionPolicy policy,
        string reason,
        string executionSource,
        ActionDataSO action,
        AnimationClip continuousClip,
        float blendSec,
        float speed,
        string fallbackReason = null)
    {
        if (!IsEnabled)
        {
            return;
        }

        var sb = Begin("RESOLVE", instanceId, 0, reason);
        AppendCommon(sb, resolvedState, policy, executionSource, action, continuousClip, blendSec, speed, loop: null);
        if (!string.IsNullOrEmpty(fallbackReason))
        {
            sb.Append(" fallbackReason=").Append(fallbackReason);
        }

        Debug.Log(sb.ToString());
    }

    public static void LogRequest(
        int instanceId,
        LocomotionStateId resolvedState,
        LocomotionExecutionPolicy policy,
        string reason,
        ActionDataSO action,
        bool duplicateSkipped)
    {
        if (!IsEnabled)
        {
            return;
        }

        // 227.5.1.1：连续态每帧都会再次命中同一 Action。主链只保留边沿，重复帧不落 Log。
        if (duplicateSkipped)
        {
            return;
        }

        var sb = Begin("REQUEST", instanceId, 0, reason);
        AppendCommon(
            sb,
            resolvedState,
            policy,
            "StateSemantic",
            action,
            action != null ? action.MainClip : null,
            action != null ? action.CrossfadeTime : 0f,
            action != null ? action.AnimSpeed : 1f,
            loop: policy == LocomotionExecutionPolicy.ContinuousPresentation);
        Debug.Log(sb.ToString());
    }

    public static int BeginMixerTransition(
        int instanceId,
        string reason,
        AnimationClip fromClip,
        AnimationClip toClip,
        float blendSec,
        float speed,
        bool loop,
        int previousPort,
        int currentPort,
        float previousWeight,
        float currentWeight,
        double previousLocalTime,
        double currentLocalTime,
        bool supersede)
    {
        if (!IsEnabled)
        {
            return 0;
        }

        var transitionId = s_nextTransitionId++;
        s_activeTransitionId = transitionId;
        s_activeInstanceId = instanceId;
        s_activeToClip = toClip != null ? toClip.name : "";
        // 主链仅保留一次中点快照；25%/75% 对定位本次抢占问题没有新增信息。
        s_nextSampleMark = 0.5f;

        var sb = Begin(supersede ? "SUPERSEDE" : "BEGIN", instanceId, transitionId, reason);
        sb.Append(" fromClip=").Append(SafeClip(fromClip));
        sb.Append(" toClip=").Append(SafeClip(toClip));
        sb.Append(" blendSec=").Append(blendSec.ToString("F3"));
        sb.Append(" speed=").Append(speed.ToString("F3"));
        sb.Append(" loop=").Append(loop);
        AppendPorts(sb, previousPort, currentPort, previousWeight, currentWeight, previousLocalTime, currentLocalTime);
        Debug.Log(sb.ToString());
        return transitionId;
    }

    public static void SampleMixerIfNeeded(
        int instanceId,
        float blendNormalized,
        int previousPort,
        int currentPort,
        float previousWeight,
        float currentWeight,
        double previousLocalTime,
        double currentLocalTime)
    {
        if (!IsEnabled || s_activeTransitionId <= 0 || instanceId != s_activeInstanceId)
        {
            return;
        }

        if (blendNormalized + 0.0001f < s_nextSampleMark || s_nextSampleMark > 0.5f + 0.0001f)
        {
            return;
        }

        var mark = s_nextSampleMark;
        var sb = Begin("SAMPLE", instanceId, s_activeTransitionId, $"blend@{mark:P0}");
        sb.Append(" toClip=").Append(s_activeToClip);
        sb.Append(" blendNorm=").Append(blendNormalized.ToString("F3"));
        AppendPorts(sb, previousPort, currentPort, previousWeight, currentWeight, previousLocalTime, currentLocalTime);
        Debug.Log(sb.ToString());

        s_nextSampleMark = 2f;
    }

    public static void EndMixerTransition(
        int instanceId,
        int previousPort,
        int currentPort,
        float previousWeight,
        float currentWeight,
        double previousLocalTime,
        double currentLocalTime)
    {
        if (!IsEnabled || s_activeTransitionId <= 0 || instanceId != s_activeInstanceId)
        {
            return;
        }

        var sb = Begin("END", instanceId, s_activeTransitionId, "blendComplete");
        sb.Append(" toClip=").Append(s_activeToClip);
        AppendPorts(sb, previousPort, currentPort, previousWeight, currentWeight, previousLocalTime, currentLocalTime);
        Debug.Log(sb.ToString());
        s_activeTransitionId = 0;
        s_nextSampleMark = 1f;
    }

    static StringBuilder Begin(string eventName, int instanceId, int transitionId, string reason)
    {
        var sb = new StringBuilder(256);
        sb.Append(LogPrefix).Append(' ').Append(eventName);
        sb.Append(" sourceDesign=").Append(SourceDesign);
        sb.Append(" transitionId=").Append(transitionId);
        sb.Append(" instanceId=").Append(instanceId);
        sb.Append(" frame=").Append(Time.frameCount);
        sb.Append(" reason=").Append(string.IsNullOrEmpty(reason) ? "-" : reason);
        return sb;
    }

    static void AppendCommon(
        StringBuilder sb,
        LocomotionStateId resolvedState,
        LocomotionExecutionPolicy policy,
        string executionSource,
        ActionDataSO action,
        AnimationClip continuousClip,
        float blendSec,
        float speed,
        bool? loop)
    {
        sb.Append(" resolvedState=").Append(resolvedState);
        sb.Append(" executionPolicy=").Append(policy);
        sb.Append(" executionSource=").Append(executionSource);
        sb.Append(" action=").Append(action != null ? action.name : "null");
        sb.Append(" toClip=").Append(SafeClip(continuousClip));
        sb.Append(" blendSec=").Append(blendSec.ToString("F3"));
        sb.Append(" speed=").Append(speed.ToString("F3"));
        if (loop.HasValue)
        {
            sb.Append(" loop=").Append(loop.Value);
        }
    }

    static void AppendPorts(
        StringBuilder sb,
        int previousPort,
        int currentPort,
        float previousWeight,
        float currentWeight,
        double previousLocalTime,
        double currentLocalTime)
    {
        sb.Append(" previousPort=").Append(previousPort);
        sb.Append(" currentPort=").Append(currentPort);
        sb.Append(" previousWeight=").Append(previousWeight.ToString("F3"));
        sb.Append(" currentWeight=").Append(currentWeight.ToString("F3"));
        sb.Append(" previousLocalTime=").Append(previousLocalTime.ToString("F3"));
        sb.Append(" currentLocalTime=").Append(currentLocalTime.ToString("F3"));
    }

    static string SafeClip(AnimationClip clip) => clip != null ? clip.name : "null";
}

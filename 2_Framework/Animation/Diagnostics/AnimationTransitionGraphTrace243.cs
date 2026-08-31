using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>243.6 — Transition Graph contract trace. It observes presentation facts only.</summary>
public enum AnimationTransitionTraceEventKind : byte
{
    Observe = 0,
    Request = 1,
    Arbiter = 2,
    Plan = 3,
    Play = 4,
    Handoff = 5,
    Stale = 6,
    Fallback = 7,
}

public readonly struct AnimationTransitionTraceEvent243
{
    public readonly AnimationTransitionTraceEventKind Kind;
    public readonly RuntimeStepStamp Step;
    public readonly int EntityInstanceId;
    public readonly ulong RequestId;
    public readonly ulong Generation;
    public readonly AnimationRequestDomain Domain;
    public readonly string Reason;

    public AnimationTransitionTraceEvent243(
        AnimationTransitionTraceEventKind kind,
        in RuntimeStepStamp step,
        int entityInstanceId,
        ulong requestId,
        ulong generation,
        AnimationRequestDomain domain,
        string reason)
    {
        Kind = kind;
        Step = step;
        EntityInstanceId = entityInstanceId;
        RequestId = requestId;
        Generation = generation;
        Domain = domain;
        Reason = reason ?? string.Empty;
    }
}

/// <summary>Per-owner finite edge budget. A missing limiter fails closed and emits no log.</summary>
public sealed class AnimationTransitionTraceLimiter243
{
    readonly int _capacity;
    readonly HashSet<string> _seen = new HashSet<string>();

    public int Count => _seen.Count;
    public int Capacity => _capacity;

    public AnimationTransitionTraceLimiter243(int capacity)
    {
        _capacity = Mathf.Max(1, capacity);
    }

    public bool TryAcquire(in AnimationTransitionTraceEvent243 traceEvent)
    {
        var key = string.Concat(
            ((int)traceEvent.Kind).ToString(), ":",
            traceEvent.EntityInstanceId.ToString(), ":",
            traceEvent.RequestId.ToString(), ":",
            traceEvent.Generation.ToString(), ":",
            ((int)traceEvent.Domain).ToString());

        if (_seen.Contains(key) || _seen.Count >= _capacity)
        {
            return false;
        }

        _seen.Add(key);
        return true;
    }

    public void Clear() => _seen.Clear();
}

public static class AnimationTransitionGraphTrace243
{
    public const string LogPrefix = "[AnimTransition243]";

    public static bool IsEnabled => GameMainDebugSettings.AnimTransitionGraph243Log;

    public static bool TryLog(
        in AnimationTransitionTraceEvent243 traceEvent,
        AnimationTransitionTraceLimiter243 limiter,
        UnityEngine.Object context = null)
    {
        if (!IsEnabled || limiter == null || !limiter.TryAcquire(in traceEvent))
        {
            return false;
        }

        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            context,
            "{0} evt={1} instanceId={2} session={3} logic={4} physics={5} frame={6} requestId={7} generation={8} domain={9} reason={10}",
            LogPrefix,
            traceEvent.Kind,
            traceEvent.EntityInstanceId,
            traceEvent.Step.RuntimeSessionId,
            traceEvent.Step.EntityLogicStepId,
            traceEvent.Step.EntityPhysicsStepId,
            traceEvent.Step.UnityFrame,
            traceEvent.RequestId,
            traceEvent.Generation,
            traceEvent.Domain,
            Sanitize(traceEvent.Reason));
        return true;
    }

    public static string Format(in AnimationTransitionTraceEvent243 traceEvent)
    {
        return string.Concat(
            LogPrefix, " evt=", traceEvent.Kind,
            " instanceId=", traceEvent.EntityInstanceId,
            " session=", traceEvent.Step.RuntimeSessionId,
            " logic=", traceEvent.Step.EntityLogicStepId,
            " physics=", traceEvent.Step.EntityPhysicsStepId,
            " frame=", traceEvent.Step.UnityFrame,
            " requestId=", traceEvent.RequestId,
            " generation=", traceEvent.Generation,
            " domain=", traceEvent.Domain,
            " reason=", Sanitize(traceEvent.Reason));
    }

    static string Sanitize(string value) => string.IsNullOrEmpty(value)
        ? "-"
        : value.Replace('\r', ' ').Replace('\n', ' ');
}

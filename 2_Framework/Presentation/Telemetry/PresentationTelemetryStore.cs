using System.Collections.Generic;
using UnityEngine;

/// <summary>Latest-only diagnostic store. Gameplay never reads this store to make decisions.</summary>
public static class PresentationTelemetryStore
{
    static readonly Dictionary<int, PresentationPlaybackSample> Latest = new Dictionary<int, PresentationPlaybackSample>();
    static readonly Dictionary<int, ulong> Versions = new Dictionary<int, ulong>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForRuntimeSession()
    {
        Latest.Clear();
        Versions.Clear();
    }

    public static ulong NextVersion(int entityInstanceId)
    {
        Versions.TryGetValue(entityInstanceId, out var current);
        current++;
        Versions[entityInstanceId] = current;
        return current;
    }

    public static void Publish(in PresentationPlaybackSample sample)
    {
        if (sample.EntityInstanceId != 0)
        {
            Latest[sample.EntityInstanceId] = sample;
        }
    }

    public static PresentationTelemetryReadStatus TryRead(
        int entityInstanceId,
        uint actionLeaseVersion,
        int actionInstanceId,
        ulong currentLogicStep,
        ulong maxStepAge,
        out PresentationPlaybackSample sample)
    {
        if (!Latest.TryGetValue(entityInstanceId, out sample))
        {
            return PresentationTelemetryReadStatus.Unavailable;
        }

        if (sample.ActionLeaseVersion != actionLeaseVersion)
        {
            return PresentationTelemetryReadStatus.MismatchedLease;
        }

        if (sample.Step.RuntimeSessionId == 0UL
            || sample.Step.RuntimeSessionId != RuntimeSession.CurrentId)
        {
            return PresentationTelemetryReadStatus.Stale;
        }

        if (sample.ActionInstanceId != actionInstanceId)
        {
            return PresentationTelemetryReadStatus.MismatchedAction;
        }

        if (currentLogicStep > sample.Step.EntityLogicStepId
            && currentLogicStep - sample.Step.EntityLogicStepId > maxStepAge)
        {
            return PresentationTelemetryReadStatus.Stale;
        }

        return PresentationTelemetryReadStatus.Available;
    }

    public static void Clear(int entityInstanceId)
    {
        Latest.Remove(entityInstanceId);
        Versions.Remove(entityInstanceId);
    }
}

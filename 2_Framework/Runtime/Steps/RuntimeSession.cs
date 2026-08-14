using System.Threading;
using UnityEngine;

/// <summary>Process-local diagnostic session identity. Reset on Unity subsystem registration.</summary>
public static class RuntimeSession
{
    static long s_nextSessionId;
    static ulong s_currentSessionId;

    public static ulong CurrentId
    {
        get
        {
            if (s_currentSessionId == 0UL)
            {
                s_currentSessionId = unchecked((ulong)Interlocked.Increment(ref s_nextSessionId));
            }
            return s_currentSessionId;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForRuntimeSession()
    {
        s_currentSessionId = 0UL;
    }
}

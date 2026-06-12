using UnityEngine;

/// <summary>三轴 Motion 系统可观测 Log（节流）。</summary>
public static class MotionXYZDebug
{
    public const string CatMotion = "Motion";
    public const string CatComposer = "Composer";
    public const string CatGravity = "Gravity";

    const float SampleLogInterval = 0.1f;

    static float s_nextMotionLogTime;

    public static bool ShouldLogMotionSample => Time.unscaledTime >= s_nextMotionLogTime;

    public static void MarkMotionSampleLogged() => s_nextMotionLogTime = Time.unscaledTime + SampleLogInterval;

    public static void Log(Object owner, string category, string message)
    {
        if (owner is Player p && !p.DebugLocomotion)
        {
            return;
        }

        Debug.Log($"[SkillRoute][{category}] {message}", owner);
    }
}

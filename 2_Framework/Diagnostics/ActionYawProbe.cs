using UnityEngine;

/// <summary>
/// Action Yaw 诊断频道 [ActionYaw] — 开关见 GameMain → Debug → Log Settings。
/// </summary>
public static class ActionYawProbe
{
    const string Prefix = "[ActionYaw]";

    public static bool IsEnabled => GameMainDebugSettings.ActionYawLog;

    public static void LogApply(
        YawPolicyMode policy,
        float normalizedTime,
        float yawDegrees,
        Vector3 prevForward,
        Vector3 nextForward)
    {
        if (!IsEnabled)
        {
            return;
        }

        var delta = Vector3.SignedAngle(prevForward, nextForward, Vector3.up);
        Debug.Log(
            $"{Prefix} policy={policy} nt={normalizedTime:F3} yaw={yawDegrees:F1}° " +
            $"yawDelta={delta:F1}° logicFwd=({nextForward.x:F2},{nextForward.z:F2})");
    }
}

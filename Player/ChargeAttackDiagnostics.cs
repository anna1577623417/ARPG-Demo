using UnityEngine;

/// <summary>
/// 蓄力链路 Console 日志；由 <see cref="Player"/> 勾选 Debug Charge Attack 后生效。
/// </summary>
public static class ChargeAttackDiagnostics
{
    public static bool Enabled { get; set; }

    public static void Log(string message)
    {
        if (!Enabled)
        {
            return;
        }

        Debug.Log($"[ChargeAttack] {message}");
    }
}

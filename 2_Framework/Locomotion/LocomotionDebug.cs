using UnityEngine;

/// <summary>
/// Locomotion 运行时诊断（162.1）—— 稳定前缀 <c>[Loco]</c>，供 Play Mode Console 过滤。
/// <para><see cref="Player.DebugLocomotion"/>：Resolver 决策边沿。</para>
/// <para><see cref="Player.DebugLocomotionTrace"/>：输入 / 移动 / 转身节流心跳 + 异常告警。</para>
/// </summary>
public static class LocomotionDebug
{
    public const string Prefix = "[Loco]";

    public const string CatInput = "Input";
    public const string CatMove = "Move";
    public const string CatTurn = "Turn";
    public const string CatRotation = "Rot";
    public const string CatResolve = "Resolve";

    const float DefaultTraceInterval = 0.12f;

    public static bool IsEnabled(Player player) => player != null && player.DebugLocomotion;

    public static bool IsTraceEnabled(Player player) =>
        player != null && (player.DebugLocomotionTrace || player.DebugLocomotion);

    public static void Log(Player player, string category, string message, Object context = null)
    {
        if (!IsEnabled(player))
        {
            return;
        }

        Debug.Log($"{Prefix}[{category}] {message}", context != null ? context : player);
    }

    /// <summary>164.1 L0：<c>[Turn][Detect/FIRE/SKIP]</c> 与蓝图过滤前缀一致（仍受 DebugLocomotion 门控）。</summary>
    public static void LogTurnPhase(Player player, string phase, string message, Object context = null)
    {
        if (!IsEnabled(player))
        {
            return;
        }

        Debug.Log($"[Turn][{phase}] {message}", context != null ? context : player);
    }

    public static void LogTrace(
        Player player,
        string category,
        string message,
        ref float nextLogTime,
        float interval = DefaultTraceInterval,
        Object context = null)
    {
        if (!IsTraceEnabled(player))
        {
            return;
        }

        var now = Time.unscaledTime;
        if (now < nextLogTime)
        {
            return;
        }

        nextLogTime = now + Mathf.Max(0.05f, interval);
        Debug.Log($"{Prefix}[{category}] {message}", context != null ? context : player);
    }

    public static void LogWarnOnce(
        Player player,
        ref bool logged,
        string category,
        string message,
        Object context = null)
    {
        if (logged || player == null)
        {
            return;
        }

        logged = true;
        Debug.LogWarning($"{Prefix}[{category}] {message}", context != null ? context : player);
    }

    /// <summary>
    /// 相机相对移动疑似失效：有侧向输入但世界意图仍贴近角色前向。
    /// </summary>
    public static void TryLogCameraRelativeAnomaly(
        Player player,
        Vector2 rawInput,
        Vector3 worldDir,
        Vector3 cameraFlatFwd,
        Vector3 characterFlatFwd,
        bool cameraRelative,
        string ctxSource,
        ref float nextAnomalyLogTime)
    {
        if (!IsTraceEnabled(player) || rawInput.sqrMagnitude < 0.0001f)
        {
            return;
        }

        var ax = Mathf.Abs(rawInput.x);
        var ay = Mathf.Abs(rawInput.y);
        if (ax < 0.25f)
        {
            return;
        }

        worldDir.y = 0f;
        characterFlatFwd.y = 0f;
        cameraFlatFwd.y = 0f;
        if (worldDir.sqrMagnitude < 1e-6f || characterFlatFwd.sqrMagnitude < 1e-6f)
        {
            return;
        }

        worldDir.Normalize();
        characterFlatFwd.Normalize();
        if (cameraFlatFwd.sqrMagnitude > 1e-6f)
        {
            cameraFlatFwd.Normalize();
        }

        var dotChar = Vector3.Dot(worldDir, characterFlatFwd);
        var dotCam = cameraFlatFwd.sqrMagnitude > 1e-6f ? Vector3.Dot(worldDir, cameraFlatFwd) : 0f;

        // 按 A/D 时世界意图应偏离角色前向；若仍高度对齐角色前向 → 相机相对可能断了。
        var alignedWithCharacter = dotChar > 0.92f;
        var misalignedWithCamera = cameraFlatFwd.sqrMagnitude > 1e-6f && dotCam < 0.35f;
        if (!alignedWithCharacter || !misalignedWithCamera)
        {
            return;
        }

        LogTrace(
            player,
            CatInput,
            $"ANOMALY camera-relative? raw=({rawInput.x:F2},{rawInput.y:F2}) " +
            $"worldDir=({worldDir.x:F2},{worldDir.z:F2}) dotChar={dotChar:F2} dotCam={dotCam:F2} " +
            $"camRel={cameraRelative} ctx={ctxSource}",
            ref nextAnomalyLogTime,
            interval: 0.35f);
    }
}

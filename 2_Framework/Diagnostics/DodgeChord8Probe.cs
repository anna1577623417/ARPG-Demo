#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

/// <summary>
/// 206.1 — 方向输入双模式诊断频道 [DodgeChord8]。
///
/// 与 [SkillRoute][Dodge4] 区分开 — 专门追踪 "WSAD 按下到 Space 按下的间隔" 与 Chord/Motion 模式决策。
///
/// 启用：Tools → Skill Route → Enable DodgeChord8 Log（EditorPrefs 持久化）
/// 节流：MoveDown / AbilityDown / PICK 都不节流（事件型 log），Resolve 类型按 tag 节流 0.2s
/// </summary>
public static class DodgeChord8Probe
{
    const string Prefix = "[DodgeChord8]";
    const string SplitFramePrefix = "[SplitFrame]";
    const string LogPrefKey = "Core-Drive/DodgeChord8Probe/EnableLog";

#if UNITY_EDITOR
    static bool s_enabled;

    static DodgeChord8Probe()
    {
        s_enabled = EditorPrefs.GetBool(LogPrefKey, false);
    }

    [MenuItem("Tools/Skill Route/Enable DodgeChord8 Log", false, 220)]
    static void ToggleLog()
    {
        s_enabled = !s_enabled;
        EditorPrefs.SetBool(LogPrefKey, s_enabled);
        Debug.Log($"{Prefix} log {(s_enabled ? "ENABLED" : "DISABLED")}");
    }

    [MenuItem("Tools/Skill Route/Enable DodgeChord8 Log", true)]
    static bool ToggleLogValidate()
    {
        Menu.SetChecked("Tools/Skill Route/Enable DodgeChord8 Log", s_enabled);
        return true;
    }
#else
    const bool s_enabled = false;
#endif

    public static bool IsEnabled => s_enabled;

    /// <summary>方向键首次按下沿（InputContextResolver 第一次 MoveActive=true 那帧）。</summary>
    public static void LogMoveDown(Vector2 moveBuffered, Vector3 cameraForward, Vector3 logicForward, float now)
    {
        if (!s_enabled) return;
        Debug.Log(
            $"{Prefix} MoveDown t={now:F3} moveBuf=({moveBuffered.x:F2},{moveBuffered.y:F2}) " +
            $"cameraFwd=({cameraForward.x:F2},{cameraForward.z:F2}) " +
            $"logicFwd=({logicForward.x:F2},{logicForward.z:F2})");
    }

    /// <summary>Space 脉冲派发前 — 对比 buffer / MoveInput / InputContext 状态（相机旋转场景专用）。</summary>
    public static void LogDispatchPulse(
        SkillEntrySlot slot,
        Vector2 bufferAxis,
        bool bufferValid,
        Vector2 moveInput,
        bool moveActive,
        float moveActiveSince,
        bool directionalCommitted,
        bool loadoutDirectionalModifier,
        string branchNote)
    {
        if (!s_enabled) return;
        var holdDur = moveActive && moveActiveSince > 0f ? Time.time - moveActiveSince : -1f;
        Debug.Log(
            $"{Prefix} Dispatch slot={slot} buf=({bufferAxis.x:F2},{bufferAxis.y:F2}) bufValid={bufferValid} " +
            $"moveInput=({moveInput.x:F2},{moveInput.y:F2}) ctxActive={moveActive} holdDur={holdDur:F3}s " +
            $"committed={directionalCommitted} loadoutDir={loadoutDirectionalModifier} {branchNote}");
    }

    /// <summary>Semantic 层 Tap / Directional 分流。</summary>
    public static void LogSemanticBranch(
        SkillEntrySlot slot,
        InputSemanticType semantic,
        Vector2 moveBuffered,
        bool moveBufferValid,
        Vector2 directionAxis)
    {
        if (!s_enabled) return;
        Debug.Log(
            $"{Prefix} Semantic slot={slot} → {semantic} moveBuf=({moveBuffered.x:F2},{moveBuffered.y:F2}) " +
            $"bufValid={moveBufferValid} dirAxis=({directionAxis.x:F2},{directionAxis.y:F2})");
    }

    /// <summary>Space（或方向技能）按下时 — 给出 holdDur + 模式决策 + 朝向快照。</summary>
    public static void LogAbilityDown(
        float now,
        float moveActiveSince,
        Vector2 moveBuffered,
        float chordWindowSec,
        float motionWindowSec,
        string modeDecision,
        bool moveActive = true,
        bool directionalCommitted = false,
        Vector3 logicForward = default,
        Vector3 cameraForward = default)
    {
        if (!s_enabled) return;
        var holdDur = moveActive && moveActiveSince > 0f ? now - moveActiveSince : -1f;
        Debug.Log(
            $"{Prefix} AbilityDown t={now:F3} holdDur={holdDur:F3}s ctxActive={moveActive} committed={directionalCommitted} " +
            $"moveBuf=({moveBuffered.x:F2},{moveBuffered.y:F2}) " +
            $"logicFwd=({logicForward.x:F2},{logicForward.z:F2}) camFwd=({cameraForward.x:F2},{cameraForward.z:F2}) " +
            $"chordWin={chordWindowSec:F2}s motionWin={motionWindowSec:F2}s → mode={modeDecision}");
    }

    /// <summary>选路判定为中性 Fallback — 记录「为何视为无方向」。</summary>
    public static void LogNeutralFallback(
        InputSemanticType semantic,
        Vector2 intentAxis,
        Vector2 snapshotBuffer,
        Vector2 liveMoveInput,
        bool moveActive,
        float holdDur,
        string routeName)
    {
        if (!s_enabled) return;
        Debug.Log(
            $"{Prefix} NEUTRAL→Fallback semantic={semantic} intentAxis=({intentAxis.x:F2},{intentAxis.y:F2}) " +
            $"snapBuf=({snapshotBuffer.x:F2},{snapshotBuffer.y:F2}) liveMove=({liveMoveInput.x:F2},{liveMoveInput.y:F2}) " +
            $"ctxActive={moveActive} holdDur={holdDur:F3}s route={routeName ?? "(null)"} " +
            $"reason=axis_deadzone");
    }

    /// <summary>Chord 态解析结果 — moveBuf → 8 向。</summary>
    public static void LogChordResolve(Vector2 moveBuffered, DirectionalRouteType result)
    {
        if (!s_enabled) return;
        Debug.Log(
            $"{Prefix} Chord moveBuf=({moveBuffered.x:F2},{moveBuffered.y:F2}) " +
            $"→ {result} (camera-relative)");
    }

    /// <summary>Motion 态解析结果 — 强制 Forward + 沿 LogicForward。</summary>
    public static void LogMotionResolve(Vector3 logicForward, float holdDur)
    {
        if (!s_enabled) return;
        Debug.Log(
            $"{Prefix} Motion holdDur={holdDur:F3}s logicFwd=({logicForward.x:F2},{logicForward.z:F2}) " +
            $"→ Forward (沿 LogicForward 持续移动方向)");
    }

    /// <summary>最终选中的 Route — 完整链路收口。</summary>
    public static void LogPick(string mode, DirectionalRouteType resolvedDir, string routeName)
    {
        if (!s_enabled) return;
        Debug.Log($"{Prefix} PICK mode={mode} dir={resolvedDir} route={routeName ?? "(null)"}");
    }

    /// <summary>209.3 — SplitFrame 选路收口（输入分轨 + 位移分轨 + PICK）。</summary>
    public static void LogSplitFramePick(
        DirectionalInputFrame inputFrame,
        MotionSpace motionBasis,
        Vector2 moveBuffered,
        DirectionalRouteType resolvedDir,
        string routeName)
    {
        if (!s_enabled) return;
        Debug.Log(
            $"{SplitFramePrefix} input={inputFrame} motion={motionBasis} " +
            $"moveBuf=({moveBuffered.x:F2},{moveBuffered.y:F2}) PICK={resolvedDir} " +
            $"route={routeName ?? "(null)"}");
    }

    /// <summary>209.2 遗留 — LogicProjected 重映射；209.3 后应不再出现。</summary>
    [System.Obsolete("209.3 BodyFixed 输入分轨下不应再 ChordReframe")]
    public static void LogChordReframe(
        DirectionalRouteType cameraDir,
        DirectionalRouteType characterDir,
        MotionSpace space)
    {
        if (!s_enabled) return;
        Debug.Log(
            $"{Prefix} ChordReframe camera={cameraDir} → character={characterDir} motionSpace={space}");
    }

    /// <summary>206.5 / 209.2 — Directional Commit 朝向。</summary>
    public static void LogDirectionalCommit(
        Vector3 committedForward,
        Vector3 capturedAtMoveDown,
        Vector3 liveLogicForward,
        float holdDur,
        float chordWin,
        string commitSource)
    {
        if (!s_enabled) return;
        Debug.Log(
            $"{Prefix} Commit fwd=({committedForward.x:F2},{committedForward.z:F2}) " +
            $"source={commitSource} " +
            $"captured=({capturedAtMoveDown.x:F2},{capturedAtMoveDown.z:F2}) " +
            $"live=({liveLogicForward.x:F2},{liveLogicForward.z:F2}) " +
            $"holdDur={holdDur:F3}s chordWin={chordWin:F2}s");
    }
}

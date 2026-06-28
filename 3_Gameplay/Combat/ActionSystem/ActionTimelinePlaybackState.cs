using System.Collections.Generic;

/// <summary>单次 Action 播放内的时间轴去重状态。</summary>
public sealed class ActionTimelinePlaybackState
{
    readonly HashSet<int> _firedMarkerIndices = new HashSet<int>();
    readonly HashSet<int> _firedTeleportIndices = new HashSet<int>();
    readonly HashSet<int> _firedWindowEventKeys = new HashSet<int>();

    internal bool CameraLockActive { get; set; }
    internal bool TimeScaleZoneActive { get; set; }

    public void Reset()
    {
        _firedMarkerIndices.Clear();
        _firedTeleportIndices.Clear();
        _firedWindowEventKeys.Clear();
        _firedCombatEventIndices.Clear();
        CameraLockActive = false;
        TimeScaleZoneActive = false;
    }

    public void OnActionExit(ActionCameraController camera, ActionTimeScaleDriver timeScale)
    {
        if (CameraLockActive)
        {
            camera?.SetLookInputLocked(false);
            CameraLockActive = false;
        }

        if (TimeScaleZoneActive)
        {
            timeScale?.PopZoneScale();
            TimeScaleZoneActive = false;
        }
    }

    internal bool TryFireMarkerOnce(int index) => _firedMarkerIndices.Add(index);
    internal bool TryFireTeleportOnce(int index) => _firedTeleportIndices.Add(index);
    internal bool TryFireWindowEventOnce(int key) => _firedWindowEventKeys.Add(key);

    // 188.3 W9 — Combat Track 去重
    readonly HashSet<int> _firedCombatEventIndices = new HashSet<int>();
    internal bool TryFireCombatEventOnce(int index) => _firedCombatEventIndices.Add(index);
}

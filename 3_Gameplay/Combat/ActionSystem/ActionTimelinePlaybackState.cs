using System.Collections.Generic;

/// <summary>单次 Action 播放内的时间轴去重状态。</summary>
public sealed class ActionTimelinePlaybackState
{
    readonly HashSet<int> _firedMarkerIndices = new HashSet<int>();
    readonly HashSet<int> _firedTeleportIndices = new HashSet<int>();
    readonly HashSet<int> _firedWindowEventKeys = new HashSet<int>();

    // 216.3 M1 L2 — 本次 Action 播放内、按 HitClip 下标持有的攻击实例（Active 区间驱动）。
    readonly List<AttackInstance> _attacks = new List<AttackInstance>();

    // 216.3 M5 L1 — 按 DefenseClip 下标持有的 GuardVolume（仅 Kind=Guard 开窗）。
    readonly List<GuardVolumeProvider> _guards = new List<GuardVolumeProvider>();

    internal bool CameraLockActive { get; set; }
    internal bool TimeScaleZoneActive { get; set; }

    /// <summary>按 HitClip 下标获取（或惰性创建）攻击实例。</summary>
    public AttackInstance GetOrCreateAttack(int index)
    {
        while (_attacks.Count <= index)
        {
            _attacks.Add(new AttackInstance());
        }

        return _attacks[index];
    }

    /// <summary>按 DefenseClip 下标获取（或惰性创建）GuardVolume。</summary>
    public GuardVolumeProvider GetOrCreateGuard(int index)
    {
        while (_guards.Count <= index)
        {
            _guards.Add(new GuardVolumeProvider());
        }

        return _guards[index];
    }

    public void Reset(Entity owner = null)
    {
        _firedMarkerIndices.Clear();
        _firedTeleportIndices.Clear();
        _firedWindowEventKeys.Clear();
        _firedCombatEventIndices.Clear();
        _firedDefenseWindowIndices.Clear();

        // 换 Action（含被打断）：结束任何仍开判的攻击，避免残留 Active / 幽灵命中去重。
        for (var i = 0; i < _attacks.Count; i++)
        {
            if (_attacks[i] != null && _attacks[i].Active)
            {
                _attacks[i].End();
            }
        }

        _attacks.Clear();

        for (var i = 0; i < _guards.Count; i++)
        {
            if (_guards[i] != null && _guards[i].Active)
            {
                _guards[i].End();
            }
        }

        _guards.Clear();

        // 216.3 M5 L2：清掉 Parry/Invincible 窗标志，避免 Action 退出后仍 Blocked/Parry。
        if (owner != null)
        {
            DefenseRuntimeRegistry.Clear(owner);
        }

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

    // 216.3 M5 L1 — Parry/Invincible 开窗边沿 Log 去重（按 DefenseClip 下标）
    readonly HashSet<int> _firedDefenseWindowIndices = new HashSet<int>();
    internal bool TryFireDefenseWindowOnce(int index) => _firedDefenseWindowIndices.Add(index);
}

using System.Collections.Generic;

/// <summary>单次 Action 播放内的时间轴去重状态。</summary>
public sealed class ActionTimelinePlaybackState
{
    readonly HashSet<int> _firedMarkerIndices = new HashSet<int>();
    readonly HashSet<int> _firedTeleportIndices = new HashSet<int>();
    readonly HashSet<int> _firedWindowEventKeys = new HashSet<int>();

    readonly Dictionary<string, AttackInstance> _contacts =
        new Dictionary<string, AttackInstance>(8);
    readonly HashSet<string> _rejectedContactIds = new HashSet<string>();

    // 216.3 M5 L1 — 按 DefenseClip 下标持有的 GuardVolume（仅 Kind=Guard 开窗）。
    readonly List<GuardVolumeProvider> _guards = new List<GuardVolumeProvider>();

    internal bool CameraLockActive { get; set; }
    internal bool TimeScaleZoneActive { get; set; }

    /// <summary>按稳定 EventId 获取 Contact Window 宿主；数组重排不会改变运行实例身份。</summary>
    public AttackInstance GetOrCreateContact(string eventId)
    {
        if (!_contacts.TryGetValue(eventId, out var instance))
        {
            instance = new AttackInstance();
            _contacts.Add(eventId, instance);
        }

        return instance;
    }

    public bool TryGetContact(string eventId, out AttackInstance instance) =>
        _contacts.TryGetValue(eventId, out instance);

    public bool IsContactRejected(string eventId) => _rejectedContactIds.Contains(eventId);
    public bool RejectContactOnce(string eventId) => _rejectedContactIds.Add(eventId);

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

        foreach (var pair in _contacts)
        {
            var contact = pair.Value;
            if (contact != null && contact.Active)
            {
                contact.End();
            }
        }

        _contacts.Clear();
        _rejectedContactIds.Clear();

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

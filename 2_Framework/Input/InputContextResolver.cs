using UnityEngine;

/// <summary>
/// 173.1 — 输入上下文解析：WASD + 方向技能键在短窗口内组合为单一 Ability 意图，
/// 期间冻结 Locomotion 转向，避免 D→转15°→Space 右翻滚变成右后翻。
/// 173.3-B — 松手 grace 内继承上一次身体朝向快照（跑→松手→反向 Space）。
/// </summary>
public sealed class InputContextResolver
{
    const float DefaultDirectionGraceSec = 0.12f;

    bool _loadoutHasDirectionalModifier;
    bool _moveActive;
    float _moveActiveSince = -999f;
    Vector3 _capturedPlanarForward = Vector3.forward;
    bool _contextRotationSuppressed;
    float _contextSuppressUntil = -999f;
    bool _directionalCommitted;
    Vector3 _committedPlanarForward = Vector3.forward;

    bool _capturedDirty;
    float _lastMoveReleaseTime = -999f;

    // ─── 173.1.B 一对一探针：仅在 _contextRotationSuppressed 翻转时输出一条，避免刷屏 ───
    public bool DebugRotationGate;
    bool _lastSuppressedLogged;
    string _ownerLabel = "Player";
    public void SetDebugOwnerLabel(string label) { _ownerLabel = string.IsNullOrEmpty(label) ? "Player" : label; }

    public bool LoadoutHasDirectionalModifier => _loadoutHasDirectionalModifier;
    public bool MoveActive => _moveActive;
    public bool DirectionalCommitted => _directionalCommitted;
    public float MoveActiveSince => _moveActiveSince;

    /// <summary>206.1 — 方向键按下后已持续秒数；未按下 → -1。</summary>
    public float MoveHoldDurationSec(float now)
    {
        if (!_moveActive || _moveActiveSince < 0f)
        {
            return -1f;
        }

        return now - _moveActiveSince;
    }

    public void SetLoadoutHasDirectionalModifier(bool enabled)
    {
        _loadoutHasDirectionalModifier = enabled;
        if (!enabled)
        {
            ClearAll();
        }
    }

    /// <summary>每帧在离散意图消费之前调用（PlayerController）。</summary>
    public void TickMoveContext(
        Vector2 rawMove,
        float moveDeadZone,
        float now,
        in Vector3 planarForward,
        float contextWindowSec,
        float directionGraceSec = DefaultDirectionGraceSec)
    {
        if (!_loadoutHasDirectionalModifier || _directionalCommitted)
        {
            return;
        }

        var hasMove = rawMove.sqrMagnitude > moveDeadZone * moveDeadZone;
        if (hasMove)
        {
            if (!_moveActive)
            {
                _moveActive = true;
                _moveActiveSince = now;
                // 208.1 — 每次 MoveDown 刷新捕获；Locomotion 运行中 LogicForward 会变，不可继承旧键快照。
                _capturedPlanarForward = planarForward;

                _contextRotationSuppressed = true;
                _contextSuppressUntil = now + Mathf.Max(0.02f, contextWindowSec);
                LogGateIfFlipped(now);
                // 206.1 — 方向键按下沿，输出 [DodgeChord8] MoveDown
                DodgeChord8Probe.LogMoveDown(rawMove, Vector3.zero, planarForward, now);
            }
            else
            {
                // 持续按住期间同步 LogicForward，避免 Commit 时 captured 与 live 大幅偏离。
                _capturedPlanarForward = planarForward;

                if (_contextRotationSuppressed && now > _contextSuppressUntil)
                {
                    _contextRotationSuppressed = false;
                    LogGateIfFlipped(now);
                }
            }

            return;
        }

        if (_moveActive)
        {
            _moveActive = false;
            _lastMoveReleaseTime = now;
            _capturedDirty = false;
        }

        if (now > _contextSuppressUntil)
        {
            _contextRotationSuppressed = false;
            LogGateIfFlipped(now);
        }
    }

    /// <summary>身体朝向已与输入意图对齐后调用，结束 grace 继承（173.3-B）。</summary>
    public void InvalidateCapturedForward()
    {
        _capturedDirty = true;
    }

    void LogGateIfFlipped(float now)
    {
        if (!DebugRotationGate) return;
        if (_contextRotationSuppressed == _lastSuppressedLogged) return;
        _lastSuppressedLogged = _contextRotationSuppressed;
        Debug.Log(
            $"[RotGate] owner={_ownerLabel} suppressed={_contextRotationSuppressed} " +
            $"directionalLoadout={_loadoutHasDirectionalModifier} committed={_directionalCommitted} " +
            $"moveActive={_moveActive} winLeft={Mathf.Max(0f, _contextSuppressUntil - now):F3}s " +
            $"t={now:F2} frame={Time.frameCount}");
    }

    /// <summary>
    /// Directional 语义入队时调用 — 锁定 Motion 基底（仅 CharacterForward Profile 读取）。
    /// 209.2 — Chord/Motion 均 commit live LogicForward；CameraForward Profile 走 MotionSpaceBasis，不读本字段。
    /// </summary>
    public void CommitDirectionalAbility(
        in Vector3 fallbackPlanarForward,
        float holdDurationSec,
        float chordWindowSec)
    {
        if (!_loadoutHasDirectionalModifier)
        {
            return;
        }

        _directionalCommitted = true;
        var useChordWindow = _moveActive
            && holdDurationSec >= 0f
            && holdDurationSec <= chordWindowSec;
        _committedPlanarForward = Planarize(fallbackPlanarForward);
        var commitSource = useChordWindow ? "characterForward@Chord" : "liveLogicForward@Motion";

        _contextRotationSuppressed = true;
        _capturedDirty = true;

        DodgeChord8Probe.LogDirectionalCommit(
            _committedPlanarForward,
            _capturedPlanarForward,
            fallbackPlanarForward,
            holdDurationSec,
            chordWindowSec,
            commitSource);
    }

    public void ClearDirectionalActionContext()
    {
        _directionalCommitted = false;
        _contextRotationSuppressed = false;
        _moveActive = false;
        _contextSuppressUntil = -999f;
        _capturedDirty = false;
    }

    public void ClearAll()
    {
        ClearDirectionalActionContext();
        _loadoutHasDirectionalModifier = false;
        _moveActiveSince = -999f;
        _lastMoveReleaseTime = -999f;
        _capturedPlanarForward = Vector3.forward;
    }

    public RotationArbitrationPolicy ResolvePolicy(float now)
    {
        if (_directionalCommitted)
        {
            return RotationArbitrationPolicy.FrozenDuringDirectionalAction;
        }

        if (_loadoutHasDirectionalModifier
            && _contextRotationSuppressed
            && now <= _contextSuppressUntil)
        {
            return RotationArbitrationPolicy.DelayedDuringAbilityContext;
        }

        return RotationArbitrationPolicy.Immediate;
    }

    public bool ShouldSuppressLocomotionRotation(float now) =>
        ResolvePolicy(now) != RotationArbitrationPolicy.Immediate;

    public bool TryGetDirectionalMotionForward(out Vector3 planarForward)
    {
        if (_directionalCommitted)
        {
            planarForward = _committedPlanarForward;
            return true;
        }

        planarForward = default;
        return false;
    }

    static Vector3 Planarize(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }
}

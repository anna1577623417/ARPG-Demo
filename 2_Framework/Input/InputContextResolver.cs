using UnityEngine;

/// <summary>
/// 173.1 — 输入上下文解析：WASD + 方向技能键在短窗口内组合为单一 Ability 意图，
/// MoveDown 只保存 Ability 基准快照；只有方向技能真正 Commit 后才冻结 Locomotion Facing。
/// 173.3-B — 松手 grace 内继承上一次身体朝向快照（跑→松手→反向 Space）。
/// </summary>
public sealed class InputContextResolver
{
    const float DefaultDirectionGraceSec = 0.12f;

    bool _loadoutHasDirectionalModifier;
    bool _moveActive;
    float _moveActiveSince = -999f;
    Vector3 _capturedPlanarForward = Vector3.forward;
    bool _directionalCommitted;
    Vector3 _committedPlanarForward = Vector3.forward;

    // ─── 234.5：仅在 Commit 冻结状态翻转时输出一条，避免刷屏 ───
    public bool DebugRotationGate;
    bool _lastSuppressedLogged;
    string _ownerLabel = "Player";
    public void SetDebugOwnerLabel(string label) { _ownerLabel = string.IsNullOrEmpty(label) ? "Player" : label; }

    public bool LoadoutHasDirectionalModifier => _loadoutHasDirectionalModifier;
    public bool MoveActive => _moveActive;
    public bool DirectionalCommitted => _directionalCommitted;
    public float MoveActiveSince => _moveActiveSince;

    /// <summary>234.5：MoveDown 时冻结的身体平面朝向，仅供组合技能语义消费，不冻结 Locomotion。</summary>
    public bool TryGetMoveDownPlanarForward(out Vector3 planarForward)
    {
        if (_moveActiveSince > -900f)
        {
            planarForward = _capturedPlanarForward;
            return true;
        }

        planarForward = default;
        return false;
    }

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
        // 234.5：保留历史 API 参数，避免既有配置与调用点发生签名迁移；
        // FreeLocomotion 已不再使用预防性 context/grace 窗口冻结朝向。
        _ = contextWindowSec;
        _ = directionGraceSec;

        if (!_loadoutHasDirectionalModifier)
        {
            return;
        }

        var hasMove = rawMove.sqrMagnitude > moveDeadZone * moveDeadZone;

        // 212.2 — Commit 期间仍跟踪松手，避免 moveActive 脏留；Hold 计时不在此清。
        if (_directionalCommitted)
        {
            if (!hasMove && _moveActive)
            {
                _moveActive = false;
            }

            return;
        }

        if (hasMove)
        {
            if (!_moveActive)
            {
                var prevSince = _moveActiveSince;
                _moveActive = true;
                _moveActiveSince = now;
                _capturedPlanarForward = planarForward;

                LogGateIfFlipped(now);
                HoldMotionDodgeProbe.LogMoveDown(
                    now,
                    prevSince,
                    prevSince > -900f,
                    rawMove,
                    prevSince > -900f ? "re-press" : "fresh");
                DodgeChord8Probe.LogMoveDown(rawMove, Vector3.zero, planarForward, now);
            }
            else
            {
                // 234.5：MoveDown 快照不可变。持续移动只更新 Hold 计时，不能覆盖技能基准。
            }

            return;
        }

        if (_moveActive)
        {
            _moveActive = false;
        }

        LogGateIfFlipped(now);
    }

    void LogGateIfFlipped(float now)
    {
        if (!DebugRotationGate) return;
        var suppressed = _directionalCommitted;
        if (suppressed == _lastSuppressedLogged) return;
        _lastSuppressedLogged = suppressed;
        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            null,
            "[RotGate] owner={0} suppressed={1} directionalLoadout={2} committed={3} moveActive={4} t={5:F2} frame={6}",
            _ownerLabel,
            suppressed,
            _loadoutHasDirectionalModifier,
            _directionalCommitted,
            _moveActive,
            now,
            Time.frameCount);
    }

    /// <summary>
    /// Directional 语义入队时调用 — 锁定 Motion 基底。
    /// 213.6 契约：CharacterForward Profile 读 _committedPlanarForward；
    /// Chord / Motion 均由 Player 传入 live LogicForward（characterForward@Chord / liveLogicForward@Motion）。
    /// 禁止 cameraAxis@Chord — 见 213.5 / 213.6 蓝图。
    /// </summary>
    public void CommitDirectionalAbility(
        in Vector3 committedPlanarForward,
        in Vector3 livePlanarForward,
        float holdDurationSec,
        float chordWindowSec,
        string commitSource)
    {
        if (!_loadoutHasDirectionalModifier)
        {
            return;
        }

        _directionalCommitted = true;
        _committedPlanarForward = Planarize(committedPlanarForward);

        LogGateIfFlipped(Time.time);

        DodgeChord8Probe.LogDirectionalCommit(
            _committedPlanarForward,
            _capturedPlanarForward,
            livePlanarForward,
            holdDurationSec,
            chordWindowSec,
            commitSource);
        DirectionalInputDiagProbe.LogCommit(
            _committedPlanarForward,
            _capturedPlanarForward,
            livePlanarForward,
            holdDurationSec,
            chordWindowSec,
            commitSource);
    }

    /// <summary>Directional 动作结束 — 清 Commit；若 WASD 仍按住则保留 MoveHold 计时（212.2）。</summary>
    public void ClearDirectionalActionContext(Vector2 liveMoveInput = default, float moveDeadZone = 0.12f)
    {
        var moveActiveBefore = _moveActive;
        var moveActiveSinceBefore = _moveActiveSince;
        var committedBefore = _directionalCommitted;
        var preserveMoveHold = liveMoveInput.sqrMagnitude > moveDeadZone * moveDeadZone;

        _directionalCommitted = false;
        LogGateIfFlipped(Time.time);

        if (!preserveMoveHold)
        {
            _moveActive = false;
        }

        HoldMotionDodgeProbe.LogContextClear(
            "ClearDirectionalActionContext",
            preserveMoveHold,
            moveActiveBefore,
            moveActiveSinceBefore,
            liveMoveInput,
            committedBefore);
    }

    public void ClearAll()
    {
        ClearDirectionalActionContext();
        _loadoutHasDirectionalModifier = false;
        _moveActiveSince = -999f;
        _capturedPlanarForward = Vector3.forward;
    }

    public RotationArbitrationPolicy ResolvePolicy(float now)
    {
        if (_directionalCommitted)
        {
            return RotationArbitrationPolicy.FrozenDuringDirectionalAction;
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

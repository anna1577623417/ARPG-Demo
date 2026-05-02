using UnityEngine;

/// <summary>
/// 玩家动画控制器 — 表现层的"主动响应者"（监听者模式）。
///
/// ═══ 核心认知：没有任何脚本"直接调用"动画控制器 ═══
///
/// 逻辑层（状态机/ActionState）不持有本类引用，不调用任何 Play 方法。
/// 本类通过订阅 LocalEventBus 上的事件，主动响应并驱动 Playables API。
/// 禁用本组件不会导致空引用 — 逻辑层照常运转，只是没有视觉表现。
///
/// ═══ 三通道动画驱动 ═══
///
/// 通道 1：地面运动（Locomotion）
///   进入 LocomotionState → 开始每帧轮询 Player 的移动意图
///   → 根据 HasMovementIntent + WantsRun 在 Idle/Walk/Run 三个 Clip 间 Crossfade
///   AnimLibrary 条目键名：Locomotion_Idle / Locomotion_Walk / Locomotion_Run
///
/// 通道 2：跳跃三阶段（Airborne）
///   PlayerJumpEvent → 播 Airborne_JumpStart
///   PlayerJumpAirPhaseEvent（到达最高点）→ 播 Airborne_Air（循环）
///   PlayerLandedEvent → 播 Airborne_Land + 短暂锁定防止 Locomotion 立即覆盖
///   AnimLibrary 条目键名：Airborne_JumpStart / Airborne_Air / Airborne_Land
///
/// 通道 3：动作状态（Action）
///   PlayerActionPresentationRequestEvent → 优先读 ActionDataSO.MainClip
///   → 若无 SO 则回退 AnimLibrary
///
/// ═══ 数据来源 ═══
///
/// 基础状态 Clip → PlayerAnimManagerSO（AnimLibrary 资产）
/// 动作 Clip → ActionDataSO.MainClip（数据驱动，Crossfade/Speed 全在 SO 上配）
/// </summary>
[RequireComponent(typeof(PlayerStateManager))]
public class PlayerAnimController : EntityAnimController
{
    [Header("Animation Library")]
    [SerializeField] private PlayerAnimManagerSO animLibrary;

    [Header("Landing Hold")]
    [Tooltip("落地动画播放占比（0~1）。落地 Clip 播放到此比例后才允许 Locomotion 覆盖。")]
    [SerializeField, Range(0f, 1f)] private float landingHoldRatio = 0.7f;

    [Header("Airborne blend")]
    [Tooltip("起跳 Clip → 滞空循环 Clip 的混合时长（秒），略大更顺滑）。")]
    [SerializeField, Range(0f, 0.5f)] private float jumpToAirCrossfade = 0.2f;

    [Header("Turn-In-Place blend")]
    [Tooltip("Turn 状态权重场的平滑速度（与 MoveTowards(..., dt * speed) 等价）。8 ≈ 0.125s 完成推拉。")]
    [SerializeField, Range(2f, 20f)] private float turnWeightBlendSpeed = 8f;

    [Header("Turn-In-Place debugger (Console)")]
    [Tooltip("开启后：输出转身切片播放细节。若仅勾选 Player State Manager 上的 Enable Trigger Debugger，也会自动输出 [TurnDbg:Anim] 边沿日志。")]
    [SerializeField] private bool debugTurnPresentation;

    [Tooltip("额外：在同一转身子状态停留时重复打印 Play（通常无需开启；边沿日志已足够确认是否播片）。")]
    [SerializeField] private bool debugTurnPresentationRepeatPlay;

    [Tooltip("Repeat 模式下的最小日志间隔（秒）。")]
    [SerializeField, Range(0.05f, 1f)] private float turnPresentationLogInterval = 0.12f;

    private Entity _entity;
    private Player _player;

    // ─── Locomotion 混合追踪 ───
    private bool _inLocomotion;
    private LocomotionSub _locoSub = LocomotionSub.None;
    private PlayerAnimManagerSO.AnimClipEntry _currentLocoEntry;

    // ─── 落地锁定（防止 Locomotion 立即覆盖 Land Clip）───
    private bool _landingHold;
    private float _landingTimer;

    private float _nextTurnPresentationLogTime;

    private enum LocomotionSub : byte
    {
        None,
        Idle,
        Walk,
        Run,
        // ── v3.3.3 模块 14：原地转身（Turn-In-Place）。沿用现有 2 端口 Crossfade，
        //    无需改造为 6 端口 mixer：Turn 子状态等价于一个"Idle 的特化分支"。
        TurnLeft90,
        TurnRight90,
        TurnLeft180,
        TurnRight180,
    }

    // ─── 生命周期 ───

    protected override void Awake()
    {
        base.Awake();
        _entity = GetComponent<Entity>();
        _player = GetComponent<Player>();
    }

    private void OnEnable()
    {
        if (_entity == null) return;

        // 状态切换
        _entity.EventBus.Subscribe<EntityStateEnterEvent>(OnStateEnter);

        // Action 支柱专用
        _entity.EventBus.Subscribe<PlayerActionPresentationRequestEvent>(OnActionPresentationRequest);
        _entity.EventBus.Subscribe<PlayablePlaybackSpeedRequestEvent>(OnPlayablePlaybackSpeedRequest);

        // 跳跃三阶段
        _entity.EventBus.Subscribe<PlayerJumpEvent>(OnJumpStart);
        _entity.EventBus.Subscribe<PlayerJumpAirPhaseEvent>(OnJumpAirPhase);
        _entity.EventBus.Subscribe<PlayerLandedEvent>(OnLanded);
    }

    private void OnDisable()
    {
        if (_entity == null) return;

        _entity.EventBus.Unsubscribe<EntityStateEnterEvent>(OnStateEnter);
        _entity.EventBus.Unsubscribe<PlayerActionPresentationRequestEvent>(OnActionPresentationRequest);
        _entity.EventBus.Unsubscribe<PlayablePlaybackSpeedRequestEvent>(OnPlayablePlaybackSpeedRequest);
        _entity.EventBus.Unsubscribe<PlayerJumpEvent>(OnJumpStart);
        _entity.EventBus.Unsubscribe<PlayerJumpAirPhaseEvent>(OnJumpAirPhase);
        _entity.EventBus.Unsubscribe<PlayerLandedEvent>(OnLanded);
    }

    protected override void Update()
    {
        base.Update(); // Crossfade 权重插值

        // 有移动输入时立即打断落地缓冲，避免「半蹲滑步」锁死 Locomotion。
        if (_landingHold && _player != null && _player.HasMovementIntent)
        {
            _landingHold = false;
            _landingTimer = 0f;
            if (_inLocomotion)
            {
                _locoSub = LocomotionSub.None;
            }
        }

        // 落地锁定倒计时
        if (_landingHold)
        {
            _landingTimer -= Time.deltaTime;
            if (_landingTimer <= 0f)
            {
                _landingHold = false;
                if (_inLocomotion)
                {
                    _locoSub = LocomotionSub.None;
                }
            }
        }

        if (_inLocomotion && !_landingHold)
        {
            var target = ResolveLocomotionSub();
            if (target != _locoSub)
            {
                var fromSub = _locoSub;
                var wasTurn = IsTurnSub(fromSub);
                var nowTurn = IsTurnSub(target);
                _locoSub = target;
                // Turn 与普通 Locomotion 互切时使用统一时序，避免条目 transition 差异造成视觉抖动。
                var transitionOverride = (wasTurn || nowTurn) ? ResolveTurnCrossfadeDuration() : -1f;
                PlayLocomotionClipForSub(target, transitionOverride, fromSub);
            }

            ApplyLocomotionStrideMatching();
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  通道 1：状态进入 → Locomotion / Dead 等基础状态
    // ═══════════════════════════════════════════════════════════

    private void OnStateEnter(EntityStateEnterEvent evt)
    {
        if (animLibrary == null) return;

        // Action 支柱由 PlayerActionPresentationRequestEvent 驱动
        if (evt.StateName == nameof(PlayerActionState))
        {
            _inLocomotion = false;
            return;
        }

        // Locomotion：启动混合追踪，由 Update 每帧驱动
        if (evt.StateName == nameof(PlayerLocomotionState))
        {
            _inLocomotion = true;
            if (!_landingHold)
            {
                _locoSub = LocomotionSub.None;
                var target = ResolveLocomotionSub();
                _locoSub = target;
                PlayLocomotionClipForSub(target);
            }
            return;
        }

        // Airborne：动画由跳跃阶段事件驱动，不在此处播放
        if (evt.StateName == nameof(PlayerAirborneState))
        {
            _inLocomotion = false;
            return;
        }

        // 其他状态（Dead 等）：直接查 AnimLibrary
        _inLocomotion = false;
        var entry = animLibrary.GetEntry(evt.StateName);
        if (entry != null && entry.Clip != null)
        {
            Play(entry.Clip, entry.TransitionDuration, entry.Speed, entry.IsLooping);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  通道 1 续：Locomotion 子状态混合（Idle / Walk / Run）
    // ═══════════════════════════════════════════════════════════

    private LocomotionSub ResolveLocomotionSub()
    {
        if (_player == null) return LocomotionSub.Idle;

        // 与 TurnSettings.EnableTurnInPlacePresentation 对齐：关开关时不播转身切片（防御性；逻辑层也会清除 Turn）。
        if (_player.States != null && !_player.States.LocomotionTurnSettings.EnableTurnInPlacePresentation)
        {
            if (!_player.HasMovementIntent) return LocomotionSub.Idle;
            return _player.WantsRun ? LocomotionSub.Run : LocomotionSub.Walk;
        }

        // 转身优先：TurnInfo 由 PlayerLocomotionState 每帧写入。
        // IsTurning 时即便玩家在按方向键，本层也优先播 Turn 切片直到锁定解除。
        var turn = _player.CurrentTurnInfo;
        if (turn.IsTurning)
        {
            switch (turn.Type)
            {
                case TurnType.Turn180:
                    return turn.Direction < 0 ? LocomotionSub.TurnLeft180 : LocomotionSub.TurnRight180;
                case TurnType.Turn90:
                    return turn.Direction < 0 ? LocomotionSub.TurnLeft90 : LocomotionSub.TurnRight90;
            }
        }

        if (!_player.HasMovementIntent) return LocomotionSub.Idle;
        return _player.WantsRun ? LocomotionSub.Run : LocomotionSub.Walk;
    }

    /// <param name="fromSub">切分前的 Locomotion 子状态；用于边沿日志（转身进出必须保留 from）。</param>
    private void PlayLocomotionClipForSub(LocomotionSub target, float transitionOverride = -1f,
        LocomotionSub fromSub = LocomotionSub.None)
    {
        if (animLibrary == null) return;

        var key = target switch
        {
            LocomotionSub.Walk => "Locomotion_Walk",
            LocomotionSub.Run => "Locomotion_Run",
            LocomotionSub.TurnLeft90 => "Locomotion_TurnLeft90",
            LocomotionSub.TurnRight90 => "Locomotion_TurnRight90",
            LocomotionSub.TurnLeft180 => "Locomotion_TurnLeft180",
            LocomotionSub.TurnRight180 => "Locomotion_TurnRight180",
            _ => "Locomotion_Idle",
        };

        _currentLocoEntry = animLibrary.GetEntry(key);
        if (_currentLocoEntry != null && _currentLocoEntry.Clip != null)
        {
            var transition = transitionOverride >= 0f ? transitionOverride : _currentLocoEntry.TransitionDuration;
            Play(_currentLocoEntry.Clip, transition, _currentLocoEntry.Speed,
                _currentLocoEntry.IsLooping);
            LogTurnPresentationEdge(fromSub, target, key, _currentLocoEntry.Clip, transition);
            MaybeLogTurnPresentationRepeat(target, key, _currentLocoEntry.Clip, transition);
        }
        else if (ShouldLogTurnPresentation && IsTurnSub(target))
        {
            Debug.LogWarning(
                $"[TurnDbg:Anim] missing AnimLibrary entry or Clip | key={key} | player={_player?.name}",
                this);
        }
    }

    /// <summary>与 TurnSettings.EnableTriggerDebugger 联动，避免“只有逻辑日志、看不到是否播片”。</summary>
    private bool ShouldLogTurnPresentation =>
        debugTurnPresentation
        || (_player != null && _player.States != null && _player.States.LocomotionTurnSettings.EnableTriggerDebugger);

    private void LogTurnPresentationEdge(LocomotionSub from, LocomotionSub to, string libraryKey, AnimationClip clip,
        float crossfade)
    {
        if (!ShouldLogTurnPresentation || clip == null)
        {
            return;
        }

        if (!IsTurnSub(from) && !IsTurnSub(to))
        {
            return;
        }

        var ti = _player != null ? _player.CurrentTurnInfo : default;
        Debug.Log(
            $"[TurnDbg:Anim] edge | {from} -> {to} | libKey={libraryKey} clip={clip.name} len={clip.length:F2}s " +
            $"crossfade={crossfade:F3}s speedMul={(_currentLocoEntry != null ? _currentLocoEntry.Speed : 1f):F2} " +
            $"loop={clip.isLooping} | primary={CurrentClipName} | TurnInfo t={ti.IsTurning} type={ti.Type} " +
            $"∠={ti.Angle:F1}°",
            this);
    }

    private void MaybeLogTurnPresentationRepeat(LocomotionSub sub, string libraryKey, AnimationClip clip, float transition)
    {
        if (!debugTurnPresentationRepeatPlay || !IsTurnSub(sub) || clip == null)
        {
            return;
        }

        var now = Time.unscaledTime;
        if (now < _nextTurnPresentationLogTime)
        {
            return;
        }

        _nextTurnPresentationLogTime = now + Mathf.Max(0.05f, turnPresentationLogInterval);

        var ti = _player != null ? _player.CurrentTurnInfo : default;
        Debug.Log(
            $"[TurnDbg:Anim] play_repeat | libKey={libraryKey} clip={clip.name} crossfade={transition:F3}s | " +
            $"TurnInfo t={ti.IsTurning} type={ti.Type} ∠={ti.Angle:F1}°",
            this);
    }

    private float ResolveTurnCrossfadeDuration()
    {
        return 1f / Mathf.Max(1f, turnWeightBlendSpeed);
    }

    /// <summary>转身切片是"原地旋转动画"，与角色平面速度无关，必须跳过步幅匹配，否则会因 currentSpeed≈0 把 playbackSpeed 推到 Clamp 下限。</summary>
    private static bool IsTurnSub(LocomotionSub sub) =>
        sub == LocomotionSub.TurnLeft90 || sub == LocomotionSub.TurnRight90
        || sub == LocomotionSub.TurnLeft180 || sub == LocomotionSub.TurnRight180;

    private void ApplyLocomotionStrideMatching()
    {
        if (_player == null || _currentLocoEntry == null) return;
        if (IsTurnSub(_locoSub)) return;

        var refSpeed = _currentLocoEntry.ReferenceLocomotionSpeed;
        if (refSpeed < 0.001f) return;

        var current = _player.PlanarVelocity.magnitude;
        var playback = current / Mathf.Max(0.01f, refSpeed) * _currentLocoEntry.Speed;
        playback = Mathf.Clamp(playback, 0.05f, 8f);
        SetPrimaryClipPlayableSpeed(playback);
    }

    // ═══════════════════════════════════════════════════════════
    //  通道 2：跳跃三阶段
    // ═══════════════════════════════════════════════════════════

    private void OnJumpStart(PlayerJumpEvent evt)
    {
        _inLocomotion = false;
        PlayLibraryEntry("Airborne_JumpStart");
    }

    private void OnJumpAirPhase(PlayerJumpAirPhaseEvent evt)
    {
        PlayLibraryEntry("Airborne_Air", jumpToAirCrossfade);
    }

    private void OnLanded(PlayerLandedEvent evt)
    {
        var entry = animLibrary?.GetEntry("Airborne_Land");
        if (entry != null && entry.Clip != null)
        {
            Play(entry.Clip, entry.TransitionDuration, entry.Speed, entry.IsLooping);
            _landingHold = true;
            _landingTimer = entry.Clip.length * landingHoldRatio;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  通道 3：Action 支柱 → ActionDataSO.MainClip 优先
    // ═══════════════════════════════════════════════════════════

    private void OnActionPresentationRequest(PlayerActionPresentationRequestEvent evt)
    {
        _inLocomotion = false;

        if (evt.Action != null && evt.Action.MainClip != null)
        {
            Play(evt.Action.MainClip, evt.Action.CrossfadeTime, evt.Action.AnimSpeed, evt.Action.MainClip.isLooping);
            return;
        }

        // 无 SO 回退：按 Kind 查 AnimLibrary
        var libraryKey = evt.Kind switch
        {
            GameplayIntentKind.Dodge => "Action_Dodge",
            GameplayIntentKind.SwordDash => "Action_SwordDash",
            _ => "Action_Attack",
        };

        PlayLibraryEntry(libraryKey);
    }

    private void OnPlayablePlaybackSpeedRequest(PlayablePlaybackSpeedRequestEvent evt)
    {
        if (_entity == null || evt.EntityInstanceId != _entity.GetInstanceID())
        {
            return;
        }

        SetPrimaryClipPlayableSpeed(evt.TargetSpeed);
    }

    // ─── 工具方法 ───

    /// <param name="transitionOverride">≥0 时覆盖条目上的 TransitionDuration（用于起跳→滞空等）。</param>
    private void PlayLibraryEntry(string key, float transitionOverride = -1f)
    {
        if (animLibrary == null) return;
        var entry = animLibrary.GetEntry(key);
        if (entry != null && entry.Clip != null)
        {
            var td = transitionOverride >= 0f ? transitionOverride : entry.TransitionDuration;
            Play(entry.Clip, td, entry.Speed, entry.IsLooping);
        }
    }
}

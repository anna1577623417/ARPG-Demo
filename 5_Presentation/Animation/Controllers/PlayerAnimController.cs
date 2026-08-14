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
    private ActionDataSO _telemetryAction;
    private uint _telemetryLeaseVersion;

    /// <summary>184.1 W5 — Turn 表现 CrossFade 打断时长（178.1 协作）。</summary>
    public const float TurnInterruptCrossfadeSec = 0.08f;

    /// <summary>184.1 — Turn 表现播放中（VisualFacingDriver 暂停缓追）。</summary>
    public bool IsPlayingTurnPresentation =>
        _inLocomotion
        && _player != null
        && _turnCompensationActive
        && IsTurnSub(_locoSub);

    /// <summary>184.1 W5 — 任何主动 Intent 打断 Turn 切片（短 CrossFade）。</summary>
    public void InterruptTurnIfAny(string reason = null)
    {
        var wasTurnVisual = IsTurnSub(_locoSub);
        var wasTurnInfo = _turnCompensationActive;
        if (!wasTurnVisual && !wasTurnInfo)
        {
            return;
        }

        TurnProbe.LogInterrupt(_player, reason);
        var fromSub = _locoSub;
        if (IsTurnSub(fromSub) && _player != null)
        {
            TurnProbe.LogSubExitAnim(
                _player,
                TurnDirectionFromSub(fromSub),
                fromSub.ToString(),
                "Interrupt",
                TurnInterruptCrossfadeSec,
                reason ?? "interrupt");
            LocomotionTurnPresentation235Probe.ObserveEnd(_player, reason ?? "interrupt");
        }

        _lastActiveTurnType = TurnType.None;
        _turnCompensationActive = false;
        _turnCompensationTimeLeft = 0f;

        if (!_inLocomotion || _player == null)
        {
            _locoSub = LocomotionSub.None;
            return;
        }

        _locoSub = LocomotionSub.None;
        var target = ResolveLocomotionSub();
        _locoSub = target;
        PlayLocomotionClipForSub(target, TurnInterruptCrossfadeSec, fromSub);
    }

    // ─── Locomotion 混合追踪 ───
    private bool _inLocomotion;
    private LocomotionSub _locoSub = LocomotionSub.None;
    private PlayerAnimManagerSO.AnimClipEntry _currentLocoEntry;

    // ─── 落地锁定（防止 Locomotion 立即覆盖 Land Clip）───
    private bool _landingHold;
    private float _landingTimer;

    private float _nextTurnPresentationLogTime;

    /// <summary>162.1：上一帧处于 Turn 子态时的类型，用于解锁边沿 PostTurnBlend。</summary>
    private TurnType _lastActiveTurnType = TurnType.None;
    private uint _consumedTurnCompensationGeneration;
    private uint _playedTurnCompensationGeneration;
    private bool _turnCompensationActive;
    private TurnInfo _activeTurnCompensationInfo;
    private float _turnCompensationTimeLeft;
    private float _turnCompensationLeaseSeconds;

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
        /// <summary>159.2+：Profile StrafeLocomotion ContinuousClip。</summary>
        StrafeProfile,
    }

    // ─── 生命周期 ───

    protected override int GetAnimProbeInstanceId() =>
        _entity != null ? _entity.GetInstanceID() : base.GetAnimProbeInstanceId();

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
        _entity.EventBus.Subscribe<PlayerContinuousLocomotionRequestEvent>(OnContinuousLocomotionRequest);
        _entity.EventBus.Subscribe<PlayablePlaybackSpeedRequestEvent>(OnPlayablePlaybackSpeedRequest);
        _entity.EventBus.Subscribe<TurnPresentationInterruptedEvent>(OnTurnPresentationInterrupted);

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
        _entity.EventBus.Unsubscribe<PlayerContinuousLocomotionRequestEvent>(OnContinuousLocomotionRequest);
        _entity.EventBus.Unsubscribe<PlayablePlaybackSpeedRequestEvent>(OnPlayablePlaybackSpeedRequest);
        _entity.EventBus.Unsubscribe<TurnPresentationInterruptedEvent>(OnTurnPresentationInterrupted);
        _entity.EventBus.Unsubscribe<PlayerJumpEvent>(OnJumpStart);
        _entity.EventBus.Unsubscribe<PlayerJumpAirPhaseEvent>(OnJumpAirPhase);
        _entity.EventBus.Unsubscribe<PlayerLandedEvent>(OnLanded);
        PresentationTelemetryStore.Clear(_entity.GetInstanceID());
        ClearTurnCompensationPresentation();
    }

    private void OnTurnPresentationInterrupted(TurnPresentationInterruptedEvent evt)
    {
        InterruptTurnIfAny(evt.Reason.ToString());
    }

    protected override void Update()
    {
        base.Update(); // Crossfade 权重插值
        PublishPlaybackTelemetry();

        // 落地锁定倒计时（表现层；Gameplay 打断统一走 Move 意图 + ActionWindow，157.2）
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
            var cueChanged = TryConsumeTurnCompensationCue();
            if (cueChanged && IsTurnSub(_locoSub))
            {
                _locoSub = LocomotionSub.None;
            }

            if (!cueChanged && _turnCompensationActive)
            {
                _turnCompensationTimeLeft -= Time.deltaTime;
                if (_turnCompensationTimeLeft <= 0f)
                {
                    _turnCompensationActive = false;
                    _turnCompensationTimeLeft = 0f;
                    LocomotionTurnPresentation235Probe.ObserveHandoff(
                        _player,
                        _consumedTurnCompensationGeneration,
                        "lease_complete");
                    _player.ClearTurnCompensationCue("lease_complete");
                }
            }

            var target = ResolveLocomotionSub();
            if (target != _locoSub)
            {
                var fromSub = _locoSub;
                var wasTurn = IsTurnSub(fromSub);
                var nowTurn = IsTurnSub(target);
                if (wasTurn && !nowTurn)
                {
                    _lastActiveTurnType = TurnTypeFromSub(fromSub);
                }

                _locoSub = target;
                var transitionOverride = (wasTurn || nowTurn)
                    ? ResolveTurnTransitionDuration(wasTurn, nowTurn, fromSub, target)
                    : -1f;
                if (wasTurn && !nowTurn && _player != null)
                {
                    TurnProbe.LogSubExitAnim(
                        _player,
                        TurnDirectionFromSub(fromSub),
                        fromSub.ToString(),
                        target.ToString(),
                        transitionOverride >= 0f ? transitionOverride : 0.08f);
                    LocomotionTurnPresentation235Probe.ObserveEnd(_player, $"sub_change:{fromSub}->{target}");
                }

                PlayLocomotionClipForSub(target, transitionOverride, fromSub);
            }

            if (IsTurnSub(_locoSub) && _turnCompensationActive)
            {
                _lastActiveTurnType = _activeTurnCompensationInfo.Type;
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
            InterruptTurnIfAny("ActionStateEnter");
            _inLocomotion = false;
            ClearTurnCompensationPresentation();
            return;
        }

        // Locomotion：启动混合追踪，由 Update 每帧驱动
        if (evt.StateName == nameof(PlayerLocomotionState))
        {
            // ActionState 在未接地时已直接回 Airborne；此处仍保留防御门，避免未来入口重引入空中 Idle。
            if (_player != null && !_player.IsGrounded)
            {
                _inLocomotion = false;
                ClearTurnCompensationPresentation();
                return;
            }

            _inLocomotion = true;

            if (!_landingHold)
            {
                TryConsumeTurnCompensationCue();
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
            ClearTurnCompensationPresentation();
            return;
        }

        // 其他状态（Dead 等）：直接查 AnimLibrary
        _inLocomotion = false;
        ClearTurnCompensationPresentation();
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

        // 235：转身只消费一次性补偿 Cue；Gameplay Facing/KCC 已在同 Tick 完成即时改向。
        // 有限 Lease 内表现优先播 Turn 切片，但它不建立 Gameplay 状态锁，也不回写世界旋转。
        var turn = _turnCompensationActive ? _activeTurnCompensationInfo : default;
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

        // 159.1 L3：LockOn + StrafeLocomotion ContinuousClip（Profile 优先）
        if (_player.IsLockedOn && _player.HasMovementIntent)
        {
            var snap = _player.LocomotionPresentation;
            if (snap.ResolvedState == LocomotionStateId.StrafeLocomotion && snap.ContinuousClip != null)
            {
                return LocomotionSub.StrafeProfile;
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
        if (IsTurnSub(target)
            && _turnCompensationActive
            && _playedTurnCompensationGeneration == _consumedTurnCompensationGeneration)
        {
            return;
        }

        // 159.2：Profile 优先（Turn ContinuousClip）；未注册或未配 Clip 时回落 AnimLibrary 字符串键。
        if (IsTurnSub(target)
            && _player != null
            && LocomotionAnimProfileBridge.TryGetTurnContinuousClip(
                _player.LocomotionProfile, _activeTurnCompensationInfo, _player.WantsRun, out var turnBinding)
            && turnBinding.TryGetContinuousPresentation(
                out var clip,
                out var bindingTransition,
                out var bindingSpeed,
                out _,
                out var continuousAction))
        {
            var transition = transitionOverride >= 0f ? transitionOverride : bindingTransition;
            var speed = bindingSpeed > 0.001f ? bindingSpeed : 1f;
            _currentLocoEntry = null;
            var isLooping = continuousAction != null
                ? continuousAction.IsContinuousLocomotion
                : clip.isLooping;
            speed = BeginTurnCompensationLease(clip, speed);
            Play(clip, transition, speed, isLooping,
                restartIfSameClip: true, requestSource: "Locomotion.ProfileTurn");
            _playedTurnCompensationGeneration = _consumedTurnCompensationGeneration;
            LocomotionTurnPresentation235Probe.ObserveSelection(
                _player,
                turnBinding.TurnDirection,
                "Profile",
                continuousAction,
                clip,
                transition,
                speed,
                isLooping);
            LocomotionTurnPresentation235Probe.ObservePlay(
                _player,
                turnBinding.TurnDirection,
                clip,
                "Locomotion.ProfileTurn");
            LogTurnSubPlayIfNeeded(target, turnBinding.TurnDirection, clip.name, "Profile", transition, clip.length, speed);
            LogTurnPresentationEdge(fromSub, target, $"Profile:TurnInPlaceDirected:{turnBinding.TurnDirection}", clip, transition);
            MaybeLogTurnPresentationRepeat(target, turnBinding.TurnDirection.ToString(), clip, transition);
            return;
        }

        if (target == LocomotionSub.StrafeProfile && _player != null)
        {
            var snap = _player.LocomotionPresentation;
            if (snap.ContinuousClip != null)
            {
                var transition = transitionOverride >= 0f ? transitionOverride : snap.TransitionDuration;
                var speed = snap.ClipSpeed > 0.001f ? snap.ClipSpeed : 1f;
                var isLooping = snap.ContinuousAction != null
                    ? snap.ContinuousAction.IsContinuousLocomotion
                    : snap.ContinuousClip.isLooping;
                _currentLocoEntry = new PlayerAnimManagerSO.AnimClipEntry
                {
                    Clip = snap.ContinuousClip,
                    TransitionDuration = transition,
                    Speed = speed,
                    ReferenceLocomotionSpeed = snap.ReferenceLocomotionSpeed,
                    IsLooping = isLooping,
                };
                Play(snap.ContinuousClip, transition, speed, isLooping,
                    restartIfSameClip: false, requestSource: "Locomotion.StrafeProfile");
                return;
            }
        }

        // 159.3：Idle / Walk / Run Profile 优先；未配 Clip 回落 AnimLibrary。
        if (_player != null
            && TryPlayProfileLoopClip(target, transitionOverride, fromSub))
        {
            return;
        }

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
            var playbackSpeed = IsTurnSub(target)
                ? BeginTurnCompensationLease(_currentLocoEntry.Clip, _currentLocoEntry.Speed)
                : _currentLocoEntry.Speed;
            Play(_currentLocoEntry.Clip, transition, playbackSpeed,
                _currentLocoEntry.IsLooping, restartIfSameClip: IsTurnSub(target),
                requestSource: $"Locomotion.AnimLibrary.{target}");
            if (IsTurnSub(target))
            {
                _playedTurnCompensationGeneration = _consumedTurnCompensationGeneration;
            }
            LocomotionTurnPresentation235Probe.ObserveSelection(
                _player,
                TurnDirectionFromSub(target),
                "AnimLibrary",
                null,
                _currentLocoEntry.Clip,
                transition,
                playbackSpeed,
                _currentLocoEntry.IsLooping);
            LocomotionTurnPresentation235Probe.ObservePlay(
                _player,
                TurnDirectionFromSub(target),
                _currentLocoEntry.Clip,
                $"Locomotion.AnimLibrary.{target}");
            LogTurnSubPlayIfNeeded(target, TurnDirectionFromSub(target), _currentLocoEntry.Clip.name, "AnimLibrary", transition, _currentLocoEntry.Clip.length, _currentLocoEntry.Speed);
            LogTurnPresentationEdge(fromSub, target, key, _currentLocoEntry.Clip, transition);
            MaybeLogTurnPresentationRepeat(target, key, _currentLocoEntry.Clip, transition);
        }
        else if (ShouldLogTurnPresentation && IsTurnSub(target))
        {
            Debug.LogFormat(
                LogType.Warning,
                LogOption.NoStacktrace,
                this,
                "[TurnDbg:Anim] missing AnimLibrary entry or Clip | key={0} | player={1}",
                key,
                _player != null ? _player.name : "null");
        }

        if (IsTurnSub(target)
            && (_currentLocoEntry == null || _currentLocoEntry.Clip == null))
        {
            _turnCompensationActive = false;
            _turnCompensationTimeLeft = 0f;
            _player?.ClearTurnCompensationCue("missing_turn_clip");
        }
    }

    /// <summary>与 TurnSettings.EnableTriggerDebugger 联动，避免“只有逻辑日志、看不到是否播片”。</summary>
    private bool ShouldLogTurnPresentation =>
        debugTurnPresentation
        || (_player != null && _player.States != null && _player.States.LocomotionTurnSettings.EnableTriggerDebugger);

    bool TryConsumeTurnCompensationCue()
    {
        if (_player == null
            || !_player.TryGetTurnCompensationCueAfter(
                _consumedTurnCompensationGeneration,
                out var cue))
        {
            return false;
        }

        _consumedTurnCompensationGeneration = cue.Generation;
        if (!TurnCompensationResolver.IsCueFresh(in cue, Time.frameCount))
        {
            _turnCompensationActive = false;
            _activeTurnCompensationInfo = default;
            _turnCompensationTimeLeft = 0f;
            _turnCompensationLeaseSeconds = 0f;
            _player.ClearTurnCompensationCue("stale_cue");
            return true;
        }

        _turnCompensationActive = cue.IsTurning;
        _activeTurnCompensationInfo = cue.ToTurnInfo();
        _turnCompensationTimeLeft = 0f;
        _turnCompensationLeaseSeconds = cue.PresentationLeaseSeconds;
        if (!cue.IsTurning)
        {
            _player.ClearTurnCompensationCue("cancel_only_consumed");
        }
        return true;
    }

    float BeginTurnCompensationLease(AnimationClip clip, float speed)
    {
        if (!_turnCompensationActive || clip == null)
        {
            return Mathf.Max(0.01f, speed);
        }

        var tuning = _player != null && _player.LocomotionProfile != null
            ? _player.LocomotionProfile.Tuning
            : null;
        var completionRatio = tuning != null ? tuning.TurnCompensationCompletionRatio : 0.7f;
        var resolvedSpeed = TurnCompensationResolver.ResolvePlaybackSpeedForLease(
            clip.length,
            speed,
            completionRatio,
            _turnCompensationLeaseSeconds);
        _turnCompensationTimeLeft = _turnCompensationLeaseSeconds > 0.0001f
            ? _turnCompensationLeaseSeconds
            : TurnCompensationResolver.ResolveLeaseSeconds(
                clip.length,
                resolvedSpeed,
                completionRatio);
        if (_player != null)
        {
            var cue = _player.CurrentTurnCompensationCue;
            LocomotionTurnPresentation235Probe.ObserveLeaseBegin(
                _player,
                in cue,
                _turnCompensationTimeLeft);
        }
        return resolvedSpeed;
    }

    void ClearTurnCompensationPresentation()
    {
        _turnCompensationActive = false;
        _activeTurnCompensationInfo = default;
        _turnCompensationTimeLeft = 0f;
        _turnCompensationLeaseSeconds = 0f;
    }

    private void LogTurnSubPlayIfNeeded(
        LocomotionSub sub,
        TurnDirection4 dir4,
        string clipName,
        string source,
        float crossfade,
        float clipLength,
        float speed)
    {
        if (!IsTurnSub(sub) || _player == null || dir4 == TurnDirection4.None)
        {
            return;
        }

        TurnProbe.LogSubPlay(_player, dir4, clipName, source, crossfade, clipLength, speed);
    }

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

    /// <summary>162.1：进入 Turn 用 Binding TransitionDuration；离开 Turn 叠加 PostTurnBlend。</summary>
    private float ResolveTurnTransitionDuration(bool wasTurn, bool nowTurn, LocomotionSub fromSub, LocomotionSub toSub)
    {
        if (wasTurn && !nowTurn)
        {
            // 235.2 空间朝向交接：Turn Clip 在旧 VisualRoot 空间结束，Locomotion Loop 在新 LogicForward
            // 空间开始。两者做普通 CrossFade 会让 Turn 根姿态在新空间再次淡出，视觉上形成第二次转向。
            // 当前双端口 Mixer 没有惯性化/根朝向补偿能力，因此必须原子切换；未来由 227 TransitionPlan
            // 的 SpatialHandoff 模式用 pose inertialization 改善姿态连续性，但仍不得混合空间朝向。
            return 0f;
        }

        if (!wasTurn && nowTurn && TryGetTurnBindingForSub(toSub, out var enterBinding))
        {
            return enterBinding.ResolvePresentationTransition();
        }

        if (wasTurn && nowTurn && fromSub != toSub
            && TryGetTurnBindingForSub(toSub, out var switchBinding))
        {
            return switchBinding.ResolvePresentationTransition();
        }

        return 1f / Mathf.Max(1f, turnWeightBlendSpeed);
    }

    private bool TryGetTurnBindingForSub(LocomotionSub sub, out LocomotionStateBinding binding)
    {
        binding = default;
        if (_player == null) return false;

        var turnDir = TurnDirectionFromSub(sub);
        if (turnDir == TurnDirection4.None) return false;

        if (!LocomotionAnimProfileBridge.TryGetTurnContinuousClip(
                _player.LocomotionProfile,
                BuildSyntheticTurnInfo(sub),
                _player.WantsRun,
                out binding))
        {
            return false;
        }

        return true;
    }

    private static TurnInfo BuildSyntheticTurnInfo(LocomotionSub sub)
    {
        return sub switch
        {
            LocomotionSub.TurnLeft90 => new TurnInfo { IsTurning = true, Type = TurnType.Turn90, Direction = -1 },
            LocomotionSub.TurnRight90 => new TurnInfo { IsTurning = true, Type = TurnType.Turn90, Direction = 1 },
            LocomotionSub.TurnLeft180 => new TurnInfo { IsTurning = true, Type = TurnType.Turn180, Direction = -1 },
            LocomotionSub.TurnRight180 => new TurnInfo { IsTurning = true, Type = TurnType.Turn180, Direction = 1 },
            _ => default,
        };
    }

    private static TurnType TurnTypeFromSub(LocomotionSub sub) =>
        sub == LocomotionSub.TurnLeft180 || sub == LocomotionSub.TurnRight180
            ? TurnType.Turn180
            : TurnType.Turn90;

    private static TurnDirection4 TurnDirectionFromSub(LocomotionSub sub) =>
        sub switch
        {
            LocomotionSub.TurnLeft90 => TurnDirection4.Left90,
            LocomotionSub.TurnRight90 => TurnDirection4.Right90,
            LocomotionSub.TurnLeft180 => TurnDirection4.Left180,
            LocomotionSub.TurnRight180 => TurnDirection4.Right180,
            _ => TurnDirection4.None,
        };

    bool TryPlayProfileLoopClip(LocomotionSub target, float transitionOverride, LocomotionSub fromSub)
    {
        var stateId = target switch
        {
            LocomotionSub.Idle => LocomotionStateId.Idle,
            LocomotionSub.Walk => LocomotionStateId.Walk,
            LocomotionSub.Run => LocomotionStateId.Run,
            _ => LocomotionStateId.None,
        };

        if (stateId == LocomotionStateId.None)
        {
            return false;
        }

        if (!LocomotionAnimProfileBridge.TryGetLoopContinuousClip(
                _player.LocomotionProfile, stateId, out var binding))
        {
            return false;
        }

        if (!binding.TryGetContinuousPresentation(
                out var clip,
                out var bindingTransition,
                out var bindingSpeed,
                out var refSpeed,
                out var continuousAction))
        {
            return false;
        }

        var transition = transitionOverride >= 0f ? transitionOverride : bindingTransition;
        var speed = bindingSpeed > 0.001f ? bindingSpeed : 1f;
        var isLooping = continuousAction != null
            ? continuousAction.IsContinuousLocomotion
            : clip.isLooping;
        _currentLocoEntry = new PlayerAnimManagerSO.AnimClipEntry
        {
            Clip = clip,
            TransitionDuration = transition,
            Speed = speed,
            ReferenceLocomotionSpeed = refSpeed,
            IsLooping = isLooping,
        };
        LocomotionTransition227BugProbe.LogPresentationRequest(
            _player,
            $"Locomotion.Profile.{stateId}",
            continuousAction,
            clip,
            CurrentClipName);
        // 227.5.1.2 勘误：Action 勾选 Is Continuous 才接管循环合同；Legacy Clip 保留导入设置。
        Play(clip, transition, speed, isLooping,
            restartIfSameClip: false, requestSource: $"Locomotion.Profile.{stateId}");
        if (target == LocomotionSub.Idle)
        {
            return true;
        }

        LogTurnPresentationEdge(fromSub, target, $"Profile:{stateId}", clip, transition);
        return true;
    }

    /// <summary>转身切片是"原地旋转动画"，与角色平面速度无关，必须跳过步幅匹配，否则会因 currentSpeed≈0 把 playbackSpeed 推到 Clamp 下限。</summary>
    private static bool IsTurnSub(LocomotionSub sub) =>
        sub == LocomotionSub.TurnLeft90 || sub == LocomotionSub.TurnRight90
        || sub == LocomotionSub.TurnLeft180 || sub == LocomotionSub.TurnRight180;

    private void ApplyLocomotionStrideMatching()
    {
        if (_player == null || _currentLocoEntry == null) return;
        if (IsTurnSub(_locoSub) || _locoSub == LocomotionSub.StrafeProfile) return;

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
        if (_entity == null || evt.PlayerInstanceId != _entity.GetInstanceID()) return;
        _inLocomotion = false;
        var entry = animLibrary?.GetEntry("Airborne_JumpStart");
        LocomotionTransition227BugProbe.LogPresentationRequest(
            _player, "Jump.Event.Start", null, entry?.Clip, CurrentClipName);
        PlayLibraryEntry("Airborne_JumpStart", restartIfSameClip: false,
            requestSource: "Jump.Event.Start");
    }

    private void OnJumpAirPhase(PlayerJumpAirPhaseEvent evt)
    {
        if (_entity == null || evt.PlayerInstanceId != _entity.GetInstanceID()) return;
        var entry = animLibrary?.GetEntry("Airborne_Air");
        LocomotionTransition227BugProbe.LogPresentationRequest(
            _player, "Jump.Event.Air", null, entry?.Clip, CurrentClipName);
        PlayLibraryEntry("Airborne_Air", jumpToAirCrossfade, restartIfSameClip: false,
            requestSource: "Jump.Event.Air");
    }

    private void OnLanded(PlayerLandedEvent evt)
    {
        if (_player != null && _player.States?.Current is PlayerActionState)
        {
            return;
        }

        if (_player != null && ProfileProvidesJumpLandAction(_player))
        {
            return;
        }

        Locomotion165Diagnostics.LogJumpLandAnimFallback(_player);

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

    private void OnContinuousLocomotionRequest(PlayerContinuousLocomotionRequestEvent evt)
    {
        if (_player == null || evt.PlayerInstanceId != _entity.GetInstanceID())
        {
            return;
        }

        if (evt.Action == null
            || evt.Action.MainClip == null
            || !evt.Action.IsContinuousLocomotion
            || evt.ExecutionPolicy != LocomotionExecutionPolicy.ContinuousPresentation)
        {
            return;
        }

        if (!_player.IsGrounded)
        {
            return;
        }

        // 235.1：Walk/Run 连续请求是 Turn 的底层期望，不能覆盖 active/pending Turn 后令同 generation 重播。
        if (_turnCompensationActive || _player.CurrentTurnCompensationCue.IsTurning)
        {
            return;
        }

        _inLocomotion = true;
        _locoSub = MapContinuousState(evt.ResolvedState);
        if (_locoSub == LocomotionSub.None)
        {
            _locoSub = ResolveLocomotionSub();
        }
        var clip = evt.Action.MainClip;
        LocomotionTransition227BugProbe.LogPresentationRequest(
            _player,
            $"Locomotion.ContinuousEvent.{evt.ResolvedState}",
            evt.Action,
            clip,
            CurrentClipName);
        // 227.5.1.2 勘误：该事件只接受已显式勾选 Is Continuous 的 Action 接管请求。
        Play(clip, evt.Action.CrossfadeTime, Mathf.Max(0.01f, evt.Action.AnimSpeed),
            isLooping: evt.Action.IsContinuousLocomotion,
            restartIfSameClip: false, requestSource: $"Locomotion.ContinuousEvent.{evt.ResolvedState}");
    }

    private void OnActionPresentationRequest(PlayerActionPresentationRequestEvent evt)
    {
        _inLocomotion = false;
        _telemetryAction = evt.Action;
        _telemetryLeaseVersion = evt.ActionLeaseVersion;

        if (evt.Action != null)
        {
            var clip = evt.PresentationClip != null ? evt.PresentationClip : evt.Action.MainClip;
            if (clip != null)
            {
                LocomotionTransition227BugProbe.LogPresentationRequest(
                    _player,
                    $"ActionPresentation.{evt.Kind}",
                    evt.Action,
                    clip,
                    CurrentClipName);
                var clipStartNorm = evt.Action.MapActionTimeToClipNormalized(evt.NormalizedStart);
                var animSpeed = evt.PlaybackAnimSpeedOverride >= 0f
                    ? evt.PlaybackAnimSpeedOverride
                    : evt.Action.ResolveEffectiveAnimSpeed();
                Play(
                    clip,
                    evt.Action.CrossfadeTime,
                    animSpeed,
                    clip.isLooping,
                    clipStartNorm,
                    restartIfSameClip: evt.Kind != GameplayIntentKind.Jump,
                    requestSource: $"ActionPresentation.{evt.Kind}");
                return;
            }
        }

        // 无 SO 回退：按 Kind 查 AnimLibrary（Ver4.3.6+ Skill_Entry_NN 默认走 Action_Attack 兜底，
        // 具体 Clip 仍以 ActionData.MainClip 优先）
        PlayLibraryEntry("Action_Attack");
    }

    private void PublishPlaybackTelemetry()
    {
        if (_player == null || _telemetryAction == null || _player.States == null)
        {
            return;
        }

        var step = _player.States.CaptureRuntimeStep(RuntimeTracePhase.PresentationObserve);
        var version = PresentationTelemetryStore.NextVersion(_player.GetInstanceID());
        if (TryBuildPlaybackSample(
                _telemetryAction,
                _telemetryLeaseVersion,
                in step,
                version,
                out var sample))
        {
            PresentationTelemetryStore.Publish(in sample);
        }
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

    static bool ProfileProvidesJumpLandAction(Player player)
    {
        var profile = player.LocomotionProfile;
        if (profile == null)
        {
            return false;
        }

        if (TryGetProfileLandAction(profile, LocomotionStateId.JumpLand, out _))
        {
            return true;
        }

        var tuning = profile.Tuning;
        if (tuning == null || !tuning.EnableTieredLanding)
        {
            return false;
        }

        return TryGetProfileLandAction(profile, LocomotionStateId.JumpLandHeavy, out _)
               || TryGetProfileLandAction(profile, LocomotionStateId.JumpLandRoll, out _);
    }

    static bool TryGetProfileLandAction(LocomotionProfile profile, LocomotionStateId id, out ActionDataSO action)
    {
        action = null;
        if (profile == null || !profile.HasState(id))
        {
            return false;
        }

        action = profile.GetBinding(id).ResolveLocomotionAction();
        return action != null;
    }

    /// <param name="transitionOverride">≥0 时覆盖条目上的 TransitionDuration（用于起跳→滞空等）。</param>
    private static LocomotionSub MapContinuousState(LocomotionStateId state) => state switch
    {
        LocomotionStateId.Idle => LocomotionSub.Idle,
        LocomotionStateId.Walk => LocomotionSub.Walk,
        LocomotionStateId.Run => LocomotionSub.Run,
        LocomotionStateId.StrafeLocomotion => LocomotionSub.StrafeProfile,
        _ => LocomotionSub.None,
    };

    private void PlayLibraryEntry(
        string key,
        float transitionOverride = -1f,
        bool restartIfSameClip = true,
        string requestSource = "AnimLibrary")
    {
        if (animLibrary == null) return;
        var entry = animLibrary.GetEntry(key);
        if (entry != null && entry.Clip != null)
        {
            var td = transitionOverride >= 0f ? transitionOverride : entry.TransitionDuration;
            Play(entry.Clip, td, entry.Speed, entry.IsLooping,
                restartIfSameClip: restartIfSameClip, requestSource: requestSource);
        }
    }
}

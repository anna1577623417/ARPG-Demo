using UnityEngine;

/// <summary>
/// 玩家控制器：连续移动采样 + 离散意图入队。
/// 跑步（Run）：165.1 L7 — Sprint 键 Hold/Toggle（见 LocomotionTuningSO.RunInputMode）或手柄高幅度。
/// </summary>
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Player))]
[AddComponentMenu("GameMain/Player/Player Controller")]
public class PlayerController : EntityController
{
    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private InputReader inputReader;
    private IIntentHost intentHost;

    [Header("Continuous locomotion — Run")]
    [Tooltip("仅手柄：左摇杆幅度超过该阈值视为 Run（键盘 Run 走 Sprint 键 Hold/Toggle）。")]
    [SerializeField, Range(0f, 1f)] private float runMagnitudeThreshold = 0.85f;

    [Tooltip("判定无输入时，合成向量模长低于此值视为松手。")]
    [SerializeField, Range(0.01f, 0.3f)] private float moveReleaseThreshold = 0.12f;

    [Header("Debug")]
    [Tooltip("勾选后在场景视图玩家脚底绘制相机参考水平前向与角色水平前向；颜色见下方两项。")]
    [SerializeField] private bool debugForwardDirectionArrows;

    [Tooltip("camera-relative DEBUG：相机/移动参考在水平面上的「正前」箭头颜色。")]
    [SerializeField] private Color debugCameraRelativeArrowColor = new Color(0.04f, 0.12f, 0.42f);

    [Tooltip("camera-relative DEBUG：角色自身水平朝前箭头颜色。")]
    [SerializeField] private Color debugPlayerForwardArrowColor = new Color(0.35f, 0.92f, 0.35f);

    [Tooltip("箭头轴线长度（世界空间）。")]
    [SerializeField, Min(0.05f)] private float debugArrowLength = 1.25f;

    [Tooltip("箭头起点相对 transform.position 的抬高（模拟贴在脚底上方）。")]
    [SerializeField] private float debugArrowHeightOffset = 0.08f;

    private readonly PrimaryAttackPressTracker _primaryAttackPress = new PrimaryAttackPressTracker();

    private readonly SecondaryInteractPressTracker _secondaryInteractPress = new SecondaryInteractPressTracker();

    private IGameModeMovementContext _movementContext;
    private bool _isInitialized;
    private bool _loggedMissingMovementContext;
    private float _nextLocoInputTraceTime;
    private float _nextLocoAnomalyLogTime;

    private readonly InputTenseResolver _wasdTense = new InputTenseResolver();

    private Vector2 _prevMoveInput;
    private bool _runToggled;
    private bool _prevWantsRun;

    ActionDataSO _moveInterruptWindowAction;
    bool _moveInterruptQueuedForWindow;

    // ─── 生命周期 ───

    private void Awake()
    {
        Init();
    }

    private void Init()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        if (player == null)
        {
            player = GetComponent<Player>();
        }

        if (inputReader == null && player != null)
        {
            inputReader = player.InputReader;
        }

        intentHost = player;


    }

    /// <summary>
    /// 构造期/生成点注入相机移动上下文：由 <see cref="SystemRoot"/>（场景登记）或 <see cref="PlayerFactory"/>（运行时生成）推送；
    /// 调用方持有接口引用而非查表解析；晚注入时移动采样会在下一帧起生效。
    /// </summary>
    public void InjectMovementContext(IGameModeMovementContext context)
    {
        _movementContext = context;
    }

    private void OnEnable()
    {
        Init();
        if (inputReader != null)
        {
            _primaryAttackPress.SyncInitialHeldState(inputReader.IsAttackHeld);
            _secondaryInteractPress.SyncInitialHeldState(inputReader.IsInteractHeld);
        }
    }

    private void OnDisable()
    {
        if (inputReader != null)
        {
            _primaryAttackPress.SyncInitialHeldState(inputReader.IsAttackHeld);
            _secondaryInteractPress.SyncInitialHeldState(inputReader.IsInteractHeld);
        }
    }

    private void Update()
    {
        if (player == null || inputReader == null)
        {
            return;
        }

        var rawInput = inputReader.MoveInput;
        var releaseSq = moveReleaseThreshold * moveReleaseThreshold;
        var hasMoveInput = rawInput.sqrMagnitude >= releaseSq;

        TickAbilityInputContext(rawInput);
        ConsumeDiscreteIntents();
        _primaryAttackPress.Tick(Time.time, inputReader.IsAttackHeld, player);
        _secondaryInteractPress.Tick(Time.time, inputReader.IsInteractHeld, player);

        TryEnqueueMoveInterruptIntent(rawInput, releaseSq);

        var tuning = player.LocomotionProfile != null ? player.LocomotionProfile.Tuning : null;
        _wasdTense.ApplyTuning(tuning);

        var worldDirection = ResolveWorldDirection(rawInput, out var moveCtxSource, out var cameraRelative);
        if (GameMainDebugSettings.CameraTurn233Log)
        {
            CameraTurn233Probe.ObserveInput(
                player,
                rawInput,
                worldDirection,
                moveCtxSource,
                cameraRelative,
                ResolveMovementReferenceYaw());
        }
        var tense = _wasdTense.Tick(hasMoveInput, Time.time);
        player.SetCurrentInputTense(tense);
        var wantsRun = ResolveRunIntent(rawInput, releaseSq);
        CharacterTurnDisplacement233Probe.ObserveInput(
            player,
            rawInput,
            worldDirection,
            wantsRun,
            tense,
            moveCtxSource);
        LocomotionMotion233Probe.ObserveInput(
            player,
            rawInput,
            worldDirection,
            wantsRun,
            tense,
            moveCtxSource);
        LocomotionTurnPresentation235Probe.ObserveInput(
            player,
            worldDirection,
            hasMoveInput,
            tense,
            wantsRun,
            moveCtxSource);
        player.SubmitTurnCompensationCommand(worldDirection, hasMoveInput);
        ApplyInputTenseFacing1841(tense, hasMoveInput, worldDirection, rawInput, releaseSq, wantsRun);
        LogLocomotionInputTrace(player, rawInput, worldDirection, moveCtxSource, cameraRelative);

        //if (debugRunLogs && wantsRun != _prevWantsRun)
        //{
        //    Debug.Log(
        //        $"[PlayerController][Locomotion] WantsRun {(wantsRun ? "TRUE → Run" : "FALSE → Walk/Idle")} | " +
        //        $"sticky={_stickyRunMode} | moveMag={rawInput.magnitude:F3} | " +
        //        $"threshold={runMagnitudeThreshold:F2}",
        //        this);
        //}

        _prevWantsRun = wantsRun;
        _prevMoveInput = rawInput;
    }

    void TickAbilityInputContext(Vector2 rawInput)
    {
        if (player == null)
        {
            return;
        }

        var tuning = player.LocomotionProfile != null ? player.LocomotionProfile.Tuning : null;
        SyncDirectionModifierBuffer(tuning);
        var window = tuning != null ? tuning.AbilityContextWindowSec : 0.1f;
        var grace = tuning != null ? tuning.DirectionGraceSec : 0.12f;
        var fwd = player.LogicForward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude > 0.0001f)
        {
            fwd.Normalize();
        }
        else
        {
            fwd = Vector3.forward;
        }

        var moveWasActive = player.InputContext.MoveActive;
        player.InputContext.TickMoveContext(
            rawInput,
            moveReleaseThreshold,
            Time.time,
            fwd,
            window,
            grace);
        var worldDir = player.ResolveCameraRelativeWorldDirection(rawInput);
        if (!moveWasActive && player.InputContext.MoveActive)
        {
            player.InputContext.TryGetMoveDownPlanarForward(out var basis);
            var token = player.NotifyDirectionIntentDown(
                rawInput,
                worldDir,
                basis,
                ResolveMovementReferenceYaw());
            SkillGroupTurn237Probe.ObserveDirDown(player, rawInput, basis, player.LogicForward);
            DirectionAuthority237Probe.ObserveDown(
                player,
                rawInput,
                worldDir,
                basis,
                player.DesiredFacing,
                player.LogicForward,
                token);
            DirectionAuthority237Probe.ObserveIntent(player, token);
            DirectionAuthority237Probe.ObserveGateOpen(player, token, player.FacingCommit.DelaySec);
            DirectionAuthority237Probe.ObserveCtxOpen(player, player.ResolveDirectionalTiming());
        }
        else if (player.InputContext.MoveActive)
        {
            player.TickDesiredFacing(
                rawInput,
                worldDir,
                moveReleaseThreshold);
        }

        player.RefreshLocomotionRuntimeContext(rawInput, worldDir);
        DirectionAuthority237Probe.ObserveMoveTick(
            player,
            rawInput,
            worldDir,
            moveWasActive,
            player.InputContext.MoveActive,
            ResolveMovementReferenceYaw());
    }

    void SyncDirectionModifierBuffer(LocomotionTuningSO tuning)
    {
        if (inputReader?.MoveModifierBuffer == null)
        {
            return;
        }

        var sec = tuning != null
            ? tuning.DirectionModifierBufferSec
            : InputModifierBuffer.DefaultBufferSeconds;
        inputReader.MoveModifierBuffer.SetBufferSeconds(sec);
    }

    /// <summary>
    /// 按 SkillEntrySlot 循环派发（Ver4.3.6+）。
    ///
    /// ═══ 数据流 ═══
    ///   物理键 → InputReader.WriteSlotEdge(SkillEntrySlot, pressed)
    ///         → ConsumeSkillEntryPressed → SkillEntryIntentFactory.ForEntry → IntentBuffer
    ///         → PlayerStateManager (TransitionResolver + SkillEntryService.TryResolveForIntent)
    ///         → RouteRuntime
    /// </summary>
    private void ConsumeDiscreteIntents()
    {
        if (inputReader.ConsumeJumpPressed())
        {
            intentHost.TryEnqueue(SkillEntryIntentFactory.ForJump(Time.time));
            if (GameMainDebugSettings.InterruptFlow)
            {
                Debug.Log($"[Input] JumpPressed → Jump intent enqueued | currentState={player.States?.Current?.StateId}", this);
            }
        }

        TryDispatchEntry(SkillEntrySlot.RM);
        TryDispatchEntry(SkillEntrySlot.Q);
        TryDispatchEntry(SkillEntrySlot.Shift);
        TryDispatchEntry(SkillEntrySlot.Space);
        TryDispatchEntry(SkillEntrySlot.R);
        TryDispatchEntry(SkillEntrySlot.Key0);
        TryDispatchEntry(SkillEntrySlot.Key1);
        TryDispatchEntry(SkillEntrySlot.Key2);
        TryDispatchEntry(SkillEntrySlot.Key3);
        TryDispatchEntry(SkillEntrySlot.Key4);
        TryDispatchEntry(SkillEntrySlot.Key5);
        TryDispatchEntry(SkillEntrySlot.Key6);
        TryDispatchEntry(SkillEntrySlot.Key7);
        TryDispatchEntry(SkillEntrySlot.Key8);
        TryDispatchEntry(SkillEntrySlot.Key9);
        // Primary 槽位脉冲被 PrimaryAttackPressTracker 独立消费（Press/Release + holdSeconds），
        // 由 RouteResolver 在仲裁阶段决定 Tap/Combo/Charge 走向。
    }

    /// <summary>
    /// 消费指定 SkillEntrySlot 的按下脉冲，转发到 InputSemanticResolver（Ver4.3.7+ Phase E）。
    ///
    /// ═══ 数据流 ═══
    ///   Reader.ConsumeSkillEntryPressed → InputSemanticResolver.OnDiscretePulse
    ///     → 由 Resolver 决策 Tap / Combo / Directional
    ///     → EnqueueSemantic（intent.Semantic + ComboIndex + DirectionAxis）
    ///     → IntentBuffer → PlayerStateManager → SkillEntryService.TryResolveForIntent（单轨）
    ///
    /// 仍保留 SkillEntryIntentFactory.ForEntryTapFallback 作为 Resolver 不可用时的兜底（Phase F 之前）。
    /// </summary>
    private void TryDispatchEntry(SkillEntrySlot slot)
    {
        if (!inputReader.ConsumeSkillEntryPressed(slot, out var holdSeconds))
        {
            return;
        }

        if (slot == SkillEntrySlot.Space)
        {
            HoldMotionDodgeProbe.NotifySpacePulse();
        }

        DirectionalInputDiagProbe.NotifyAbilityPulse(slot);

        Vector2 moveBuf = default;
        var moveBufValid = inputReader.MoveModifierBuffer != null
            && (moveBuf = inputReader.MoveModifierBuffer.GetBufferedMove(Time.time)).sqrMagnitude > 0.0001f;

        var branchNote = string.Empty;
        // 206.2 — 持续按住 WASD 跑步时 buffer 可能已过期；仍用当前 MoveInput 作方向 modifier。
        if (!moveBufValid && inputReader.MoveInput.sqrMagnitude > moveReleaseThreshold * moveReleaseThreshold)
        {
            moveBuf = inputReader.MoveInput;
            moveBufValid = true;
            branchNote = "branch=liveMoveInput_fallback";
        }

        var ctx = player.InputContext;
        var ctxHold = ctx != null && ctx.MoveActive
            ? Time.time - ctx.MoveActiveSince
            : -1f;
        var tuning = player.LocomotionProfile != null ? player.LocomotionProfile.Tuning : null;
        var chordWin = tuning != null ? tuning.ChordWindowSec : 0.12f;

        // 213.6 — Chord 窗内 MoveContext 已激活，同帧 live 刚按下。
        if (!moveBufValid
            && ctx != null
            && ctx.MoveActive
            && ctxHold >= 0f
            && ctxHold <= chordWin)
        {
            var live = inputReader.MoveInput;
            if (live.sqrMagnitude > moveReleaseThreshold * moveReleaseThreshold)
            {
                moveBuf = live;
                moveBufValid = true;
                branchNote = "branch=ctxChordLive_fallback";
            }
        }

        // 213.6 — Shift 软过期：硬 Buffer 刚过仍读 last WASD。
        if (!moveBufValid
            && slot == SkillEntrySlot.Shift
            && inputReader.MoveModifierBuffer != null)
        {
            var softSec = tuning != null ? tuning.ShiftModifierSoftGraceSec : 0.12f;
            if (softSec > 0f
                && inputReader.MoveModifierBuffer.TryGetSoftBufferedMove(Time.time, softSec, out var softMove))
            {
                moveBuf = softMove;
                moveBufValid = true;
                branchNote = "branch=bufferSoftGrace_fallback";
            }
        }

        var bufferMaxAge = tuning != null
            ? tuning.DirectionModifierBufferSec
            : InputModifierBuffer.DefaultBufferSeconds;
        DirectionalInputDiagProbe.LogBufferState(
            inputReader.MoveModifierBuffer != null
                ? inputReader.MoveModifierBuffer.GetBufferAgeSec(Time.time)
                : -1f,
            moveBufValid ? moveBuf : Vector2.zero,
            inputReader.MoveInput,
            bufferMaxAge);
        DirectionalInputDiagProbe.LogDispatch(
            slot,
            moveBuf,
            moveBufValid,
            inputReader.MoveInput,
            ctx != null && ctx.MoveActive,
            ctxHold,
            ctx != null && ctx.DirectionalCommitted,
            branchNote);
        DodgeChord8Probe.LogDispatchPulse(
            slot,
            moveBuf,
            moveBufValid,
            inputReader.MoveInput,
            ctx != null && ctx.MoveActive,
            ctx != null ? ctx.MoveActiveSince : -1f,
            ctx != null && ctx.DirectionalCommitted,
            ctx != null && ctx.LoadoutHasDirectionalModifier,
            branchNote);

        var resolver = player.InputSemantic;
        if (resolver != null)
        {
            resolver.OnDiscretePulse(slot, Time.time, holdSeconds, moveBuf, moveBufValid);
        }
        else
        {
            // Resolver 缺失（启动时序问题）→ 兜底 Tap 直连入队，避免输入丢失。
            var intent = SkillEntryIntentFactory.ForEntryTapFallback(
                slot, Time.time, holdSeconds, moveBuf, moveBufValid);
            intentHost.TryEnqueue(intent);
        }

        if (GameMainDebugSettings.InterruptFlow)
        {
            Debug.Log($"[IntentInput] EntryPulse slot={slot} hold={holdSeconds:F3}s moveBuf={moveBuf} valid={moveBufValid}", this);
        }
    }

    /// <summary>
    /// 157.2 — Action 期 WASD → Move 意图（后摇窗口放行 Locomotion 时：按下边沿，或窗口内已按住 WASD）。
    /// </summary>
    void TryEnqueueMoveInterruptIntent(Vector2 rawInput, float releaseSq)
    {
        if (player.States?.Current is not PlayerActionState)
        {
            ResetMoveInterruptWindowTrack();
            return;
        }

        if (!player.TryGetActiveActionInterruptProbe(out var action, out var nt))
        {
            ResetMoveInterruptWindowTrack();
            return;
        }

        if (!ActionInterruptResolver.IsCategoryAllowedAtWindow(action, nt, ActionCategory.Locomotion))
        {
            if (!ReferenceEquals(_moveInterruptWindowAction, action))
            {
                ResetMoveInterruptWindowTrack();
            }

            _moveInterruptWindowAction = action;
            return;
        }

        if (!ReferenceEquals(_moveInterruptWindowAction, action))
        {
            _moveInterruptWindowAction = action;
            _moveInterruptQueuedForWindow = false;
        }

        if (_moveInterruptQueuedForWindow)
        {
            return;
        }

        var hasMove = rawInput.sqrMagnitude >= releaseSq;
        var edge = DetectMovePressEdge(rawInput, releaseSq);
        if (!edge && !hasMove)
        {
            return;
        }

        var moveBuf = rawInput;
        var moveBufValid = moveBuf.sqrMagnitude > releaseSq;
        var forbidden = (ulong)StateTag.Dead;
        intentHost.TryEnqueue(SkillEntryIntentFactory.ForMove(
            Time.time, moveBuf, moveBufValid, forbidden));
        _moveInterruptQueuedForWindow = true;

        if (GameMainDebugSettings.InterruptFlow)
        {
            var trigger = edge ? "edge" : "hold-in-window";
            Debug.Log(
                $"[Intent] ENQUEUE Move ({trigger}) buffered=({moveBuf.x:F2},{moveBuf.y:F2}) nt={nt:F2} action={action.name}",
                this);
        }
    }

    void ResetMoveInterruptWindowTrack()
    {
        _moveInterruptWindowAction = null;
        _moveInterruptQueuedForWindow = false;
    }

    bool DetectMovePressEdge(Vector2 rawInput, float releaseSq)
    {
        var hadMove = _prevMoveInput.sqrMagnitude >= releaseSq;
        var hasMove = rawInput.sqrMagnitude >= releaseSq;
        return !hadMove && hasMove;
    }

    private bool ResolveRunIntent(Vector2 rawInput, float releaseSq)
    {
        if (rawInput.sqrMagnitude < releaseSq)
        {
            return false;
        }

        if (inputReader.MoveActuatedByGamepad && rawInput.magnitude >= runMagnitudeThreshold)
        {
            return true;
        }

        var tuning = player != null && player.LocomotionProfile != null
            ? player.LocomotionProfile.Tuning
            : null;
        var mode = tuning != null ? tuning.RunInputMode : RunInputMode.Toggle;

        if (mode == RunInputMode.Toggle)
        {
            if (inputReader.ConsumeRunToggled())
            {
                _runToggled = !_runToggled;
                Locomotion165Diagnostics.LogRunIntent(
                    player,
                    mode,
                    _runToggled,
                    _runToggled,
                    inputReader.IsRunHeld,
                    "SprintToggleEdge");
            }

            return _runToggled;
        }

        var holdRun = inputReader.IsRunHeld;
        if (holdRun != _prevWantsRun && player.HasMovementIntent)
        {
            Locomotion165Diagnostics.LogRunIntent(
                player,
                mode,
                holdRun,
                _runToggled,
                inputReader.IsRunHeld,
                "SprintHoldChange");
        }

        return holdRun;
    }

    /// <summary>234.5 — Combo 由 InputContext 快照解析；Pending/Hold 写移动，Tap 只表示既有移动会话的释放。</summary>
    void ApplyInputTenseFacing1841(
        InputTense tense,
        bool hasMoveInput,
        Vector3 worldDirection,
        Vector2 rawInput,
        float releaseSq,
        bool wantsRun)
    {
        if (tense == InputTense.Tap)
        {
            // Tap 是已发生过 KCC 移动的会话释放边沿；不得再补一次 Root Facing 或 Arm Turn。
            player.ClearMovementIntent();
            return;
        }

        if (!hasMoveInput)
        {
            player.ClearMovementIntent();
            return;
        }

        switch (tense)
        {
            case InputTense.Hold:
            case InputTense.ShortHold:
                // 234.5：连续移动只写意图；Player 在 Logic Tick 同步 Velocity direction / LogicFacing。
                player.SetMovementIntent(worldDirection, wantsRun);
                break;

            case InputTense.Pending:
                // Pending 只供组合技能读取 MoveDown 快照；首帧即进入 Locomotion，不冻结 Facing。
                player.SetMovementIntent(worldDirection, wantsRun);
                break;

            default:
                player.ClearMovementIntent();
                break;
        }
    }

    private Vector3 ResolveWorldDirection(Vector2 rawInput, out string ctxSource, out bool cameraRelative)
    {
        ctxSource = "none";
        cameraRelative = true;

        if (rawInput.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        var input = Vector2.ClampMagnitude(rawInput, 1f);
        var ctx = ResolveMovementContext(out ctxSource);

        if (ctx != null && !ctx.IsCameraRelativeMovement)
        {
            cameraRelative = false;
            return new Vector3(input.x, 0f, input.y);
        }

        Quaternion refRotation;

        if (ctx != null)
        {
            refRotation = ctx.GetMovementReferenceRotation();
        }
        else
        {
            LocomotionDebug.LogWarnOnce(
                player,
                ref _loggedMissingMovementContext,
                LocomotionDebug.CatInput,
                "缺少 IGameModeMovementContext（注入与 GameModeManager 均为 null）→ 回落世界轴 (x,0,y)；CameraFwd 失效。",
                this);

            var mainCam = Camera.main;
            if (mainCam == null)
            {
                ctxSource = "world-fallback";
                cameraRelative = false;
                return new Vector3(input.x, 0f, input.y);
            }

            refRotation = Quaternion.Euler(0f, mainCam.transform.eulerAngles.y, 0f);
            ctxSource = "camera.main";
        }

        var forward = refRotation * Vector3.forward;
        var right = refRotation * Vector3.right;
        return forward * input.y + right * input.x;
    }

    float ResolveMovementReferenceYaw()
    {
        var ctx = ResolveMovementContext(out _);
        if (ctx != null)
        {
            var reference = ctx.GetMovementReferenceRotation();
            return (reference * Vector3.forward).sqrMagnitude > 0.0001f
                ? reference.eulerAngles.y
                : float.NaN;
        }

        var mainCamera = Camera.main;
        return mainCamera != null ? mainCamera.transform.eulerAngles.y : float.NaN;
    }

    /// <summary>注入优先；缺失时回落 <see cref="GameModeManager"/> 单例，避免 CameraFwd 静默失效。</summary>
    IGameModeMovementContext ResolveMovementContext(out string source)
    {
        if (_movementContext != null)
        {
            source = "injected";
            return _movementContext;
        }

        var gmm = GameModeManager.Instance;
        if (gmm != null)
        {
            source = "GameModeManager";
            return gmm;
        }

        source = "missing";
        return null;
    }

    void LogLocomotionInputTrace(
        Player p,
        Vector2 rawInput,
        Vector3 worldDir,
        string ctxSource,
        bool cameraRelative)
    {
        if (rawInput.sqrMagnitude < 0.0001f)
        {
            return;
        }

        var camFwd = Vector3.forward;
        var ctx = ResolveMovementContext(out _);
        if (ctx != null && ctx.IsCameraRelativeMovement)
        {
            camFwd = ctx.GetMovementReferenceRotation() * Vector3.forward;
        }

        var charFwd = p.LogicForward;
        LocomotionDebug.TryLogCameraRelativeAnomaly(
            p, rawInput, worldDir, camFwd, charFwd, cameraRelative, ctxSource, ref _nextLocoAnomalyLogTime);

        LocomotionDebug.LogTrace(
            p,
            LocomotionDebug.CatInput,
            $"raw=({rawInput.x:F2},{rawInput.y:F2}) world=({worldDir.x:F2},{worldDir.z:F2}) " +
            $"camRel={cameraRelative} ctx={ctxSource} state={p.States?.Current?.StateId} " +
            $"charFwd=({charFwd.x:F2},{charFwd.z:F2}) camFwd=({camFwd.x:F2},{camFwd.z:F2})",
            ref _nextLocoInputTraceTime);
    }

    /// <summary>
    /// 场景视图调试：与 <see cref="ResolveWorldDirection"/> 相同的参考旋转，绘制水平面上「镜头参考前」与「角色前」。
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!debugForwardDirectionArrows)
        {
            return;
        }

        Init();

        var origin = transform.position + Vector3.up * debugArrowHeightOffset;

        if (!TryGetDebugFlatCameraReferenceForward(out var camFlatFwd))
        {
            return;
        }

        DrawHorizontalDirectionArrow(origin, camFlatFwd, debugArrowLength, debugCameraRelativeArrowColor);

        var playerFlatFwd = transform.forward;
        playerFlatFwd.y = 0f;
        if (playerFlatFwd.sqrMagnitude > 1e-8f)
        {
            DrawHorizontalDirectionArrow(
                origin,
                playerFlatFwd.normalized,
                debugArrowLength,
                debugPlayerForwardArrowColor);
        }
    }

    /// <summary>
    /// 与移动解析一致的相机参考：取 yaw 后在 XZ 平面上的单位前向；非相机相对模式时为世界 +Z。
    /// </summary>
    private bool TryGetDebugFlatCameraReferenceForward(out Vector3 flatForward)
    {
        flatForward = Vector3.forward;

        var ctx = ResolveMovementContext(out _);
        if (ctx != null && !ctx.IsCameraRelativeMovement)
        {
            flatForward = Vector3.forward;
            return true;
        }

        Quaternion refRotation;

        if (ctx != null)
        {
            refRotation = ctx.GetMovementReferenceRotation();
        }
        else
        {
            var mainCam = Camera.main;
            if (mainCam == null)
            {
                flatForward = Vector3.forward;
                return true;
            }

            refRotation = Quaternion.Euler(0f, mainCam.transform.eulerAngles.y, 0f);
        }

        var raw = refRotation * Vector3.forward;
        raw.y = 0f;
        if (raw.sqrMagnitude < 1e-8f)
        {
            flatForward = Vector3.forward;
            return true;
        }

        flatForward = raw.normalized;
        return true;
    }

    private static void DrawHorizontalDirectionArrow(Vector3 origin, Vector3 directionXZUnit, float length, Color color)
    {
        var dir = directionXZUnit;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-8f)
        {
            return;
        }

        dir.Normalize();
        var tip = origin + dir * length;

        Gizmos.color = color;
        Gizmos.DrawLine(origin, tip);

        var headLen = Mathf.Clamp(length * 0.18f, 0.05f, length * 0.45f);
        var wing = Vector3.Cross(Vector3.up, dir).normalized * (headLen * 0.42f);
        var back = -dir * headLen;
        Gizmos.DrawLine(tip, tip + back + wing);
        Gizmos.DrawLine(tip, tip + back - wing);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {

    }
#endif
}

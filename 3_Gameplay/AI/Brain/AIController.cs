using UnityEngine;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy))]
[AddComponentMenu("GameMain/AI/AI Controller")]
public sealed class AIController : EntityController
{
    [Header("References")]
    [SerializeField] Enemy enemy;
    [SerializeField] Entity initialTarget;
    [SerializeField] bool autoFindPlayerTarget = true;
    [SerializeField, Min(0.05f)] float playerSearchInterval = 0.25f;

    [Header("D1 Blackboard")]
    [SerializeField] bool assignInitialTargetOnEnable = true;

    [Header("D4 Skill Selection")]
    [SerializeField] SkillEntrySlot skillEntrySlot = SkillEntrySlot.LM;
    [SerializeField, Min(0f)] float meleeRange = 1.5f;
    [SerializeField] MonsterPersonalitySO personality;

    readonly AiBlackboard _blackboard = new AiBlackboard();
    readonly AiCommandBuffer _commands = new AiCommandBuffer();
    readonly SkillSelector _skillSelector = new SkillSelector();
    BehaviorTree _tree;
    EnemyPerception _perception;
    bool _runtimeBlockedLogged;
    string _lastSelectorFailReason;
    float _nextPlayerSearchTime;
    bool _noPlayerLogged;

    public Enemy Enemy => enemy;
    public IBlackboardReader Blackboard => _blackboard;
    public IBlackboardWriter BlackboardWriter => _blackboard;

    void Awake()
    {
        if (enemy == null)
        {
            enemy = GetComponent<Enemy>();
        }

        _perception = GetComponent<EnemyPerception>();

        RebuildBehaviorTree();
    }

    internal void ApplyDefinition(EnemyDefinitionSO definition)
    {
        if (definition == null)
        {
            return;
        }

        personality = definition.Personality;
        RebuildBehaviorTree();

        if (GameMainDebugSettings.EnemyPerception2208Log
            || GameMainDebugSettings.AIBrain2207Log)
        {
            Debug.Log(
                $"[AI] Definition bind id={definition.Id} " +
                $"personality={personality?.name ?? "-"} " +
                $"behaviorTree={definition.BehaviorTreeId} log=220.8",
                this);
        }
    }

    void RebuildBehaviorTree()
    {
        var effectiveMeleeRange = personality != null ? personality.MeleeRange : meleeRange;
        var effectivePreferredDistance = personality != null
            ? personality.PreferredDistance
            : effectiveMeleeRange;
        var effectiveAggressive = personality == null || personality.Aggressive;

        _tree = new BehaviorTree(
            new BtSelector(
                "ApproachOrReleaseSkill",
                new BtSequence(
                    "ReleaseSkillWhenInRange",
                    new BtHasTarget(),
                    new BtDistanceCheck(effectiveMeleeRange),
                    new BtReleaseSkill(skillEntrySlot, effectiveMeleeRange, effectiveAggressive)),
                new BtSequence(
                    "ApproachTarget",
                    new BtHasTarget(),
                    new BtSetMovementIntent(effectivePreferredDistance))));
    }

    void OnEnable()
    {
        _runtimeBlockedLogged = false;
        _nextPlayerSearchTime = 0f;
        _noPlayerLogged = false;
        if (assignInitialTargetOnEnable)
        {
            var target = initialTarget;
            if (target != null)
            {
                SetCurrentTarget(target);
            }
            else
            {
                if (_perception == null || !_perception.IsActive)
                {
                    RefreshPlayerTarget(true);
                }
            }
        }

        if (GameMainDebugSettings.AIBrain2207Log)
        {
            Debug.Log(
                $"[AI] Producer host=Enemy primary=AIController " +
                $"emitter=DebugOnly target={(_blackboard.Contains(AiBlackboardKeys.CurrentTarget) ? "ready" : "missing")} log=220.7",
                this);

            if (personality != null)
            {
                Debug.Log(
                    $"[AI] Personality preferred={personality.PreferredDistance:F2} " +
                    $"melee={personality.MeleeRange:F2} aggressive={personality.Aggressive} " +
                    $"retreat={personality.RetreatThreshold:F2} " +
                    $"combo={personality.ComboProbability:F2} " +
                    $"counter={personality.CounterAttackWeight:F2} " +
                    $"random={personality.Randomness:F2} log=220.8",
                    this);
            }
        }
    }

    void Update()
    {
        if (!IsActive || enemy == null || enemy.IsDead)
        {
            return;
        }

        if (_perception == null)
        {
            _perception = GetComponent<EnemyPerception>();
        }

        if (_perception == null || !_perception.IsActive)
        {
            RefreshPlayerTarget();
        }

        if (!enemy.IsRuntimeReady)
        {
            if (!_runtimeBlockedLogged && GameMainDebugSettings.AIBrain2207Log)
            {
                Debug.LogWarning(
                    $"[AI] Tick host=Enemy result=Blocked reason=" +
                    $"{enemy.RuntimeReadyFailure ?? "runtime-not-ready"} log=220.7",
                    this);
                _runtimeBlockedLogged = true;
            }

            return;
        }

        _commands.BeginTick();
        var context = new AiBtTickContext(
            _blackboard,
            _blackboard,
            enemy,
            _commands,
            (ISkillHost)enemy,
            _skillSelector,
            Time.time,
            Time.deltaTime);
        var status = _tree.Tick(context);
        CommitCommands();

        if (GameMainDebugSettings.AIBrain2207Log)
        {
            var targetName = _blackboard.TryGet(AiBlackboardKeys.CurrentTarget, out Entity target)
                ? target != null ? target.name : "-"
                : "-";
            Debug.Log(
                $"[AI] Tick host=Enemy bb.keys={_blackboard.Count} " +
                $"bb.revision={_blackboard.Revision} target={targetName} log=220.7",
                this);
            Debug.Log(
                $"[AI] BT status={status} node={context.LastNodeName ?? "-"} " +
                $"tree=ApproachOrReleaseSkill log=220.7",
                this);

            if (_blackboard.TryGet(AiBlackboardKeys.LastSelectorFailReason, out string failReason))
            {
                if (_lastSelectorFailReason != failReason)
                {
                    _lastSelectorFailReason = failReason;
                    Debug.Log($"[AI] Selector miss reason={failReason} log=220.7", this);
                }
            }
            else
            {
                _lastSelectorFailReason = null;
            }
        }
    }

    void CommitCommands()
    {
        if (_commands.TryConsumeMovement(out var movementCommand))
        {
            CommitMovementCommand(in movementCommand);
        }
        else
        {
            ((IMovementIntentHost)enemy).ClearMovementIntent();
        }

        if (_commands.TryConsumeSkill(out var skillCommand))
        {
            CommitSkillCommand(in skillCommand);
        }
    }

    void CommitMovementCommand(in AiCommand command)
    {
        var movementHost = (IMovementIntentHost)enemy;
        if (command.Kind == AiCommandKind.ClearMove
            || enemy.StateManager == null
            || !enemy.StateManager.IsCurrentOfType<EnemyLocomotionState>())
        {
            movementHost.ClearMovementIntent();
            return;
        }

        movementHost.SetMovementIntent(command.Direction, command.WantsRun);
        if (GameMainDebugSettings.AIBrain2207Log)
        {
            Debug.Log(
                $"[AI] MoveIntent dir={command.Direction} run={command.WantsRun} " +
                $"source={command.Source} result=Committed log=220.7",
                this);
        }
    }

    void CommitSkillCommand(in AiCommand command)
    {
        if (enemy.StateManager == null
            || !enemy.StateManager.IsCurrentOfType<EnemyLocomotionState>())
        {
            return;
        }

        var intent = command.Intent;
        var result = enemy.TryEnqueue(in intent);
        if (GameMainDebugSettings.AIBrain2207Log)
        {
            var entryNumber = (int)command.Intent.Kind
                              - (int)GameplayIntentKind.Skill_Entry_01
                              + 1;
            Debug.Log(
                $"[AI] ReleaseSkill entry={entryNumber:00} " +
                $"semantic={command.Intent.Semantic} result={result} source={command.Source} log=220.7",
                this);
        }
    }

    public void SetCurrentTarget(Entity target)
    {
        if (target == null)
        {
            _blackboard.Remove(AiBlackboardKeys.CurrentTarget);
            return;
        }

        _blackboard.Set(AiBlackboardKeys.CurrentTarget, target);
    }

    void RefreshPlayerTarget(bool force = false)
    {
        if (!autoFindPlayerTarget
            || (!force && Time.time < _nextPlayerSearchTime))
        {
            return;
        }

        _nextPlayerSearchTime = Time.time + Mathf.Max(0.05f, playerSearchInterval);

        var hasCurrentTarget = _blackboard.TryGet(
            AiBlackboardKeys.CurrentTarget,
            out Entity currentTarget);
        var currentTargetValid = hasCurrentTarget
                                 && currentTarget != null
                                 && !currentTarget.IsDead;

        if (!TryFindNearestPlayer(out var nearestPlayer))
        {
            if (!currentTargetValid)
            {
                if (hasCurrentTarget)
                {
                    SetCurrentTarget(null);
                }

                if (!_noPlayerLogged)
                {
                    LogTargetSearch("Lose", "no-player", null, 0f);
                    _noPlayerLogged = true;
                }
            }

            return;
        }

        _noPlayerLogged = false;

        if (!currentTargetValid || !(currentTarget is Player))
        {
            SetCurrentTarget(nearestPlayer);
            LogTargetSearch("Acquire", "nearest-player", nearestPlayer, DistanceTo(nearestPlayer));
            return;
        }

        var nearestDistance = DistanceTo(nearestPlayer);
        var currentDistance = DistanceTo(currentTarget);
        if (nearestPlayer != currentTarget && nearestDistance + 0.01f < currentDistance)
        {
            SetCurrentTarget(nearestPlayer);
            LogTargetSearch("Switch", "nearer-player", nearestPlayer, nearestDistance);
        }
    }

    bool TryFindNearestPlayer(out Player nearestPlayer)
    {
        nearestPlayer = null;
        if (enemy == null)
        {
            return false;
        }

        var players = FindObjectsOfType<Player>();
        var nearestDistance = float.PositiveInfinity;
        for (var i = 0; i < players.Length; i++)
        {
            var candidate = players[i];
            if (candidate == null || candidate.IsDead)
            {
                continue;
            }

            var distance = DistanceTo(candidate);
            if (distance >= nearestDistance)
            {
                continue;
            }

            nearestPlayer = candidate;
            nearestDistance = distance;
        }

        return nearestPlayer != null;
    }

    float DistanceTo(Entity target)
    {
        if (target == null || enemy == null)
        {
            return float.PositiveInfinity;
        }

        var offset = target.Position - enemy.Position;
        return offset.magnitude;
    }

    void LogTargetSearch(string result, string reason, Player target, float distance)
    {
        if (!GameMainDebugSettings.AIBrain2207Log)
        {
            return;
        }

        Debug.Log(
            $"[AI] TargetSearch result={result} reason={reason} " +
            $"target={target?.name ?? "-"} distance={distance:F2} " +
            "mode=NearestRuntimePlayer log=220.7",
            this);
    }

    public bool TryGetCurrentTarget(out Entity target)
        => _blackboard.TryGet(AiBlackboardKeys.CurrentTarget, out target);
}

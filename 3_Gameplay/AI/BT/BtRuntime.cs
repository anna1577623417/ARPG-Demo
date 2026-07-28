using System;
using System.Collections.Generic;
using UnityEngine;

public enum BtStatus : byte
{
    Running = 0,
    Success = 1,
    Failure = 2,
}

public sealed class AiBtTickContext
{
    public AiBtTickContext(
        IBlackboardReader blackboard,
        IBlackboardWriter blackboardWriter,
        Entity actor,
        AiCommandBuffer commands,
        ISkillHost skillHost,
        SkillSelector skillSelector,
        float now,
        float deltaTime)
    {
        Blackboard = blackboard;
        BlackboardWriter = blackboardWriter;
        Actor = actor;
        Commands = commands;
        SkillHost = skillHost;
        SkillSelector = skillSelector;
        Now = now;
        DeltaTime = deltaTime;
    }

    public IBlackboardReader Blackboard { get; }
    public IBlackboardWriter BlackboardWriter { get; }
    public Entity Actor { get; }
    public AiCommandBuffer Commands { get; }
    public ISkillHost SkillHost { get; }
    public SkillSelector SkillSelector { get; }
    public float Now { get; }
    public float DeltaTime { get; }
    public string LastNodeName { get; private set; }

    public void RecordNode(string nodeName)
    {
        LastNodeName = nodeName;
    }
}

public interface IBtNode
{
    string Name { get; }
    BtStatus Tick(AiBtTickContext context);
    void Reset();
}

public sealed class BehaviorTree
{
    readonly IBtNode _root;

    public BehaviorTree(IBtNode root)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
    }

    public BtStatus Tick(AiBtTickContext context)
    {
        return _root.Tick(context);
    }

    public void Reset()
    {
        _root.Reset();
    }
}

public sealed class BtSequence : IBtNode
{
    readonly IReadOnlyList<IBtNode> _children;
    int _childIndex;

    public BtSequence(string name, params IBtNode[] children)
    {
        Name = name ?? "Sequence";
        _children = children ?? Array.Empty<IBtNode>();
    }

    public string Name { get; }

    public BtStatus Tick(AiBtTickContext context)
    {
        while (_childIndex < _children.Count)
        {
            var status = _children[_childIndex].Tick(context);
            if (status == BtStatus.Running)
            {
                return status;
            }

            if (status == BtStatus.Failure)
            {
                Reset();
                return status;
            }

            _childIndex++;
        }

        Reset();
        return BtStatus.Success;
    }

    public void Reset()
    {
        _childIndex = 0;
        for (var i = 0; i < _children.Count; i++)
        {
            _children[i].Reset();
        }
    }
}

public sealed class BtSelector : IBtNode
{
    readonly IReadOnlyList<IBtNode> _children;
    int _childIndex;

    public BtSelector(string name, params IBtNode[] children)
    {
        Name = name ?? "Selector";
        _children = children ?? Array.Empty<IBtNode>();
    }

    public string Name { get; }

    public BtStatus Tick(AiBtTickContext context)
    {
        while (_childIndex < _children.Count)
        {
            var status = _children[_childIndex].Tick(context);
            if (status == BtStatus.Running)
            {
                return status;
            }

            if (status == BtStatus.Success)
            {
                Reset();
                return status;
            }

            _childIndex++;
        }

        Reset();
        return BtStatus.Failure;
    }

    public void Reset()
    {
        _childIndex = 0;
        for (var i = 0; i < _children.Count; i++)
        {
            _children[i].Reset();
        }
    }
}

public sealed class BtWait : IBtNode
{
    readonly float _duration;
    float _remaining;
    bool _started;

    public BtWait(float durationSeconds, string name = "Wait")
    {
        _duration = Math.Max(0f, durationSeconds);
        Name = name ?? "Wait";
    }

    public string Name { get; }

    public BtStatus Tick(AiBtTickContext context)
    {
        context.RecordNode(Name);
        if (!_started)
        {
            _started = true;
            _remaining = _duration;
        }

        _remaining -= Math.Max(0f, context.DeltaTime);
        if (_remaining > 0f)
        {
            return BtStatus.Running;
        }

        Reset();
        return BtStatus.Success;
    }

    public void Reset()
    {
        _remaining = 0f;
        _started = false;
    }
}

public sealed class BtHasTarget : IBtNode
{
    public BtHasTarget(string name = "HasTarget")
    {
        Name = name ?? "HasTarget";
    }

    public string Name { get; }

    public BtStatus Tick(AiBtTickContext context)
    {
        context.RecordNode(Name);
        if (context.Blackboard.TryGet(AiBlackboardKeys.CurrentTarget, out Entity target)
            && target != null)
        {
            return BtStatus.Success;
        }

        return BtStatus.Failure;
    }

    public void Reset() { }
}

public sealed class BtSetMovementIntent : IBtNode
{
    readonly float _stopDistance;

    public BtSetMovementIntent(
        float stopDistance,
        string name = "SetMovementIntent")
    {
        _stopDistance = Math.Max(0f, stopDistance);
        Name = name ?? "SetMovementIntent";
    }

    public string Name { get; }

    public BtStatus Tick(AiBtTickContext context)
    {
        context.RecordNode(Name);
        if (context.Actor == null
            || context.Commands == null
            || !context.Blackboard.TryGet(AiBlackboardKeys.CurrentTarget, out Entity target)
            || target == null)
        {
            context.Commands?.ClearMovement(Name);
            return BtStatus.Failure;
        }

        var direction = target.Position - context.Actor.Position;
        direction.y = 0f;
        if (direction.magnitude <= _stopDistance)
        {
            context.Commands.ClearMovement(Name);
            return BtStatus.Success;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            context.Commands.ClearMovement(Name);
            return BtStatus.Success;
        }

        context.Commands.SetMovement(direction, wantsRun: false, source: Name);
        return BtStatus.Success;
    }

    public void Reset() { }
}

public sealed class BtDistanceCheck : IBtNode
{
    readonly float _maxDistance;

    public BtDistanceCheck(float maxDistance, string name = "DistanceCheck")
    {
        _maxDistance = Math.Max(0f, maxDistance);
        Name = name ?? "DistanceCheck";
    }

    public string Name { get; }

    public BtStatus Tick(AiBtTickContext context)
    {
        context.RecordNode(Name);
        if (context.Actor == null
            || !context.Blackboard.TryGet(AiBlackboardKeys.CurrentTarget, out Entity target)
            || target == null)
        {
            return BtStatus.Failure;
        }

        var distance = Vector3.ProjectOnPlane(
            target.Position - context.Actor.Position,
            Vector3.up).magnitude;
        return distance <= _maxDistance ? BtStatus.Success : BtStatus.Failure;
    }

    public void Reset() { }
}

public sealed class BtReleaseSkill : IBtNode
{
    readonly SkillEntrySlot _entrySlot;
    readonly float _meleeRange;
    readonly bool _aggressive;

    public BtReleaseSkill(
        SkillEntrySlot entrySlot,
        float meleeRange,
        bool aggressive,
        string name = "ReleaseSkill")
    {
        _entrySlot = entrySlot;
        _meleeRange = Math.Max(0f, meleeRange);
        _aggressive = aggressive;
        Name = name ?? "ReleaseSkill";
    }

    public string Name { get; }

    public BtStatus Tick(AiBtTickContext context)
    {
        context.RecordNode(Name);
        if (context.SkillSelector == null
            || context.SkillHost == null
            || context.Commands == null)
        {
            return BtStatus.Failure;
        }

        if (!context.SkillSelector.TryPick(
                context.SkillHost,
                context.Blackboard,
                _entrySlot,
                _meleeRange,
                _aggressive,
                context.Now,
                out var intent,
                out var reason))
        {
            context.BlackboardWriter?.Set(AiBlackboardKeys.LastSelectorFailReason, reason);
            return BtStatus.Failure;
        }

        context.BlackboardWriter?.Remove(AiBlackboardKeys.LastSelectorFailReason);
        context.Commands.RequestSkill(in intent, Name);
        return BtStatus.Success;
    }

    public void Reset() { }
}

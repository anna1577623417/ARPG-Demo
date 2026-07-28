using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-60)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy))]
[RequireComponent(typeof(AIController))]
[AddComponentMenu("GameMain/AI/Enemy Perception")]
public sealed class EnemyPerception : MonoBehaviour
{
    [SerializeField] Enemy enemy;
    [SerializeField] AIController aiController;
    [SerializeField] PerceptionConfigSO config;
    [SerializeField] bool perceptionEnabled = true;

    readonly List<Entity> _visibleTargets = new List<Entity>(8);
    float _nextScanTime;
    Entity _currentTarget;

    public bool IsActive => perceptionEnabled;
    public PerceptionConfigSO Config => config;

    void Awake()
    {
        if (enemy == null)
        {
            enemy = GetComponent<Enemy>();
        }

        if (aiController == null)
        {
            aiController = GetComponent<AIController>();
        }
    }

    void OnEnable()
    {
        _nextScanTime = 0f;
        _currentTarget = null;
    }

    void Update()
    {
        if (!perceptionEnabled || enemy == null || aiController == null || enemy.IsDead)
        {
            return;
        }

        if (Time.time < _nextScanTime)
        {
            return;
        }

        var interval = config != null ? config.ScanInterval : 0.25f;
        _nextScanTime = Time.time + Mathf.Max(0.05f, interval);
        Scan();
    }

    public void ApplyDefinition(PerceptionConfigSO definition)
    {
        config = definition;
        _nextScanTime = 0f;
    }

    void Scan()
    {
        _visibleTargets.Clear();
        var nearest = FindVisibleTargets();
        var writer = aiController.BlackboardWriter;
        writer.Set(AiBlackboardKeys.VisibleTargets, _visibleTargets.ToArray());
        writer.Set(AiBlackboardKeys.TargetDistance, nearest != null ? DistanceTo(nearest) : 0f);
        writer.Set(AiBlackboardKeys.VisibleTargetCount, _visibleTargets.Count);

        if (nearest == null)
        {
            writer.Remove(AiBlackboardKeys.LastSeenPosition);
            if (_currentTarget != null)
            {
                _currentTarget = null;
                aiController.SetCurrentTarget(null);
                Log("lose", "no-visible-target", null, 0f);
            }

            return;
        }

        writer.Set(AiBlackboardKeys.LastSeenPosition, nearest.Position);
        if (_currentTarget != nearest)
        {
            var result = _currentTarget == null ? "acquire" : "switch";
            _currentTarget = nearest;
            aiController.SetCurrentTarget(nearest);
            Log(result, result == "acquire" ? "visible-target" : "nearer-target", nearest, DistanceTo(nearest));
        }
    }

    Entity FindVisibleTargets()
    {
        var nearest = default(Entity);
        var nearestDistance = float.PositiveInfinity;
        var candidates = FindObjectsOfType<Entity>();
        var radius = config != null ? config.DetectionRadius : 8f;
        var radiusSquared = radius * radius;

        for (var i = 0; i < candidates.Length; i++)
        {
            var candidate = candidates[i];
            if (!IsCandidate(candidate, radiusSquared))
            {
                continue;
            }

            _visibleTargets.Add(candidate);
            var distance = DistanceTo(candidate);
            if (distance < nearestDistance)
            {
                nearest = candidate;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    bool IsCandidate(Entity candidate, float radiusSquared)
    {
        if (candidate == null
            || candidate == enemy
            || candidate.IsDead
            || candidate.TeamId == enemy.TeamId
            || (candidate.UnitKind != UnitKind.Hero
                && candidate.UnitKind != UnitKind.HeroClone))
        {
            return false;
        }

        var targetMask = config != null ? config.TargetLayers.value : ~0;
        if ((targetMask & (1 << candidate.gameObject.layer)) == 0)
        {
            return false;
        }

        var offset = candidate.Position - enemy.Position;
        offset.y = 0f;
        if (offset.sqrMagnitude > radiusSquared)
        {
            return false;
        }

        var fieldOfView = config != null ? config.FieldOfView : 360f;
        if (fieldOfView < 359.9f
            && offset.sqrMagnitude > 0.0001f
            && Vector3.Angle(enemy.Forward, offset) > fieldOfView * 0.5f)
        {
            return false;
        }

        if (config != null
            && config.RequireLineOfSight
            && Physics.Linecast(
                enemy.Position + Vector3.up,
                candidate.Position + Vector3.up,
                config.ObstructionLayers))
        {
            return false;
        }

        return true;
    }

    float DistanceTo(Entity target)
    {
        return target == null || enemy == null
            ? float.PositiveInfinity
            : Vector3.Distance(enemy.Position, target.Position);
    }

    void Log(string result, string reason, Entity target, float distance)
    {
        if (!GameMainDebugSettings.EnemyPerception2208Log)
        {
            return;
        }

        Debug.Log(
            $"[Perception] {result} target={target?.name ?? "-"} " +
            $"dist={distance:F2} reason={reason} visible={_visibleTargets.Count} log=220.8",
            this);
    }
}

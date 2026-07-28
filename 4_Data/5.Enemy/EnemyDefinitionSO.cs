using System;
using UnityEngine;

[CreateAssetMenu(
    menuName = "GameMain/AI/Enemy Definition",
    fileName = "EnemyDefinition_")]
public sealed class EnemyDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] string id = "enemy";
    [SerializeField] string displayName = "Enemy";
    [SerializeField] UnitKind unitKind = UnitKind.Monster;
    [SerializeField] int teamId = 1;
    [SerializeField] FactionTag faction = FactionTag.Enemy;

    [Header("Runtime Prefab")]
    [SerializeField] Enemy runtimePrefab;

    [Header("Runtime Data")]
    [SerializeField] EntityStatsSO stats;
    [SerializeField] SkillEntryLoadoutSO skillLoadout;
    [SerializeField] ReactionProfileSO reactionProfile;
    [SerializeField] MonsterPersonalitySO personality;
    [SerializeField] LocomotionProfile locomotionProfile;

    [Header("AI Configuration")]
    [SerializeField] string behaviorTreeId = "ApproachOrReleaseSkill";
    [SerializeField] PerceptionConfigSO perceptionConfig;

    public string Id => id;
    public string DisplayName => displayName;
    public UnitKind UnitKind => unitKind;
    public int TeamId => teamId;
    public FactionTag Faction => faction;
    public Enemy RuntimePrefab => runtimePrefab;
    public EntityStatsSO Stats => stats;
    public SkillEntryLoadoutSO SkillLoadout => skillLoadout;
    public ReactionProfileSO ReactionProfile => reactionProfile;
    public MonsterPersonalitySO Personality => personality;
    public LocomotionProfile LocomotionProfile => locomotionProfile;
    public string BehaviorTreeId => behaviorTreeId;
    public PerceptionConfigSO PerceptionConfig => perceptionConfig;

    public bool TryValidate(out string reason)
    {
        if (runtimePrefab == null)
        {
            reason = "missing-runtime-prefab";
        }
        else if (stats == null)
        {
            reason = "missing-stats";
        }
        else if (skillLoadout == null)
        {
            reason = "missing-skill-loadout";
        }
        else if (reactionProfile == null)
        {
            reason = "missing-reaction-profile";
        }
        else if (personality == null)
        {
            reason = "missing-personality";
        }
        else if (perceptionConfig == null)
        {
            reason = "missing-perception-config";
        }
        else if (string.IsNullOrWhiteSpace(id))
        {
            reason = "missing-id";
        }
        else if (string.IsNullOrWhiteSpace(behaviorTreeId))
        {
            reason = "missing-behavior-tree-id";
        }
        else
        {
            reason = null;
        }

        return string.IsNullOrEmpty(reason);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!TryValidate(out var reason))
        {
            Debug.LogError(
                $"[EnemyDefinition] invalid asset={name} reason={reason}",
                this);
        }
    }
#endif
}

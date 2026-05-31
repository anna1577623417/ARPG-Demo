using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能组 — 多条 Route 共享 CD / Icon / Cost；组内选路仍由 DirectionalRouteSet 或 CombatGraph 决定。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/SkillRoute/Group/Skill Group", fileName = "Group_")]
public sealed class SkillGroupDefinition : ScriptableObject, ISkillUnit
{
    [Header("Identity (Group-level)")]
    [SerializeField] string displayName;
    [SerializeField] Sprite icon;
    [SerializeField, Min(0f)] float baseCooldownSeconds = 1f;
    [SerializeField] SkillCostEntry[] costs;
    [SerializeField] ulong requiredAbilityTags;

    [Header("Composition")]
    [SerializeField, Tooltip("组内成员 Route（顺序无意义）。")]
    SkillRouteDefinition[] routes;

    [SerializeField, Tooltip("无方向 / Resolver 未命中时的默认 Route。")]
    SkillRouteDefinition fallbackRoute;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public Sprite Icon => icon;
    public float CooldownSeconds => baseCooldownSeconds;
    public SkillCostEntry[] Costs => costs;
    public ulong RequiredAbilityTags => requiredAbilityTags;

    public IReadOnlyList<SkillRouteDefinition> Routes => routes;
    public SkillRouteDefinition FallbackRoute => fallbackRoute;

    public bool ContainsRoute(SkillRouteDefinition route)
    {
        if (route == null || routes == null)
        {
            return false;
        }

        for (var i = 0; i < routes.Length; i++)
        {
            if (routes[i] == route)
            {
                return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (routes == null)
        {
            return;
        }

        for (var i = 0; i < routes.Length; i++)
        {
            var r = routes[i];
            if (r == null)
            {
                continue;
            }

            r.EditorAssignOwnerGroup(this);
        }
    }
#endif
}

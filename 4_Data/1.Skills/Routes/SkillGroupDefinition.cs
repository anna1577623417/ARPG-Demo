using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能组 — 多条 Route 共享 CD / Icon / Cost；组内四向由本类型 SelectByDirection 决定（136.1，不再使用 DirectionalRouteSet）。
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
    [SerializeField, Tooltip("组内成员 Route（顺序无意义；可与四向槽位重复引用）。")]
    SkillRouteDefinition[] routes;

    [Header("Directional (136.1 — 组内四向，替代 Entry.DirectionalRoute)")]
    [SerializeField, Tooltip("前向 Route。")]
    SkillRouteDefinition forward;

    [SerializeField, Tooltip("后向 Route。")]
    SkillRouteDefinition backward;

    [SerializeField, Tooltip("左向 Route。")]
    SkillRouteDefinition left;

    [SerializeField, Tooltip("右向 Route。")]
    SkillRouteDefinition right;

    [SerializeField, Tooltip("摇杆中性时是否回落到 Forward。")]
    bool defaultToForwardWhenNeutral = true;

    [SerializeField, Tooltip("四向均未命中时的组内默认 Route。")]
    SkillRouteDefinition fallbackRoute;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public Sprite Icon => icon;
    public float CooldownSeconds => baseCooldownSeconds;
    public SkillCostEntry[] Costs => costs;
    public ulong RequiredAbilityTags => requiredAbilityTags;

    public IReadOnlyList<SkillRouteDefinition> Routes => routes;
    public SkillRouteDefinition Forward => forward;
    public SkillRouteDefinition Backward => backward;
    public SkillRouteDefinition Left => left;
    public SkillRouteDefinition Right => right;
    public bool DefaultToForwardWhenNeutral => defaultToForwardWhenNeutral;
    public SkillRouteDefinition FallbackRoute => fallbackRoute;

    public SkillRouteDefinition SelectByDirection(DirectionalRouteType dir)
    {
        switch (dir)
        {
            case DirectionalRouteType.Backward:
                return backward != null ? backward : forward;
            case DirectionalRouteType.Left:
                return left != null ? left : forward;
            case DirectionalRouteType.Right:
                return right != null ? right : forward;
            default:
                return forward;
        }
    }

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
        SyncRoutesArrayFromDirectionalFields();

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

    void SyncRoutesArrayFromDirectionalFields()
    {
        var list = new System.Collections.Generic.List<SkillRouteDefinition>(8);
        void AddUnique(SkillRouteDefinition r)
        {
            if (r == null)
            {
                return;
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] == r)
                {
                    return;
                }
            }

            list.Add(r);
        }

        AddUnique(forward);
        AddUnique(backward);
        AddUnique(left);
        AddUnique(right);
        routes = list.ToArray();
    }
#endif
}

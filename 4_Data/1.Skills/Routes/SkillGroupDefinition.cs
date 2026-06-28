using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 技能组 — 多条 Route 共享 CD / Icon / Cost；组内八向由 <see cref="SelectByDirection"/> 决定（173.3）。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/SkillRoute/Group/Skill Group", fileName = "Group_")]
public sealed class SkillGroupDefinition : ScriptableObject, ISkillUnit
{
    // 173.3 — Header attributes removed; SkillGroupDefinitionInspector owns layout.
    [SerializeField] string displayName;
    [SerializeField] Sprite icon;
    [SerializeField, Tooltip("是否在 HUD 显示本技能组（一槽一个 Widget；子 Route 无需单独勾选 Show On Hud）。")]
    bool showOnHud;
    [SerializeField, Min(0f)] float baseCooldownSeconds = 1f;
    [SerializeField] SkillCostEntry[] costs;
    [SerializeField] ulong requiredAbilityTags;

    [SerializeField, Tooltip("组内成员 Route（OnValidate 自动同步；勿手改）。")]
    SkillRouteDefinition[] routes;

    [SerializeField, Tooltip("前向 Route。")]
    SkillRouteDefinition forward;

    [SerializeField, Tooltip("左前 Route。")]
    SkillRouteDefinition forwardLeft;

    [SerializeField, Tooltip("右前 Route。")]
    SkillRouteDefinition forwardRight;

    [SerializeField, Tooltip("后向 Route。")]
    SkillRouteDefinition backward;

    [SerializeField, Tooltip("左后 Route。")]
    SkillRouteDefinition backwardLeft;

    [SerializeField, Tooltip("右后 Route。")]
    SkillRouteDefinition backwardRight;

    [SerializeField, Tooltip("左向 Route。")]
    SkillRouteDefinition left;

    [SerializeField, Tooltip("右向 Route。")]
    SkillRouteDefinition right;

    [SerializeField, Tooltip("勾选 = 摇杆中性时走 Fallback Route（艾尔登：站立后撤步）。\n" +
                             "不勾选 = 摇杆中性时走 Forward 槽（旧 4 槽兼容）。")]
    bool useFallbackOnNeutral = true;

    [SerializeField, Tooltip("八向均未命中或中性 Fallback 时的 Route。")]
    SkillRouteDefinition fallbackRoute;

    [SerializeField, Tooltip("206.1 — Motion 态（持续移动按 Space）专用 Route。\n" +
                              "为空 → 复用 Forward 槽。\n" +
                              "用于做前冲翻滚等区别于站立 F-Dodge 的独立动作。")]
    SkillRouteDefinition motionForwardRoute;

    [SerializeField, Tooltip("173.6 — Group 级准入规则（可选）。\n" +
                              "在 ContextGroup Gate 之后、Route Gate 之前生效。通常留空；\n" +
                              "用于「同一 ContextGroup 路由到此组后，组级再校验」的少数场景。")]
    AbilityGateRuleSO[] abilityGateRules;

#if UNITY_EDITOR
    [FormerlySerializedAs("defaultToForwardWhenNeutral")]
    [SerializeField, HideInInspector] bool defaultToForwardWhenNeutral = true;

    [SerializeField, HideInInspector] bool _1733NeutralMigrated;
#endif

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public Sprite Icon => icon;
    public bool ShowOnHud => showOnHud;
    public float CooldownSeconds => baseCooldownSeconds;
    public SkillCostEntry[] Costs => costs;
    public ulong RequiredAbilityTags => requiredAbilityTags;

    public IReadOnlyList<SkillRouteDefinition> Routes => routes;
    public SkillRouteDefinition Forward => forward;
    public SkillRouteDefinition ForwardLeft => forwardLeft;
    public SkillRouteDefinition ForwardRight => forwardRight;
    public SkillRouteDefinition Backward => backward;
    public SkillRouteDefinition BackwardLeft => backwardLeft;
    public SkillRouteDefinition BackwardRight => backwardRight;
    public SkillRouteDefinition Left => left;
    public SkillRouteDefinition Right => right;
    public bool UseFallbackOnNeutral => useFallbackOnNeutral;
    public SkillRouteDefinition FallbackRoute => fallbackRoute;
    public SkillRouteDefinition MotionForwardRoute => motionForwardRoute;
    public AbilityGateRuleSO[] AbilityGateRules => abilityGateRules;

    /// <summary>173.6 — 三段 Gate 中段：选路前的组级准入校验。空数组视为放行。</summary>
    public bool PassAbilityGate(in CombatContextSnapshot ctx)
    {
        if (abilityGateRules == null) return true;
        for (var i = 0; i < abilityGateRules.Length; i++)
        {
            var rule = abilityGateRules[i];
            if (rule != null && !rule.Pass(in ctx)) return false;
        }
        return true;
    }

    public SkillRouteDefinition SelectByDirection(DirectionalRouteType dir)
    {
        var picked = dir switch
        {
            DirectionalRouteType.Forward => forward,
            DirectionalRouteType.ForwardLeft => forwardLeft,
            DirectionalRouteType.ForwardRight => forwardRight,
            DirectionalRouteType.Backward => backward,
            DirectionalRouteType.BackwardLeft => backwardLeft,
            DirectionalRouteType.BackwardRight => backwardRight,
            DirectionalRouteType.Left => left,
            DirectionalRouteType.Right => right,
            _ => null,
        };

        if (picked != null)
        {
            return picked;
        }

        return dir switch
        {
            DirectionalRouteType.ForwardLeft or DirectionalRouteType.ForwardRight => forward,
            DirectionalRouteType.BackwardLeft or DirectionalRouteType.BackwardRight => backward,
            _ => null,
        };
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
        Migrate1733NeutralSemanticsOnce();
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

    void Migrate1733NeutralSemanticsOnce()
    {
        if (_1733NeutralMigrated)
        {
            return;
        }

        useFallbackOnNeutral = !defaultToForwardWhenNeutral;
        _1733NeutralMigrated = true;
        EditorUtility.SetDirty(this);
    }

    void SyncRoutesArrayFromDirectionalFields()
    {
        var list = new List<SkillRouteDefinition>(12);
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
        AddUnique(forwardLeft);
        AddUnique(forwardRight);
        AddUnique(backward);
        AddUnique(backwardLeft);
        AddUnique(backwardRight);
        AddUnique(left);
        AddUnique(right);
        AddUnique(fallbackRoute);
        AddUnique(motionForwardRoute);
        routes = list.ToArray();
    }
#endif
}

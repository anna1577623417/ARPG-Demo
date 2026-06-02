using UnityEngine;

/// <summary>
/// 单条能力准入规则：挂 Route.abilityGateRules（起手）或 AbilityMap（CombatFlow 流转）。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/SkillRoute/Ability/Ability Gate Rule", fileName = "AbilityGateRule_")]
public sealed class AbilityGateRuleSO : ScriptableObject
{
    [SerializeField] AbilitySemantic ability = AbilitySemantic.Mobility;

    [SerializeField] bool requireGrounded;
    [SerializeField] bool requireAirborne;
    [SerializeField] bool allowWhenGrounded = true;
    [SerializeField] bool allowWhenAirborne = true;

    public AbilitySemantic Ability => ability;
    public bool RequireGrounded => requireGrounded;
    public bool RequireAirborne => requireAirborne;
    public bool AllowWhenGrounded => allowWhenGrounded;
    public bool AllowWhenAirborne => allowWhenAirborne;

    public bool Pass(in CombatContextSnapshot ctx)
    {
        if (requireGrounded && ctx.IsAirborne)
        {
            return false;
        }

        if (requireAirborne && !ctx.IsAirborne)
        {
            return false;
        }

        if (!requireGrounded && !requireAirborne)
        {
            return ctx.IsAirborne ? allowWhenAirborne : allowWhenGrounded;
        }

        return true;
    }
}

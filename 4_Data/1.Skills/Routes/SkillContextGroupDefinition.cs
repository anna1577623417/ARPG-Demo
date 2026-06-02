using UnityEngine;

/// <summary>
/// 上下文语义组 — 将 CombatContext + Intent 映射到 SkillGroup（136.1 L3）。
/// 比 CombatGraph 更适合「同一入口、多上下文」的选组，不负责跨节点流转。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/SkillRoute/Context/Skill Context Group", fileName = "ContextGroup_")]
public sealed class SkillContextGroupDefinition : ScriptableObject
{
    [SerializeField, Tooltip("命中后输出的 Route 组。")]
    SkillGroupDefinition targetGroup;

    [Header("Intent")]
    [SerializeField, Tooltip("限定入口槽位；Any 表示任意槽位。")]
    SkillEntrySlot requiredSlot = SkillEntrySlot.Any;

    [SerializeField, Tooltip("要求 InputSemantic 为 Directional。")]
    bool requireDirectional;

    [Header("Combat Context")]
    [SerializeField, Tooltip("要求接地。")]
    bool requireGrounded;

    [SerializeField, Tooltip("要求滞空。")]
    bool requireAirborne;

    [SerializeField, Tooltip("要求移动方向（None 表示不限制）。")]
    MoveDirection8 requiredMoveDirection = MoveDirection8.None;

    [SerializeField, Tooltip("数值越小越先匹配。")]
    int priority;

    public SkillGroupDefinition TargetGroup => targetGroup;
    public SkillEntrySlot RequiredSlot => requiredSlot;
    public bool RequireDirectional => requireDirectional;
    public bool RequireGrounded => requireGrounded;
    public bool RequireAirborne => requireAirborne;
    public MoveDirection8 RequiredMoveDirection => requiredMoveDirection;
    public int Priority => priority;

    public bool Matches(SkillEntrySlot slot, InputSemanticType semantic, in CombatContextSnapshot ctx)
    {
        if (targetGroup == null)
        {
            return false;
        }

        if (requiredSlot != SkillEntrySlot.Any && requiredSlot != slot)
        {
            return false;
        }

        if (requireDirectional
            && semantic != InputSemanticType.Directional
            && semantic != InputSemanticType.Tap
            && semantic != InputSemanticType.None)
        {
            return false;
        }

        if (requireGrounded && ctx.IsAirborne)
        {
            return false;
        }

        if (requireAirborne && !ctx.IsAirborne)
        {
            return false;
        }

        if (requiredMoveDirection != MoveDirection8.None
            && ctx.MoveDirection != requiredMoveDirection)
        {
            return false;
        }

        return true;
    }
}

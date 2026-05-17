using UnityEngine;

/// <summary>
/// 方向化路由集 — 一个"入口语义"对应多条方向化子 Route。
///
/// ═══ 设计要点 ═══
///   · 方向（Forward / Backward / Left / Right / 4 斜向）来自 InputContext.MoveBuffered，
///     由 InputChordResolver 解析；本 SO 本身不读输入。
///   · 与 Combo / Charge 正交：方向化也可以与 Combo 嵌套（外层 Combo，内层 Directional）。
///   · 派生灵感：8 方向闪避、锁定姿态闪避、武器姿态变招。
///
/// ═══ 与「Modifier 进 Intent」错误设计的边界 ═══
///   方向不进 GameplayIntent；玩家"按 Space"的 Intent 永远是 Intent_Skill_08_Space，
///   方向解析发生在 RouteResolver 之后选 child 的瞬间，因此**未来加新方向不污染 Intent 枚举**。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/SkillRoute/Route/Directional Route Set", fileName = "Route_Directional_")]
public sealed class DirectionalRouteSet : SkillRouteDefinition
{
    [Header("Directional Children (子 Route 必填，至少 Forward)")]
    [SerializeField, Tooltip("前向（W）。必填。")]
    private SkillRouteDefinition forward;

    [SerializeField, Tooltip("后向（S）。空 = 沿用 forward。")]
    private SkillRouteDefinition backward;

    [SerializeField, Tooltip("左向（A）。空 = 沿用 forward。")]
    private SkillRouteDefinition left;

    [SerializeField, Tooltip("右向（D）。空 = 沿用 forward。")]
    private SkillRouteDefinition right;

    [Header("Diagonals (可选)")]
    [SerializeField] private SkillRouteDefinition forwardLeft;
    [SerializeField] private SkillRouteDefinition forwardRight;
    [SerializeField] private SkillRouteDefinition backwardLeft;
    [SerializeField] private SkillRouteDefinition backwardRight;

    [Header("Behaviour")]
    [SerializeField, Tooltip("无方向输入时（中性）默认选 Forward。否则视作不可释放。")]
    private bool defaultToForwardWhenNeutral = true;

    public bool DefaultToForwardWhenNeutral => defaultToForwardWhenNeutral;

    public override RouteKind Kind => RouteKind.Directional;

    /// <summary>按方向选子 Route；未配置则回退到 forward。</summary>
    public SkillRouteDefinition SelectByDirection(DirectionalRouteType dir)
    {
        switch (dir)
        {
            case DirectionalRouteType.Forward:       return forward;
            case DirectionalRouteType.Backward:      return backward      != null ? backward      : forward;
            case DirectionalRouteType.Left:          return left          != null ? left          : forward;
            case DirectionalRouteType.Right:         return right         != null ? right         : forward;
            case DirectionalRouteType.ForwardLeft:   return forwardLeft   != null ? forwardLeft   : (left  != null ? left  : forward);
            case DirectionalRouteType.ForwardRight:  return forwardRight  != null ? forwardRight  : (right != null ? right : forward);
            case DirectionalRouteType.BackwardLeft:  return backwardLeft  != null ? backwardLeft  : (left  != null ? left  : forward);
            case DirectionalRouteType.BackwardRight: return backwardRight != null ? backwardRight : (right != null ? right : forward);
            default:                                  return forward;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (forward == null)
        {
            Debug.LogWarning($"[DirectionalRouteSet] '{name}' Forward 子 Route 未配置，方向化路由将不可释放。", this);
        }
    }
#endif
}

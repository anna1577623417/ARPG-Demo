using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 上下文语义组 — 将 CombatContext + Intent 映射到 SkillGroup。
///
/// 173.6：条件层（Context Routing）。"WHEN 满足条件 → 用哪个 Group"。
///   - Ability Gate 统一改用 <see cref="AbilityGateRuleSO"/> 列表（与 Route 同语言）
///   - bool requireGrounded / requireAirborne / requireDirectional 旧字段已迁移
/// </summary>
[CreateAssetMenu(menuName = "GameMain/SkillRoute/Context/Skill Context Group", fileName = "ContextGroup_")]
public sealed class SkillContextGroupDefinition : ScriptableObject
{
    [SerializeField, Tooltip("命中后路由到的 SkillGroup。")]
    SkillGroupDefinition targetGroup;

    [SerializeField, Tooltip("限定入口槽位；Any 表示任意槽位。")]
    SkillEntrySlot requiredSlot = SkillEntrySlot.Any;

    [SerializeField, Tooltip("限定输入语义（None 表示任意）。Directional 仅在方向键组合时命中。")]
    InputSemanticType requiredSemantic = InputSemanticType.None;

    [SerializeField, Tooltip("要求当前移动方向（None 表示任意）。")]
    MoveDirection8 requiredMoveDirection = MoveDirection8.None;

    [SerializeField, Tooltip("准入规则数组（173.6 与 Route 共享 SO 语言）；全部 Pass 才匹配。\n" +
                              "Grounded/Airborne/HasTarget 等用 GateRule 资产，不再使用散列 bool。")]
    AbilityGateRuleSO[] abilityGateRules;

    [SerializeField, Tooltip("237 L1：方向意图时间窗。为空则回落 LocomotionTuning.DirectionalTimingProfile，再回落代码默认 Pre=0.10。")]
    DirectionalTimingProfileSO timingProfile;

    [SerializeField, Tooltip("数值越小越先匹配。")]
    int priority;

    // ─── 173.6 Legacy 字段（OnValidate 一次性迁移到 abilityGateRules）───
#if UNITY_EDITOR
    [FormerlySerializedAs("requireDirectional")] [HideInInspector]
    [SerializeField] bool _legacyRequireDirectional;

    [FormerlySerializedAs("requireGrounded")] [HideInInspector]
    [SerializeField] bool _legacyRequireGrounded;

    [FormerlySerializedAs("requireAirborne")] [HideInInspector]
    [SerializeField] bool _legacyRequireAirborne;

    [SerializeField, HideInInspector] bool _1736GateMigrated;

    const string GuidRequireGrounded  = "1736aabb01000001000000000000abcd";
    const string GuidRequireAirborne  = "1736aabb02000002000000000000abcd";
#endif

    public SkillGroupDefinition TargetGroup => targetGroup;
    public SkillEntrySlot RequiredSlot => requiredSlot;
    public InputSemanticType RequiredSemantic => requiredSemantic;
    public MoveDirection8 RequiredMoveDirection => requiredMoveDirection;
    public AbilityGateRuleSO[] AbilityGateRules => abilityGateRules;
    public DirectionalTimingProfileSO TimingProfile => timingProfile;
    public int Priority => priority;

    public bool Matches(SkillEntrySlot slot, InputSemanticType semantic, in CombatContextSnapshot ctx)
    {
        if (targetGroup == null) return false;

        if (requiredSlot != SkillEntrySlot.Any && requiredSlot != slot)
            return false;

        if (requiredSemantic != InputSemanticType.None)
        {
            // Directional 接受 Tap 兼容（与旧 requireDirectional 一致）
            var ok = semantic == requiredSemantic
                     || (requiredSemantic == InputSemanticType.Directional
                         && (semantic == InputSemanticType.Tap || semantic == InputSemanticType.None));
            if (!ok) return false;
        }

        if (requiredMoveDirection != MoveDirection8.None && ctx.MoveDirection != requiredMoveDirection)
            return false;

        if (abilityGateRules != null)
        {
            for (var i = 0; i < abilityGateRules.Length; i++)
            {
                var rule = abilityGateRules[i];
                if (rule != null && !rule.Pass(in ctx)) return false;
            }
        }

        return true;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (_1736GateMigrated) return;
        if (!_legacyRequireDirectional && !_legacyRequireGrounded && !_legacyRequireAirborne)
        {
            _1736GateMigrated = true;
            return;
        }

        var list = new List<AbilityGateRuleSO>();
        if (abilityGateRules != null)
        {
            for (var i = 0; i < abilityGateRules.Length; i++)
            {
                if (abilityGateRules[i] != null) list.Add(abilityGateRules[i]);
            }
        }

        if (_legacyRequireDirectional && requiredSemantic == InputSemanticType.None)
        {
            requiredSemantic = InputSemanticType.Directional;
        }

        if (_legacyRequireGrounded)
        {
            var rule = LoadStandardGateRule(GuidRequireGrounded);
            if (rule != null && !list.Contains(rule)) list.Add(rule);
        }

        if (_legacyRequireAirborne)
        {
            var rule = LoadStandardGateRule(GuidRequireAirborne);
            if (rule != null && !list.Contains(rule)) list.Add(rule);
        }

        abilityGateRules = list.ToArray();
        _legacyRequireDirectional = false;
        _legacyRequireGrounded = false;
        _legacyRequireAirborne = false;
        _1736GateMigrated = true;
        EditorUtility.SetDirty(this);
    }

    static AbilityGateRuleSO LoadStandardGateRule(string guid)
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.LoadAssetAtPath<AbilityGateRuleSO>(path);
    }
#endif
}

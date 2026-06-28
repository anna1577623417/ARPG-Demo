#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 173.6 — ContextGroup Inspector：Output 预览 / Intent Match / Ability Gate / Routing 四分区 + Quick Add。
/// </summary>
[CustomEditor(typeof(SkillContextGroupDefinition))]
public sealed class SkillContextGroupDefinitionEditor : Editor
{
    const string GuidRequireGrounded = "1736aabb01000001000000000000abcd";
    const string GuidRequireAirborne = "1736aabb02000002000000000000abcd";
    const string GuidGroundOrAir     = "1736aabb03000003000000000000abcd";

    SerializedProperty m_targetGroup;
    SerializedProperty m_requiredSlot;
    SerializedProperty m_requiredSemantic;
    SerializedProperty m_requiredMoveDirection;
    SerializedProperty m_abilityGateRules;
    SerializedProperty m_priority;

    void OnEnable()
    {
        m_targetGroup           = serializedObject.FindProperty("targetGroup");
        m_requiredSlot          = serializedObject.FindProperty("requiredSlot");
        m_requiredSemantic      = serializedObject.FindProperty("requiredSemantic");
        m_requiredMoveDirection = serializedObject.FindProperty("requiredMoveDirection");
        m_abilityGateRules      = serializedObject.FindProperty("abilityGateRules");
        m_priority              = serializedObject.FindProperty("priority");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawOutputSection();
        EditorGUILayout.Space(4f);
        DrawIntentSection();
        EditorGUILayout.Space(4f);
        DrawAbilityGateSection();
        EditorGUILayout.Space(4f);
        DrawRoutingSection();

        serializedObject.ApplyModifiedProperties();
    }

    // ─── Output ───
    void DrawOutputSection()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Output (路由终点)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_targetGroup, new GUIContent("Target Group"));

            var def = (SkillContextGroupDefinition)target;
            var preview = BuildPreviewSentence(def);
            var type = def.TargetGroup != null ? MessageType.Info : MessageType.Warning;
            EditorGUILayout.HelpBox(preview, type);
        }
    }

    static string BuildPreviewSentence(SkillContextGroupDefinition def)
    {
        if (def.TargetGroup == null) return "⚠ 未配置 Target Group — 此 ContextGroup 永不命中。";

        var slot = def.RequiredSlot == SkillEntrySlot.Any ? "任意槽" : def.RequiredSlot.ToString();
        var semantic = def.RequiredSemantic == InputSemanticType.None ? "任意语义" : def.RequiredSemantic.ToString();
        var dir = def.RequiredMoveDirection == MoveDirection8.None ? "任意方向" : def.RequiredMoveDirection.ToString();

        var gateCount = def.AbilityGateRules != null ? def.AbilityGateRules.Length : 0;
        var gate = gateCount == 0 ? "无 Gate" : $"{gateCount} 条 Gate";

        return $"{slot} + {semantic} + {dir} + {gate}  →  {def.TargetGroup.name}  (priority={def.Priority})";
    }

    // ─── Intent Match ───
    void DrawIntentSection()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Intent Match (输入匹配)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_requiredSlot, new GUIContent("Required Slot"));
            EditorGUILayout.PropertyField(m_requiredSemantic, new GUIContent("Required Semantic"));
            EditorGUILayout.PropertyField(m_requiredMoveDirection, new GUIContent("Required Move Dir"));
        }
    }

    // ─── Ability Gate ───
    void DrawAbilityGateSection()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Ability Gate (与 Route 共享语言)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_abilityGateRules, new GUIContent("Rules"), true);

            EditorGUILayout.Space(2f);
            DrawQuickAddButtons();

            EditorGUILayout.HelpBox(
                "全部 Rule.Pass(ctx) == true 才算命中此 ContextGroup。\n" +
                "示例：Grounded 限定地面；Airborne 限定空中；GroundOrAir 不限。",
                MessageType.None);
        }
    }

    void DrawQuickAddButtons()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel("Quick Add");
            if (GUILayout.Button("+ Grounded", EditorStyles.miniButton))
                TryAppendRule(GuidRequireGrounded);
            if (GUILayout.Button("+ Airborne", EditorStyles.miniButton))
                TryAppendRule(GuidRequireAirborne);
            if (GUILayout.Button("+ Ground/Air", EditorStyles.miniButton))
                TryAppendRule(GuidGroundOrAir);
        }
    }

    void TryAppendRule(string guid)
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path))
        {
            EditorUtility.DisplayDialog(
                "标准 GateRule 未找到",
                $"未在工程中找到 GUID={guid} 的 AbilityGateRule 资产。\n" +
                "请确认 4_Data/AbilityGateRules/ 已存在标准资产。",
                "OK");
            return;
        }

        var rule = AssetDatabase.LoadAssetAtPath<AbilityGateRuleSO>(path);
        if (rule == null) return;

        // 去重：已存在则不重复添加
        for (var i = 0; i < m_abilityGateRules.arraySize; i++)
        {
            if (m_abilityGateRules.GetArrayElementAtIndex(i).objectReferenceValue == rule) return;
        }

        m_abilityGateRules.arraySize++;
        var newIdx = m_abilityGateRules.arraySize - 1;
        m_abilityGateRules.GetArrayElementAtIndex(newIdx).objectReferenceValue = rule;
    }

    // ─── Routing ───
    void DrawRoutingSection()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Routing (优先级)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_priority, new GUIContent("Priority", "数值越小越先匹配"));
        }
    }
}
#endif

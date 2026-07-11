#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 188.3 W17 — CombatObjectDefinitionSO Inspector：按 enum 选择条件显示对应字段。
/// <para>避免策划在 Static Movement 看到 Speed/MaxDistance 等无关字段。</para>
/// </summary>
[CustomEditor(typeof(CombatObjectDefinitionSO))]
public sealed class CombatObjectDefinitionSOInspector : Editor
{
    SerializedProperty m_Id;
    SerializedProperty m_DisplayName;
    SerializedProperty m_Description;
    SerializedProperty m_Shape;
    SerializedProperty m_Movement;
    SerializedProperty m_Damage;
    SerializedProperty m_Lifecycle;
    SerializedProperty m_TargetFilter;
    SerializedProperty m_SpawnSource;
    SerializedProperty m_LocalOffset;
    SerializedProperty m_LocalEulerOffset;
    SerializedProperty m_QueryLayerMask;

    void OnEnable()
    {
        m_Id = serializedObject.FindProperty("Id");
        m_DisplayName = serializedObject.FindProperty("DisplayName");
        m_Description = serializedObject.FindProperty("Description");
        m_Shape = serializedObject.FindProperty("Shape");
        m_Movement = serializedObject.FindProperty("Movement");
        m_Damage = serializedObject.FindProperty("Damage");
        m_Lifecycle = serializedObject.FindProperty("Lifecycle");
        m_TargetFilter = serializedObject.FindProperty("TargetFilter");
        m_SpawnSource = serializedObject.FindProperty("SpawnSource");
        m_LocalOffset = serializedObject.FindProperty("LocalOffset");
        m_LocalEulerOffset = serializedObject.FindProperty("LocalEulerOffset");
        m_QueryLayerMask = serializedObject.FindProperty("QueryLayerMask");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Identity
        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(m_Id);
        EditorGUILayout.PropertyField(m_DisplayName);
        EditorGUILayout.PropertyField(m_Description);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Shape", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(m_Shape);
        if (m_Shape.objectReferenceValue is HitShapeSO shape)
        {
            EditorGUILayout.HelpBox($"Shape 类型：{shape.GetType().Name}", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("Shape 未设置 → 不会命中任何目标。", MessageType.Warning);
        }

        EditorGUILayout.Space();
        DrawMovement();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Damage", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(m_Damage);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Lifecycle", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(m_Lifecycle);
        DrawLifecycleHint();
        DrawLifecycleActions();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Target Filter", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(m_TargetFilter, includeChildren: true);
        var kind = (TargetFilterKind)m_TargetFilter.FindPropertyRelative(nameof(TargetFilterParams.Kind)).enumValueIndex;
        switch (kind)
        {
            case TargetFilterKind.AnyExceptSelf:
                EditorGUILayout.HelpBox("命中任意 Entity（除施法者）。静态 Player 互殴靶适用。", MessageType.None);
                break;
            case TargetFilterKind.HostileOnly:
                EditorGUILayout.HelpBox("仅命中阵营敌对（Faction 轨或 TeamId 不同）。", MessageType.None);
                break;
            case TargetFilterKind.FriendlyOnly:
                EditorGUILayout.HelpBox("仅命中友方（治疗/增益）。", MessageType.None);
                break;
            case TargetFilterKind.SelfOnly:
                EditorGUILayout.HelpBox("仅命中施法者自己。", MessageType.None);
                break;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Spawn", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(m_SpawnSource);
        EditorGUILayout.PropertyField(m_LocalOffset);
        EditorGUILayout.PropertyField(m_LocalEulerOffset);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Physics Query", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(m_QueryLayerMask);

        EditorGUILayout.Space();
        DrawValidationStatus();

        serializedObject.ApplyModifiedProperties();
    }

    void DrawMovement()
    {
        EditorGUILayout.LabelField("Movement", EditorStyles.boldLabel);
        var kindProp = m_Movement.FindPropertyRelative("Kind");
        EditorGUILayout.PropertyField(kindProp);
        var kind = (MovementKind)kindProp.enumValueIndex;

        // 按 Kind 条件显示
        switch (kind)
        {
            case MovementKind.Static:
                EditorGUILayout.HelpBox("Static：CombatObject 不动；近战 / 火焰地板用。", MessageType.None);
                break;
            case MovementKind.Linear:
                EditorGUILayout.PropertyField(m_Movement.FindPropertyRelative("Speed"));
                EditorGUILayout.PropertyField(m_Movement.FindPropertyRelative("MaxDistance"));
                EditorGUILayout.HelpBox("Linear：直线匀速；飞弹 / 滑刀用。沿 Spawn 朝向 forward 方向。", MessageType.None);
                break;
            case MovementKind.Curve:
                EditorGUILayout.PropertyField(m_Movement.FindPropertyRelative("LocalOffsetXOverTime"));
                EditorGUILayout.PropertyField(m_Movement.FindPropertyRelative("LocalOffsetYOverTime"));
                EditorGUILayout.PropertyField(m_Movement.FindPropertyRelative("LocalOffsetZOverTime"));
                EditorGUILayout.HelpBox("Curve：三轴 AnimationCurve 控制局部偏移；弧线 / S 形用。", MessageType.None);
                break;
            case MovementKind.Expand:
                EditorGUILayout.PropertyField(m_Movement.FindPropertyRelative("StartRadius"));
                EditorGUILayout.PropertyField(m_Movement.FindPropertyRelative("EndRadius"));
                EditorGUILayout.PropertyField(m_Movement.FindPropertyRelative("ExpandCurve"));
                EditorGUILayout.HelpBox("Expand：判定半径随 Lifecycle.Duration 变化；冲击波用。注意 Shape 字段被忽略，用 Sphere 查询。", MessageType.Info);
                break;
            case MovementKind.Homing:
                EditorGUILayout.PropertyField(m_Movement.FindPropertyRelative("Speed"));
                EditorGUILayout.PropertyField(m_Movement.FindPropertyRelative("MaxDistance"));
                EditorGUILayout.PropertyField(m_Movement.FindPropertyRelative("TurnRateDegPerSec"));
                EditorGUILayout.HelpBox("Homing：跟踪导弹；需调用方在 Spawn 后设置 HomingTarget。", MessageType.Info);
                break;
        }
    }

    void DrawLifecycleHint()
    {
        var lifeProp = m_Lifecycle;
        var duration = lifeProp.FindPropertyRelative("Duration").floatValue;
        var tick = lifeProp.FindPropertyRelative("TickInterval").floatValue;
        var maxHits = lifeProp.FindPropertyRelative("MaxHitsPerTarget").intValue;
        var maxTargets = lifeProp.FindPropertyRelative("MaxTargets").intValue;

        if (maxHits < 1 || maxTargets < 1)
        {
            EditorGUILayout.HelpBox(
                "MaxHitsPerTarget / MaxTargets 不能为 0 → Spawn 将被拒绝。",
                MessageType.Error);
        }

        string mode;
        if (duration <= 0f) mode = "单帧瞬时";
        else if (tick > 0f) mode = $"DOT：{duration:F1}s 内每 {tick:F2}s 一次（最多每目标 {maxHits} 次）";
        else if (maxHits == 1) mode = $"单次命中（持续 {duration:F1}s）";
        else mode = $"多段命中（{duration:F1}s 内最多 {maxHits} 次/目标）";

        EditorGUILayout.HelpBox(mode, MessageType.None);
    }

    void DrawLifecycleActions()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("填充 DefaultMeleeOneShot"))
            {
                Undo.RecordObject(target, "Fill DefaultMeleeOneShot");
                var def = (CombatObjectDefinitionSO)target;
                def.Lifecycle = LifecycleParams.DefaultMeleeOneShot;
                EditorUtility.SetDirty(def);
            }
        }
    }

    void DrawValidationStatus()
    {
        var def = (CombatObjectDefinitionSO)target;
        if (def.IsValid(out var reason))
        {
            EditorGUILayout.HelpBox("✓ 配置有效，可在 CombatTrack 中使用。", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox($"✗ 配置无效：{reason}", MessageType.Error);
        }
    }
}
#endif

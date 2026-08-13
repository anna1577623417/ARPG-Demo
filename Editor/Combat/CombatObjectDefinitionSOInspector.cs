#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 223.6 M2：Schema Projection 驱动的 CombatObject 作者入口。
/// 这里不重新解释 Archetype；所有区块由 Central Schema Projection 决定。
/// </summary>
[CustomEditor(typeof(CombatObjectDefinitionSO))]
public sealed class CombatObjectDefinitionSOInspector : Editor
{
    SerializedProperty _id;
    SerializedProperty _displayName;
    SerializedProperty _description;
    SerializedProperty _archetype;
    SerializedProperty _schemaVersion;
    SerializedProperty _migrationState;
    SerializedProperty _revision;
    SerializedProperty _shapePreset;
    SerializedProperty _actionContactAuthoring;
    SerializedProperty _attackProfile;
    SerializedProperty _queryPolicy;
    SerializedProperty _hitPolicy;
    SerializedProperty _spawnedData;
    SerializedProperty _shape;
    SerializedProperty _movement;
    SerializedProperty _damage;
    SerializedProperty _lifecycle;
    SerializedProperty _targetFilter;
    SerializedProperty _spawnSource;
    SerializedProperty _localOffset;
    SerializedProperty _localEulerOffset;
    SerializedProperty _queryLayerMask;

    void OnEnable()
    {
        _id = serializedObject.FindProperty(nameof(CombatObjectDefinitionSO.Id));
        _displayName = serializedObject.FindProperty(nameof(CombatObjectDefinitionSO.DisplayName));
        _description = serializedObject.FindProperty(nameof(CombatObjectDefinitionSO.Description));
        _archetype = serializedObject.FindProperty(nameof(CombatObjectDefinitionSO.Archetype));
        _schemaVersion = serializedObject.FindProperty(nameof(CombatObjectDefinitionSO.SchemaVersion));
        _migrationState = serializedObject.FindProperty(nameof(CombatObjectDefinitionSO.MigrationState));
        _revision = serializedObject.FindProperty(nameof(CombatObjectDefinitionSO.DefinitionRevision));
        _shapePreset = serializedObject.FindProperty(nameof(CombatObjectDefinitionSO.ShapePreset));
        _actionContactAuthoring = serializedObject.FindProperty(nameof(CombatObjectDefinitionSO.ActionContactAuthoring));
        _attackProfile = serializedObject.FindProperty(nameof(CombatObjectDefinitionSO.AttackProfile));
        _queryPolicy = serializedObject.FindProperty(nameof(CombatObjectDefinitionSO.QueryPolicy));
        _hitPolicy = serializedObject.FindProperty(nameof(CombatObjectDefinitionSO.HitPolicy));
        _spawnedData = serializedObject.FindProperty(nameof(CombatObjectDefinitionSO.SpawnedData));
        _shape = serializedObject.FindProperty(nameof(CombatObjectDefinitionSO.Shape));
        _movement = serializedObject.FindProperty(nameof(CombatObjectDefinitionSO.Movement));
        _damage = serializedObject.FindProperty(nameof(CombatObjectDefinitionSO.Damage));
        _lifecycle = serializedObject.FindProperty(nameof(CombatObjectDefinitionSO.Lifecycle));
        _targetFilter = serializedObject.FindProperty(nameof(CombatObjectDefinitionSO.TargetFilter));
        _spawnSource = serializedObject.FindProperty(nameof(CombatObjectDefinitionSO.SpawnSource));
        _localOffset = serializedObject.FindProperty(nameof(CombatObjectDefinitionSO.LocalOffset));
        _localEulerOffset = serializedObject.FindProperty(nameof(CombatObjectDefinitionSO.LocalEulerOffset));
        _queryLayerMask = serializedObject.FindProperty(nameof(CombatObjectDefinitionSO.QueryLayerMask));
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var definition = (CombatObjectDefinitionSO)target;
        var projection = CombatObjectInspectorProjectionResolver.Resolve(definition);

        DrawIdentity(projection);
        if (projection.IsLegacy)
        {
            DrawLegacyGate();
            DrawLegacySnapshot();
            DrawValidation(definition);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        if (projection.Allows(CombatFeatureBlock.AttackProfile))
        {
            DrawPayload(projection);
        }

        if (projection.Allows(CombatFeatureBlock.ActionWindow))
        {
            DrawActionContact();
        }

        if (definition.SchemaVersion >= CombatObjectSchemaVersion.ArchetypeV2
            && definition.Archetype != CombatObjectArchetype.ActionContact)
        {
            DrawSpawnedV2(projection);
        }
        else if (definition.Archetype != CombatObjectArchetype.ActionContact)
        {
            DrawLegacyGate();
            DrawLegacySnapshot();
        }

        DrawValidation(definition);
        serializedObject.ApplyModifiedProperties();
    }

    void DrawIdentity(CombatObjectInspectorProjection projection)
    {
        EditorGUILayout.LabelField("Identity / Archetype / Schema", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_id);
        EditorGUILayout.PropertyField(_displayName);
        EditorGUILayout.PropertyField(_description);
        EditorGUILayout.PropertyField(_archetype);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(_schemaVersion);
            EditorGUILayout.PropertyField(_migrationState);
        }

        EditorGUILayout.PropertyField(_revision);
        EditorGUILayout.HelpBox(
            $"Execution Model: {projection.Schema.ExecutionModel}\n" +
            $"Required Features: {projection.Schema.RequiredFeatures}\n" +
            $"Allowed Use Sites: " +
            $"{UseSiteSummary(projection.Schema)}",
            projection.IsLegacy ? MessageType.Warning : MessageType.Info);
    }

    void DrawPayload(CombatObjectInspectorProjection projection)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Payload / Query / Hit", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_attackProfile, includeChildren: true);
        EditorGUILayout.PropertyField(_queryPolicy, includeChildren: true);
        EditorGUILayout.HelpBox(
            "目标语义唯一入口：QueryPolicy.Target（TargetProfile）。TargetFilter 已淘汰，不可再作为写入入口。",
            MessageType.Info);
        EditorGUILayout.PropertyField(_hitPolicy, includeChildren: true);
    }

    void DrawActionContact()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Action Contact Definition", EditorStyles.boldLabel);
        var definition = (CombatObjectDefinitionSO)target;
        ContactAnchorAuthoringDrawer.Draw(definition);
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(_shapePreset);
        EditorGUILayout.HelpBox(
            "Geometry Bundle：ShapePreset 只提供 ShapeMode/Geometry/Layout；" +
            "Binding/Origin/LocalPose 只写在本 CO。Preset Default Placement 与 Event Override 为 Legacy 只读。",
            MessageType.Info);
        ContactAuthoringSharedImpactUI.Draw(definition);

        if (_actionContactAuthoring != null)
        {
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(_actionContactAuthoring, new GUIContent("Authoring Snapshot"), true);
            }
        }
    }

    void DrawSpawnedV2(CombatObjectInspectorProjection projection)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Spawned V2 Feature Data", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_spawnedData, includeChildren: true);
        if (!projection.Allows(CombatFeatureBlock.Motion))
        {
            EditorGUILayout.HelpBox(
                "当前 Archetype Schema 不允许 Motion；请通过 Schema 投影检查配置。",
                MessageType.Warning);
        }
    }

    void DrawLegacyGate()
    {
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "此 Definition 仍是 Legacy/未分类资产。Legacy 数据只用于迁移对照，" +
            "不会作为新版生产入口；请创建迁移副本或使用受控新建入口。",
            MessageType.Error);
    }

    void DrawLegacySnapshot()
    {
        EditorGUILayout.LabelField("Legacy Snapshot (Read Only)", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(_shape);
            EditorGUILayout.PropertyField(_movement, includeChildren: true);
            EditorGUILayout.PropertyField(_damage);
            EditorGUILayout.PropertyField(_lifecycle, includeChildren: true);
            EditorGUILayout.PropertyField(_targetFilter, includeChildren: true);
            EditorGUILayout.PropertyField(_spawnSource);
            EditorGUILayout.PropertyField(_localOffset);
            EditorGUILayout.PropertyField(_localEulerOffset);
            EditorGUILayout.PropertyField(_queryLayerMask);
        }
    }

    void DrawValidation(CombatObjectDefinitionSO definition)
    {
        EditorGUILayout.Space();
        var validation = CombatObjectDefinitionValidator.Validate(
            definition,
            CombatDefinitionUseSite.Intrinsic);
        if (validation.IsValid)
        {
            EditorGUILayout.HelpBox("Definition Intrinsic Validation: PASS", MessageType.Info);
            return;
        }

        for (var i = 0; i < validation.Issues.Count; i++)
        {
            var issue = validation.Issues[i];
            EditorGUILayout.HelpBox(
                $"{issue.Code} [{issue.FieldPath}] {issue.Message}",
                issue.Severity >= CombatValidationSeverity.Error
                    ? MessageType.Error
                    : MessageType.Warning);
        }
    }

    static string UseSiteSummary(CombatArchetypeSchema schema)
    {
        var result = "Intrinsic";
        if (schema.AllowsContactEvent) result += ", ContactEvent";
        if (schema.AllowsSpawnRequest) result += ", SpawnRequest";
        if (schema.AllowsTerminationChild) result += ", TerminationChild";
        return result;
    }
}
#endif

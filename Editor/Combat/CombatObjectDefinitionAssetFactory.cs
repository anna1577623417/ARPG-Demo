using System.IO;
using UnityEditor;
using UnityEngine;

public static class CombatObjectDefinitionAssetFactory
{
    [MenuItem("Assets/Create/GameMain/Combat/Action Contact Definition", priority = 200)]
    public static void CreateActionContact()
    {
        var definition = ScriptableObject.CreateInstance<CombatObjectDefinitionSO>();
        definition.name = "CombatObject_ActionContact";
        definition.Id = definition.name;
        definition.DisplayName = "New Action Contact";
        definition.Archetype = CombatObjectArchetype.ActionContact;
        definition.SchemaVersion = CombatObjectSchemaVersion.ArchetypeV2;
        definition.MigrationState = CombatObjectMigrationState.Classified;
        definition.DefinitionRevision = 1;
        definition.AttackProfile = CombatAttackProfile.Default;
        definition.QueryPolicy = ContactQueryPolicy.Default;
        definition.HitPolicy = HitPolicyParams.Default;
        definition.ActionContactAuthoring = ActionContactAuthoringData.CreateNewV1();
        CreateAsset(definition);
    }

    [MenuItem("Assets/Create/GameMain/Combat/Projectile Definition", priority = 201)]
    public static void CreateProjectile()
    {
        var definition = ScriptableObject.CreateInstance<CombatObjectDefinitionSO>();
        definition.name = "CombatObject_Projectile";
        definition.Id = definition.name;
        definition.DisplayName = "New Projectile";
        definition.Archetype = CombatObjectArchetype.Projectile;
        definition.SchemaVersion = CombatObjectSchemaVersion.ArchetypeV2;
        definition.MigrationState = CombatObjectMigrationState.Classified;
        definition.DefinitionRevision = 1;

        var data = SpawnedCombatAuthoringData.Default;
        data.Motion = MovementParams.DefaultLinear();
        data.Outcome = CombatOutcomeProfile.FromReaction(HitReaction.Default);
        definition.SpawnedData = data;
        CreateAsset(definition);
    }

    static void CreateAsset(CombatObjectDefinitionSO definition)
    {
        var folder = GetSelectedFolder();
        var path = AssetDatabase.GenerateUniqueAssetPath(
            $"{folder}/{definition.name}.asset");
        AssetDatabase.CreateAsset(definition, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = definition;
        EditorGUIUtility.PingObject(definition);
    }

    static string GetSelectedFolder()
    {
        var selected = Selection.activeObject;
        var selectedPath = selected != null
            ? AssetDatabase.GetAssetPath(selected)
            : string.Empty;
        if (!string.IsNullOrEmpty(selectedPath))
        {
            if (AssetDatabase.IsValidFolder(selectedPath))
            {
                return selectedPath;
            }

            var directory = Path.GetDirectoryName(selectedPath);
            if (!string.IsNullOrEmpty(directory))
            {
                return directory.Replace('\\', '/');
            }
        }

        return "Assets";
    }
}

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 136.3+ — MotionSpace 枚举迁移与 C1 四向闪避默认空间。
/// </summary>
public static class MotionSpaceMigration
{
    const string GroupPath =
        "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/SkillUnit/Group_Standing_Dodge.asset";

    [MenuItem("Tools/Motion/136.3 Migrate Legacy DisplacementFrame → MotionSpace")]
    public static void MigrateLegacyDisplacementFrame()
    {
        var guids = AssetDatabase.FindAssets("t:MotionProfileSO");
        var updated = 0;
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var text = File.ReadAllText(path);
            if (!text.Contains("DisplacementFrame") && !text.Contains("MovementIntent"))
            {
                continue;
            }

            var profile = AssetDatabase.LoadAssetAtPath<MotionProfileSO>(path);
            if (profile == null)
            {
                continue;
            }

            var so = new SerializedObject(profile);
            var prop = so.FindProperty("MotionSpace");
            if (prop == null)
            {
                continue;
            }

            // 旧 0=MovementIntent → CameraForward(1)；旧 1=CharacterForward → 0
            if (text.Contains("DisplacementFrame: 0") || text.Contains("DisplacementFrame: 0\n"))
            {
                prop.enumValueIndex = (int)MotionSpace.CameraForward;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(profile);
                updated++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Motion][136.3] MotionSpace migration updated={updated} scanned={guids.Length}");
    }

    [MenuItem("Tools/Motion/136.3 Wire C1 Dodge MotionSpace = CameraForward")]
    public static void WireC1DodgeMotionSpace()
    {
        var group = AssetDatabase.LoadAssetAtPath<SkillGroupDefinition>(GroupPath);
        if (group == null)
        {
            Debug.LogError($"[Motion][136.3] Group 缺失: {GroupPath}");
            return;
        }

        var count = 0;
        count += WireProfile(group.Forward);
        count += WireProfile(group.Backward);
        count += WireProfile(group.Left);
        count += WireProfile(group.Right);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Motion][136.3] C1 dodge MotionSpace=CameraForward profiles={count}");
    }

    static int WireProfile(SkillRouteDefinition route)
    {
        var profile = route?.FirstStage()?.Action?.MotionProfile;
        if (profile == null)
        {
            return 0;
        }

        var so = new SerializedObject(profile);
        so.FindProperty("MotionSpace").enumValueIndex = (int)MotionSpace.CameraForward;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
        return 1;
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 将 legacyYPolicy 烘焙为三权字段并标记 yAxisV2Configured。
/// </summary>
public static class MotionYAxisV2Applicator
{
    [MenuItem("Tools/Motion/Y Axis V2/Migrate All MotionProfiles")]
    public static void MigrateAllMenu()
    {
        var count = MigrateAllInProject();
        Debug.Log($"[MotionYAxisV2] migrated {count} MotionProfile assets.");
    }

    public static void ApplyLegacyToV2(MotionProfileSO profile)
    {
        if (profile == null)
        {
            return;
        }

#pragma warning disable 0618
        var legacy = profile.GetEffectiveYPolicy();
        var config = MotionYAxisLegacyMapping.FromLegacy(legacy);
#pragma warning restore 0618

        profile.YMotion = config.YMotion;
        profile.Gravity = config.Gravity;
        profile.GroundConstraint = config.GroundConstraint;
        profile.SetYAxisV2Configured(true);
        EditorUtility.SetDirty(profile);
    }

    public static int MigrateAllInProject()
    {
        var guids = AssetDatabase.FindAssets("t:MotionProfileSO");
        var count = 0;
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var profile = AssetDatabase.LoadAssetAtPath<MotionProfileSO>(path);
            if (profile == null)
            {
                continue;
            }

            ApplyLegacyToV2(profile);
            count++;
        }

        AssetDatabase.SaveAssets();
        return count;
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 为新动作一键生成_motionProfile占位，并按 Action 已有 MainClip 墙钟填入参考字段。
/// Why: ActionDataSO 已剥离 burst；行程/塑形只在 MotionProfile 上调。
/// </summary>
public static class MotionMigrationTool
{
    public static void CreateMissingProfiles()
    {
        var guids = AssetDatabase.FindAssets("t:ActionDataSO");
        var migrated = 0;

        for (var i = 0; i < guids.Length; i++)
        {
            var actionPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            var action = AssetDatabase.LoadAssetAtPath<ActionDataSO>(actionPath);
            if (action == null || action.MotionProfile != null)
            {
                continue;
            }

            var profile = ScriptableObject.CreateInstance<MotionProfileSO>();
            ApplyBaselineFromActionTiming(action, profile);
            profile.AnimSpeedMode = AnimSpeedMode.Constant;
            profile.SpeedOverTime = AnimationCurve.Constant(0f, 1f, 1f);

            var profilePath = actionPath.Replace(".asset", "_MotionProfile.asset");
            AssetDatabase.CreateAsset(profile, profilePath);

            action.MotionProfile = profile;
            EditorUtility.SetDirty(action);
            migrated++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Motion Migration] created MotionProfile assets count={migrated}");
    }

    public static void RefreshExistingProfileBaselinesFromAction()
    {
        var guids = AssetDatabase.FindAssets("t:ActionDataSO");
        var updated = 0;

        for (var i = 0; i < guids.Length; i++)
        {
            var actionPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            var action = AssetDatabase.LoadAssetAtPath<ActionDataSO>(actionPath);
            if (action == null || action.MotionProfile == null)
            {
                continue;
            }

            ApplyBaselineFromActionTiming(action, action.MotionProfile);
            EditorUtility.SetDirty(action.MotionProfile);
            EditorUtility.SetDirty(action);
            updated++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Motion Migration] refreshed MotionProfile baseline from Action clip count={updated}");
    }

    static void ApplyBaselineFromActionTiming(ActionDataSO action, MotionProfileSO profile)
    {
        if (!profile.UsesAxisCurves)
        {
            profile.ApplyDefaultZeroAxisDisplacement();
        }
    }
}
#endif

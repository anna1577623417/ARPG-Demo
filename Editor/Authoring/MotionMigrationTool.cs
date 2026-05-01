#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 为新动作一键生成_motionProfile占位，并按 Action 已有 MainClip 墙钟填入参考字段。
/// Why: ActionDataSO 已剥离 burst；行程/塑形只在 MotionProfile 上调。
/// </summary>
public static class MotionMigrationTool
{
    [MenuItem("Tools/Motion/Migrate ActionData → Motion Profile (missing profile only)")]
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
            profile.SpeedOverTime = AnimationCurve.Constant(0f, 1f, 1f);
            profile.MatchAnimationSpeed = true;

            var profilePath = actionPath.Replace(".asset", "_MotionProfile.asset");
            AssetDatabase.CreateAsset(profile, profilePath);

            action.MotionProfile = profile;
            EditorUtility.SetDirty(action);
            migrated++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Motion Migration] created MotionProfile assets count={migrated}");
    }

    [MenuItem("Tools/Motion/Refresh MotionProfile baseline timing from Action clip")]
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
        var wall = action.ResolveAnimWallClockSeconds();

        profile.BurstDurationSeconds = wall;
        if (profile.DisplacementCurve == null || profile.DisplacementCurve.length == 0)
        {
            profile.DisplacementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        if (profile.BaseDistance < 0.001f)
        {
            profile.BaseDistance = 4f;
        }

        profile.ReferenceSpeed = Mathf.Max(0.1f, profile.BaseDistance / Mathf.Max(wall, 0.0001f));
        profile.UsePlanarVelocityShape = false;
        profile.LegacyConstantPlanarSpeed = profile.BaseDistance / Mathf.Max(wall, 0.0001f);
    }
}
#endif

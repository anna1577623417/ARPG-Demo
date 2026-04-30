#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 批量为 ActionDataSO 生成并挂接 MotionProfile。
/// Why: 让旧资产以可回滚方式迁移到新管线，而不是一次性手改全部动作。
/// </summary>
public static class MotionMigrationTool
{
    [MenuItem("Tools/Motion/Migrate ActionData -> MotionProfile")]
    public static void MigrateAll()
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
            profile.BaseDistance = action.BurstTravelDistance > 0.01f ? action.BurstTravelDistance : Mathf.Max(0f, action.BurstPlanarSpeed * action.ResolveMotionDurationSeconds());
            profile.DisplacementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            profile.SpeedOverTime = AnimationCurve.Constant(0f, 1f, 1f);
            profile.ReferenceSpeed = Mathf.Max(0.1f, action.ResolveBurstPlanarSpeed(action.ResolveBurstMovementSeconds()));
            profile.MatchAnimationSpeed = true;

            var profilePath = actionPath.Replace(".asset", "_MotionProfile.asset");
            AssetDatabase.CreateAsset(profile, profilePath);

            action.MotionProfile = profile;
            EditorUtility.SetDirty(action);
            migrated++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Motion Migration] migrated={migrated}");
    }
}
#endif

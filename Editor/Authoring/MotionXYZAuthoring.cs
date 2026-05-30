#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>三轴 Motion：验收资产、批量迁移、Scene 轨迹预览。</summary>
public static class MotionXYZAuthoring
{
    const string VerifyRoot = "Assets/_Verification/MotionProfiles";

    [MenuItem("Tools/Motion XYZ/Migrate Selected Profile (Legacy → AxisCurves)")]
    public static void MigrateSelectedProfile()
    {
        var profile = Selection.activeObject as MotionProfileSO;
        if (profile == null)
        {
            Debug.LogWarning("[MotionXYZ] Select a MotionProfileSO asset.");
            return;
        }

        Undo.RecordObject(profile, "Migrate MotionProfile to AxisCurves");
        var r = MotionProfileLegacyMigration.TryMigrate(profile);
        AssetDatabase.SaveAssets();
        Debug.Log(r.Changed
            ? $"[MotionXYZ] Migrated {profile.name} — {r.Note}"
            : $"[MotionXYZ] No change on {profile.name} — {r.Note}");
    }

    [MenuItem("Tools/Motion XYZ/Migrate All Profiles In Project")]
    public static void MigrateAllProfiles()
    {
        if (!EditorUtility.DisplayDialog(
                "Motion XYZ 批量迁移",
                "将 Displacement/Lateral/Warp/GravityBehavior 写入 AxisCurves + YPolicy。\n" +
                "L5 删字段后须先执行本菜单。继续？",
                "迁移",
                "取消"))
        {
            return;
        }

        var n = MotionProfileLegacyMigration.MigrateAllInProject(dryRun: false, out var report);
        Debug.Log($"[MotionXYZ] Migrate All done. migrated={n}\n{report}");
    }

    [MenuItem("Tools/Motion XYZ/Report Unmigrated Profiles (Dry Run)")]
    public static void ReportUnmigrated()
    {
        MotionProfileLegacyMigration.MigrateAllInProject(dryRun: true, out var report);
        Debug.Log($"[MotionXYZ] Dry run:\n{report}");
    }

    [MenuItem("Tools/Motion XYZ/Create Verification Profiles")]
    public static void CreateVerificationProfiles()
    {
        Directory.CreateDirectory(VerifyRoot);
        CreateGalioE($"{VerifyRoot}/Motion_Galio_E.asset");
        CreateSCurve($"{VerifyRoot}/Motion_S_Curve.asset");
        CreateAirSlam($"{VerifyRoot}/Motion_AirSlam.asset");
        CreateLeapAttack($"{VerifyRoot}/Motion_LeapAttack.asset");
        CreateLauncher($"{VerifyRoot}/Motion_Launcher.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MotionXYZ] Verification profiles created under Assets/_Verification/MotionProfiles/");
    }

    [MenuItem("Tools/Motion XYZ/Preview Trajectory In Scene (Selected Profile)")]
    public static void PreviewSelectedInScene()
    {
        var profile = Selection.activeObject as MotionProfileSO;
        if (profile == null)
        {
            Debug.LogWarning("[MotionXYZ] Select a MotionProfileSO.");
            return;
        }

        MotionPathGizmoDrawer.SetPreviewProfile(profile);
    }

    static MotionProfileSO CreateProfile(string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<MotionProfileSO>(path);
        if (existing != null)
        {
            return existing;
        }

        var p = ScriptableObject.CreateInstance<MotionProfileSO>();
        AssetDatabase.CreateAsset(p, path);
        return p;
    }

    static void CreateGalioE(string path)
    {
        var p = CreateProfile(path);
        p.YPolicy = YAxisPolicy.UseGravity;
        p.AxisCurves.ZCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.2f, -1f),
            new Keyframe(0.4f, 0f),
            new Keyframe(1f, 1f));
        p.AxisCurves.ZScale = 6f;
        EditorUtility.SetDirty(p);
    }

    static void CreateSCurve(string path)
    {
        var p = CreateProfile(path);
        p.YPolicy = YAxisPolicy.UseGravity;
        p.AxisCurves.XCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.25f, 1f),
            new Keyframe(0.5f, -1f),
            new Keyframe(0.75f, 1f),
            new Keyframe(1f, 0f));
        p.AxisCurves.XScale = 2f;
        p.AxisCurves.ZCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        p.AxisCurves.ZScale = 4f;
        EditorUtility.SetDirty(p);
    }

    static void CreateAirSlam(string path)
    {
        var p = CreateProfile(path);
        p.YPolicy = YAxisPolicy.MotionControlled;
        p.AxisCurves.YCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.3f, 1f),
            new Keyframe(0.6f, 1f),
            new Keyframe(1f, -0.5f));
        p.AxisCurves.YScale = 3f;
        p.AxisCurves.ZCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        p.AxisCurves.ZScale = 3f;
        EditorUtility.SetDirty(p);
    }

    static void CreateLeapAttack(string path)
    {
        var p = CreateProfile(path);
        p.YPolicy = YAxisPolicy.AdditiveGravity;
        p.AxisCurves.YCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.4f, 1f),
            new Keyframe(1f, 1f));
        p.AxisCurves.YScale = 2f;
        p.AxisCurves.ZCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        p.AxisCurves.ZScale = 3f;
        EditorUtility.SetDirty(p);
    }

    static void CreateLauncher(string path)
    {
        var p = CreateProfile(path);
        p.YPolicy = YAxisPolicy.MotionControlled;
        p.AxisCurves.YCurve = AnimationCurve.Linear(0f, 0f, 0.5f, 1f);
        p.AxisCurves.YScale = 3f;
        EditorUtility.SetDirty(p);
    }
}
#endif

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 将 Displacement/Lateral/Warp/GravityBehavior 迁入 AxisCurves + YPolicy（L5 删字段前须先跑批量迁移）。
/// </summary>
public static class MotionProfileLegacyMigration
{
    public struct Result
    {
        public bool Changed;
        public bool SkippedAlreadyMigrated;
        public bool SkippedNoLegacyData;
        public string Note;
    }

    public static Result TryMigrate(MotionProfileSO profile)
    {
        var r = new Result();
        if (profile == null)
        {
            r.Note = "profile null";
            return r;
        }

        if (profile.UsesAxisCurves)
        {
            r.SkippedAlreadyMigrated = true;
            r.Note = "already has AxisCurves";
            return r;
        }

        var so = new SerializedObject(profile);
        var disp = so.FindProperty("DisplacementCurve");
        var baseDist = so.FindProperty("BaseDistance");
        var peak = so.FindProperty("PeakSpeedMultiplier");
        var lat = so.FindProperty("LateralCurve");
        var latDist = so.FindProperty("LateralDistance");
        var warp = so.FindProperty("WarpCurve");
        var warpMax = so.FindProperty("MaxWarpDistance");
        var grav = so.FindProperty("GravityBehavior");

        if (disp == null && baseDist == null)
        {
            profile.ApplyDefaultForwardAxis(4f);
            EditorUtility.SetDirty(profile);
            r.Changed = true;
            r.Note = "default Z axis (legacy YAML already stripped)";
            return r;
        }

        var axis = so.FindProperty("AxisCurves");
        if (axis == null)
        {
            r.Note = "AxisCurves property missing on SO";
            return r;
        }

        var zCurve = axis.FindPropertyRelative("ZCurve");
        var zScale = axis.FindPropertyRelative("ZScale");
        var xCurve = axis.FindPropertyRelative("XCurve");
        var xScale = axis.FindPropertyRelative("XScale");
        var yPolicy = so.FindProperty("legacyYPolicy") ?? so.FindProperty("YPolicy");

        if (disp != null && disp.animationCurveValue != null && disp.animationCurveValue.length > 0)
        {
            zCurve.animationCurveValue = new AnimationCurve(disp.animationCurveValue.keys);
        }
        else
        {
            zCurve.animationCurveValue = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        var baseMeters = baseDist != null ? baseDist.floatValue : 0f;
        var peakMul = peak != null ? peak.floatValue : 1f;
        zScale.floatValue = baseMeters * Mathf.Max(0.01f, peakMul);
        if (zScale.floatValue < 0.001f)
        {
            zScale.floatValue = 4f;
        }

        if (lat != null && lat.animationCurveValue != null && lat.animationCurveValue.length > 0)
        {
            xCurve.animationCurveValue = new AnimationCurve(lat.animationCurveValue.keys);
            xScale.floatValue = latDist != null ? latDist.floatValue : 0f;
        }

        if (warp != null && warp.animationCurveValue != null && warp.animationCurveValue.length > 0)
        {
            var warpMaxVal = warpMax != null ? warpMax.floatValue : 0f;
            var keys = warp.animationCurveValue.keys;
            if (keys != null && keys.Length > 0)
            {
                var merged = new AnimationCurve(zCurve.animationCurveValue.keys);
                for (var i = 0; i < keys.Length; i++)
                {
                    var t = keys[i].time;
                    var w = warp.animationCurveValue.Evaluate(t) * warpMaxVal;
                    var z = merged.Evaluate(t) * zScale.floatValue + w;
                    merged.AddKey(t, z / Mathf.Max(0.01f, zScale.floatValue));
                }

                zCurve.animationCurveValue = merged;
            }
        }

        if (yPolicy != null)
        {
            var suspended = grav != null && grav.enumValueIndex == 1;
            yPolicy.enumValueIndex = suspended ? (int)YAxisPolicy.SuspendGravity : (int)YAxisPolicy.UseGravity;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
        r.Changed = true;
        r.Note = $"ZScale={zScale.floatValue:F2}";
        return r;
    }

    public static int MigrateAllInProject(bool dryRun, out string report)
    {
        var guids = AssetDatabase.FindAssets("t:MotionProfileSO");
        var migrated = 0;
        var skipped = 0;
        var failed = 0;
        var sb = new StringBuilder();

        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var profile = AssetDatabase.LoadAssetAtPath<MotionProfileSO>(path);
            if (profile == null)
            {
                continue;
            }

            if (dryRun)
            {
                if (profile.UsesAxisCurves)
                {
                    skipped++;
                }
                else
                {
                    sb.AppendLine($"  NEED: {path}");
                    failed++;
                }

                continue;
            }

            Undo.RecordObject(profile, "Migrate MotionProfile to AxisCurves");
            var r = TryMigrate(profile);
            if (r.Changed)
            {
                migrated++;
                sb.AppendLine($"  OK {profile.name} ({r.Note})");
            }
            else if (r.SkippedAlreadyMigrated)
            {
                skipped++;
            }
            else
            {
                failed++;
                sb.AppendLine($"  FAIL {path} — {r.Note}");
            }
        }

        if (!dryRun)
        {
            AssetDatabase.SaveAssets();
        }

        report = $"total={guids.Length} migrated={migrated} skipped={skipped} needManual={failed}\n{sb}";
        return migrated;
    }
}
#endif

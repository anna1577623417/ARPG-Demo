#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 179.1 W3 — Stride Reference 烘焙器。
/// <para>菜单：</para>
/// <para>  GameMain/Locomotion/Bake Stride References (Selected)  — 仅烘焙选中 ActionDataSO</para>
/// <para>  GameMain/Locomotion/Bake Stride References (All MotorDriven) — 烘焙整库所有 MotorDriven Action</para>
/// <para>烘焙策略：ReferenceDuration = Clip.length；ReferenceDistance = Clip RootMotion 水平位移；RM=0 时按 DesignedSpeed 反推。</para>
/// </summary>
public static class StrideReferenceBaker
{
    [MenuItem("GameMain/Locomotion/Bake Stride References (Selected)")]
    public static void BakeSelected()
    {
        var targets = new List<ActionDataSO>();
        foreach (var obj in Selection.objects)
        {
            if (obj is ActionDataSO a) targets.Add(a);
        }
        if (targets.Count == 0)
        {
            EditorUtility.DisplayDialog("Stride Baker", "请先在 Project 窗口选中至少一个 ActionDataSO。", "OK");
            return;
        }
        BakeList(targets);
    }

    [MenuItem("GameMain/Locomotion/Bake Stride References (All MotorDriven)")]
    public static void BakeAllMotorDriven()
    {
        var list = new List<ActionDataSO>();
        var guids = AssetDatabase.FindAssets("t:ActionDataSO");
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var a = AssetDatabase.LoadAssetAtPath<ActionDataSO>(path);
            if (a != null && a.MovementMode == MovementMode.MotorDriven) list.Add(a);
        }
        if (list.Count == 0)
        {
            EditorUtility.DisplayDialog("Stride Baker",
                "未找到任何 MovementMode=MotorDriven 的 ActionDataSO。\n" +
                "请先把 Walk_Loop / Run_Loop / Sprint_Loop / Crouch_*_Loop 资产的 MovementMode 设为 MotorDriven。",
                "OK");
            return;
        }
        BakeList(list);
    }

    static void BakeList(List<ActionDataSO> targets)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[Stride Baker] {System.DateTime.Now:HH:mm}");
        sb.AppendLine($"Targets: {targets.Count}");
        sb.AppendLine();

        var written = 0;
        var skipped = 0;
        for (var i = 0; i < targets.Count; i++)
        {
            var a = targets[i];
            if (Bake(a, sb)) written++;
            else skipped++;
        }
        sb.AppendLine();
        sb.AppendLine($"✓ Written: {written}   ✗ Skipped: {skipped}");
        AssetDatabase.SaveAssets();
        Debug.Log(sb.ToString());
    }

    static bool Bake(ActionDataSO action, StringBuilder sb)
    {
        if (action == null) return false;
        var clip = action.MainClip;
        if (clip == null)
        {
            sb.AppendLine($"  ✗ {action.name}: 无 MainClip");
            return false;
        }
        if (action.MovementMode != MovementMode.MotorDriven)
        {
            sb.AppendLine($"  - {action.name}: 跳过（MovementMode={action.MovementMode}）");
            return false;
        }

        action.ReferenceDuration = clip.length;

        var totalDist = EstimateClipHorizontalDistance(clip);
        if (totalDist < 0.01f && action.DesignedSpeed > 0f)
        {
            totalDist = action.DesignedSpeed * clip.length;
            sb.AppendLine($"  ⚠ {action.name}: RootMotion≈0；按 DesignedSpeed={action.DesignedSpeed} 反推 → distance={totalDist:F2}m");
        }
        action.ReferenceDistance = totalDist;

        EditorUtility.SetDirty(action);
        sb.AppendLine($"  ✓ {action.name}: dur={action.ReferenceDuration:F2}s dist={action.ReferenceDistance:F2}m speed={action.ReferenceSpeed:F2}m/s");
        return true;
    }

    /// <summary>简化：用 Animation Curves 中 RootT.x/z 关键帧首末差估算水平位移。</summary>
    static float EstimateClipHorizontalDistance(AnimationClip clip)
    {
        var binding = AnimationUtility.GetCurveBindings(clip);
        var startPos = Vector3.zero;
        var endPos = Vector3.zero;
        for (var i = 0; i < binding.Length; i++)
        {
            ref readonly var b = ref binding[i];
            var curve = AnimationUtility.GetEditorCurve(clip, b);
            if (curve == null || curve.length < 2) continue;
            var v0 = curve[0].value;
            var vN = curve[curve.length - 1].value;
            switch (b.propertyName)
            {
                case "RootT.x": startPos.x = v0; endPos.x = vN; break;
                case "RootT.z": startPos.z = v0; endPos.z = vN; break;
            }
        }
        var dx = endPos.x - startPos.x;
        var dz = endPos.z - startPos.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }
}
#endif

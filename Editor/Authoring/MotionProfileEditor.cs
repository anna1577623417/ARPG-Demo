#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MotionProfileSO))]
public sealed class MotionProfileEditor : Editor
{
    private CurvePresetType _preset = CurvePresetType.EaseInOut;
    private float _power = 2f;
    private float _defaultForwardMeters = 4f;

    public override void OnInspectorGUI()
    {
        var profile = (MotionProfileSO)target;
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "【三轴位置曲线】局部空间：Evaluate(t)×Scale=米。允许负值。运行时仅读 AxisCurves。",
            MessageType.Info);

        if (!profile.UsesAxisCurves)
        {
            EditorGUILayout.HelpBox(
                "未配置 AxisCurves — 运行时位移为 0。请点下方迁移或生成默认 Z 轴。",
                MessageType.Warning);
            if (GUILayout.Button("从旧字段迁移（若资产仍含 DisplacementCurve 序列化数据）"))
            {
                Undo.RecordObject(profile, "Migrate MotionProfile");
                var r = MotionProfileLegacyMigration.TryMigrate(profile);
                Debug.Log($"[MotionXYZ] {profile.name}: {r.Note}");
            }

            _defaultForwardMeters = EditorGUILayout.FloatField("默认前进距离 (m)", _defaultForwardMeters);
            if (GUILayout.Button("生成默认 Z 轴 (0→1 × 距离)"))
            {
                Undo.RecordObject(profile, "Default Z Axis");
                profile.ApplyDefaultForwardAxis(_defaultForwardMeters);
                EditorUtility.SetDirty(profile);
            }
        }

        DrawDefaultInspector();

        MotionCurveSegmentPresetGUI.Draw(profile, serializedObject);

        EditorGUILayout.Space(6f);
        if (profile.UsesAxisCurves && GUILayout.Button("Scene 预览轨迹（折线 Gizmo）"))
        {
            MotionPathGizmoDrawer.SetPreviewProfile(profile);
        }

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("整段曲线生成（替换整条曲线）", EditorStyles.boldLabel);

        _preset = (CurvePresetType)EditorGUILayout.EnumPopup("Preset", _preset);
        _power = EditorGUILayout.Slider("Power", _power, 1f, 6f);

        if (GUILayout.Button("Generate Z Curve (Forward)"))
        {
            Undo.RecordObject(profile, "Generate Z Curve");
            profile.AxisCurves.ZCurve = MotionCurveGenerator.Generate(_preset, _power);
            if (profile.AxisCurves.ZScale < 0.001f)
            {
                profile.AxisCurves.ZScale = _defaultForwardMeters;
            }

            EditorUtility.SetDirty(profile);
        }

        if (GUILayout.Button("Generate Speed Curve"))
        {
            Undo.RecordObject(profile, "Generate Motion Speed Curve");
            profile.SpeedOverTime = MotionCurveGenerator.Generate(_preset, _power);
            EditorUtility.SetDirty(profile);
        }

        EditorGUILayout.HelpBox(
            "【段预设】只改两 Key 之间切线；【整段生成】会替换整条曲线。负 Z=后撤，负 Y=下砸。",
            MessageType.Info);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif

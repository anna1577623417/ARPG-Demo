#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// MotionProfile 的 Inspector 增强工具。
/// Why: 将数学预设生成入口放到资产面板，提高策划迭代效率。
/// </summary>
[CustomEditor(typeof(MotionProfileSO))]
public sealed class MotionProfileEditor : Editor
{
    private CurvePresetType _preset = CurvePresetType.EaseInOut;
    private float _power = 2f;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Curve Preset Generator", EditorStyles.boldLabel);

        _preset = (CurvePresetType)EditorGUILayout.EnumPopup("Preset", _preset);
        _power = EditorGUILayout.Slider("Power", _power, 1f, 6f);

        if (GUILayout.Button("Generate Displacement Curve"))
        {
            var profile = (MotionProfileSO)target;
            Undo.RecordObject(profile, "Generate Motion Displacement Curve");
            profile.DisplacementCurve = MotionCurveGenerator.Generate(_preset, _power);
            EditorUtility.SetDirty(profile);
        }

        if (GUILayout.Button("Generate Speed Curve"))
        {
            var profile = (MotionProfileSO)target;
            Undo.RecordObject(profile, "Generate Motion Speed Curve");
            profile.SpeedOverTime = MotionCurveGenerator.Generate(_preset, _power);
            EditorUtility.SetDirty(profile);
        }

        EditorGUILayout.HelpBox("Use presets for baseline, then tweak keys manually for final combat feel.", MessageType.Info);
    }
}
#endif

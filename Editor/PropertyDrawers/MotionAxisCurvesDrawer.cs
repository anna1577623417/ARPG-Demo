#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(MotionAxisCurves))]
public sealed class MotionAxisCurvesDrawer : PropertyDrawer
{
    const float FoldoutLine = 18f;
    const float LabelLine = 16f;
    const float CurveHeight = 22f;
    const float ScaleLabelLine = 14f;
    const float ScaleFieldLine = 18f;
    const float ScaleFieldWidth = 72f;
    const float AxisGap = 4f;
    const float FlipWidth = 24f;
    const float BlockPadding = 2f;

    static float AxisBlockHeight =>
        LabelLine + CurveHeight + ScaleLabelLine + ScaleFieldLine + AxisGap;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
        {
            return FoldoutLine;
        }

        return FoldoutLine + AxisBlockHeight * 3f + BlockPadding;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var foldoutRect = new Rect(position.x, position.y, position.width, FoldoutLine);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            return;
        }

        var y = position.y + FoldoutLine + BlockPadding;
        DrawAxisRow(position.x, ref y, position.width, property,
            nameof(MotionAxisCurves.XCurve), nameof(MotionAxisCurves.XScale),
            "【X】Left ↔ Right");
        DrawAxisRow(position.x, ref y, position.width, property,
            nameof(MotionAxisCurves.YCurve), nameof(MotionAxisCurves.YScale),
            "【Y】Down ↕ Up");
        DrawAxisRow(position.x, ref y, position.width, property,
            nameof(MotionAxisCurves.ZCurve), nameof(MotionAxisCurves.ZScale),
            "【Z】Back ↔ Forward");
    }

    static void DrawAxisRow(
        float x,
        ref float y,
        float width,
        SerializedProperty curvesProperty,
        string curveName,
        string scaleName,
        string axisLabel)
    {
        var curveProp = curvesProperty.FindPropertyRelative(curveName);
        var scaleProp = curvesProperty.FindPropertyRelative(scaleName);

        var labelRect = new Rect(x, y, width - FlipWidth - 2f, LabelLine);
        var flipRect = new Rect(x + width - FlipWidth, y, FlipWidth, LabelLine);
        EditorGUI.LabelField(labelRect, axisLabel, EditorStyles.miniBoldLabel);
        if (GUI.Button(flipRect, new GUIContent("↕", "垂直镜像曲线（Value 取反）")))
        {
            Undo.RecordObject(curvesProperty.serializedObject.targetObject, $"Flip {axisLabel}");
            var curve = curveProp.animationCurveValue;
            MotionAxisCurveFlipUtil.FlipCurve(ref curve);
            curveProp.animationCurveValue = curve;
            curvesProperty.serializedObject.ApplyModifiedProperties();
        }

        y += LabelLine;

        var curveRect = new Rect(x, y, width, CurveHeight);
        EditorGUI.PropertyField(curveRect, curveProp, GUIContent.none);
        y += CurveHeight;

        var scaleLabelRect = new Rect(x, y, width, ScaleLabelLine);
        EditorGUI.LabelField(scaleLabelRect, "Scale (m)", EditorStyles.miniLabel);
        y += ScaleLabelLine;

        var scaleFieldRect = new Rect(x, y, ScaleFieldWidth, ScaleFieldLine);
        scaleProp.floatValue = EditorGUI.FloatField(scaleFieldRect, scaleProp.floatValue);
        y += ScaleFieldLine + AxisGap;
    }
}
#endif

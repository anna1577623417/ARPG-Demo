#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(MotionAxisCurves))]
public sealed class MotionAxisCurvesDrawer : PropertyDrawer
{
    const int FieldCount = 6;
    const float Line = 18f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        property.isExpanded ? Line * (FieldCount + 1) : Line;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var foldoutRect = new Rect(position.x, position.y, position.width, Line);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            return;
        }

        EditorGUI.indentLevel++;
        var y = position.y + Line;
        DrawField(ref y, position, property.FindPropertyRelative(nameof(MotionAxisCurves.XCurve)), "【X】Left ↔ Right");
        DrawField(ref y, position, property.FindPropertyRelative(nameof(MotionAxisCurves.XScale)), "X Scale (m)");
        DrawField(ref y, position, property.FindPropertyRelative(nameof(MotionAxisCurves.YCurve)), "【Y】Down ↕ Up");
        DrawField(ref y, position, property.FindPropertyRelative(nameof(MotionAxisCurves.YScale)), "Y Scale (m)");
        DrawField(ref y, position, property.FindPropertyRelative(nameof(MotionAxisCurves.ZCurve)), "【Z】Back ↔ Forward");
        DrawField(ref y, position, property.FindPropertyRelative(nameof(MotionAxisCurves.ZScale)), "Z Scale (m)");
        EditorGUI.indentLevel--;
    }

    static void DrawField(ref float y, Rect position, SerializedProperty prop, string text)
    {
        var rect = new Rect(position.x, y, position.width, Line);
        EditorGUI.PropertyField(rect, prop, new GUIContent(text));
        y += Line;
    }
}
#endif

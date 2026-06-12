using UnityEditor;
using UnityEngine;

/// <summary>
/// 164.1 L8 终态 Drawer：State / Fallback / LocomotionAction + 方向 key（7 字段）。
/// Obsolete 字段不在 Inspector 展示；运行时经 <see cref="LocomotionStateBindingExtensions"/> 只读回落。
/// </summary>
[CustomPropertyDrawer(typeof(LocomotionStateBinding))]
public class LocomotionStateBindingDrawer : PropertyDrawer
{
    const float LineH = 18f;
    const float Pad = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
        {
            return LineH + Pad;
        }

        var extra = CountExtraRowsForState(property);
        return (1 /*header*/ + 3 /*State/Fallback/Action*/ + extra) * (LineH + Pad);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var headerRect = new Rect(position.x, position.y, position.width, LineH);
        property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, label, true);

        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        var y = position.y + LineH + Pad;
        var lineRect = new Rect(position.x, y, position.width, LineH);
        EditorGUI.indentLevel++;

        var stateProp = property.FindPropertyRelative(nameof(LocomotionStateBinding.State));
        EditorGUI.PropertyField(lineRect, stateProp);
        lineRect.y += LineH + Pad;

        EditorGUI.PropertyField(lineRect, property.FindPropertyRelative(nameof(LocomotionStateBinding.FallbackState)));
        lineRect.y += LineH + Pad;

        EditorGUI.PropertyField(
            lineRect,
            property.FindPropertyRelative(nameof(LocomotionStateBinding.LocomotionAction)),
            new GUIContent("Locomotion Action ★"));
        lineRect.y += LineH + Pad;

        var stateId = ReadStateId(stateProp);
        if (stateId == LocomotionStateId.None)
        {
            EditorGUI.HelpBox(lineRect, "请先在上方 State 选择具体状态。", MessageType.Info);
        }

        var strafeDirProp = property.FindPropertyRelative(nameof(LocomotionStateBinding.StrafeDirection));
        var turnDirProp = property.FindPropertyRelative(nameof(LocomotionStateBinding.TurnDirection));
        var runReqProp = property.FindPropertyRelative(nameof(LocomotionStateBinding.RunRequirement));

        if (stateId == LocomotionStateId.StrafeLocomotion)
        {
            lineRect.y += LineH + Pad;
            EditorGUI.PropertyField(lineRect, strafeDirProp, new GUIContent("Strafe Direction ★"));
            lineRect.y += LineH + Pad;
            EditorGUI.PropertyField(lineRect, runReqProp, new GUIContent("Run Requirement ★"));
            WriteEnumInt(turnDirProp, (int)TurnDirection4.None);
        }
        else if (stateId == LocomotionStateId.TurnInPlaceDirected)
        {
            lineRect.y += LineH + Pad;
            EditorGUI.PropertyField(lineRect, turnDirProp, new GUIContent("Turn Direction ★"));
            WriteEnumInt(strafeDirProp, (int)StrafeDirection8.None);
            WriteEnumInt(runReqProp, (int)LocomotionRunRequirement.Any);
        }
        else if (stateId != LocomotionStateId.None)
        {
            WriteEnumInt(strafeDirProp, (int)StrafeDirection8.None);
            WriteEnumInt(turnDirProp, (int)TurnDirection4.None);
            WriteEnumInt(runReqProp, (int)LocomotionRunRequirement.Any);
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    static int CountExtraRowsForState(SerializedProperty property)
    {
        var stateProp = property.FindPropertyRelative(nameof(LocomotionStateBinding.State));
        if (stateProp == null) return 0;
        var stateId = ReadStateId(stateProp);
        if (stateId == LocomotionStateId.None) return 1;
        if (stateId == LocomotionStateId.StrafeLocomotion) return 3;
        if (stateId == LocomotionStateId.TurnInPlaceDirected) return 2;
        return 0;
    }

    static LocomotionStateId ReadStateId(SerializedProperty stateProp) =>
        stateProp == null ? LocomotionStateId.None : (LocomotionStateId)(byte)stateProp.intValue;

    static void WriteEnumInt(SerializedProperty prop, int value)
    {
        if (prop != null)
        {
            prop.intValue = value;
        }
    }
}

#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// AutoFix 直接写 SO 后，把 bindings 顺序推回 SerializedObject，避免 Inspector 用旧缓存覆盖。
/// </summary>
static class LocomotionProfileEditorBindingSync
{
    public static void WriteBindingsToSerializedObject(SerializedObject serializedObject, LocomotionProfile profile)
    {
        if (serializedObject == null || profile == null)
        {
            return;
        }

        serializedObject.Update();
        var bindingsProp = serializedObject.FindProperty("bindings");
        if (bindingsProp == null || !bindingsProp.isArray)
        {
            return;
        }

        var rows = profile.Bindings;
        var count = rows != null ? rows.Length : 0;
        bindingsProp.arraySize = count;

        for (var i = 0; i < count; i++)
        {
            CopyBinding(rows[i], bindingsProp.GetArrayElementAtIndex(i));
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        serializedObject.Update();
    }

    static void CopyBinding(LocomotionStateBinding src, SerializedProperty dst)
    {
        // 必须用 intValue：LocomotionStateId 等枚举声明序 ≠ 底层数值（159.3/161.2 重排后 enumValueIndex 会写空 State）。
        WriteEnumInt(dst.FindPropertyRelative(nameof(LocomotionStateBinding.State)), (int)src.State);
        WriteEnumInt(dst.FindPropertyRelative(nameof(LocomotionStateBinding.FallbackState)), (int)src.FallbackState);
        dst.FindPropertyRelative(nameof(LocomotionStateBinding.DiscreteAction)).objectReferenceValue = src.DiscreteAction;
        dst.FindPropertyRelative(nameof(LocomotionStateBinding.ContinuousClip)).objectReferenceValue = src.ContinuousClip;
        dst.FindPropertyRelative(nameof(LocomotionStateBinding.TransitionDuration)).floatValue = src.TransitionDuration;
        dst.FindPropertyRelative(nameof(LocomotionStateBinding.Speed)).floatValue = src.Speed;
        dst.FindPropertyRelative(nameof(LocomotionStateBinding.UseRootMotion)).boolValue = src.UseRootMotion;
        dst.FindPropertyRelative(nameof(LocomotionStateBinding.CanRotateDuring)).boolValue = src.CanRotateDuring;
        dst.FindPropertyRelative(nameof(LocomotionStateBinding.CanMoveDuring)).boolValue = src.CanMoveDuring;
        dst.FindPropertyRelative(nameof(LocomotionStateBinding.ReferenceLocomotionSpeed)).floatValue = src.ReferenceLocomotionSpeed;
        WriteEnumInt(dst.FindPropertyRelative(nameof(LocomotionStateBinding.StrafeDirection)), (int)src.StrafeDirection);
        WriteEnumInt(dst.FindPropertyRelative(nameof(LocomotionStateBinding.TurnDirection)), (int)src.TurnDirection);
        WriteEnumInt(dst.FindPropertyRelative(nameof(LocomotionStateBinding.RunRequirement)), (int)src.RunRequirement);
    }

    static void WriteEnumInt(SerializedProperty prop, int value)
    {
        if (prop == null)
        {
            return;
        }

        prop.intValue = value;
    }
}
#endif

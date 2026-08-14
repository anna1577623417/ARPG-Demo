#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed partial class MotionProfileEditor
{
    static StopStrategy s_stopPreviewStrategy = StopStrategy.InheritPhysics;
    static bool s_unlockStopRhythmCurves;

    static readonly Color StopPreviewTint = new Color(1f, 1f, 1f, 0.55f);

    void DrawStopAuthoringSection(MotionProfileSO profile)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Stop Authoring（182.1）", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(MotionProfileSO.EnableStopAuthoring)));

        if (!profile.EnableStopAuthoring)
        {
            EditorGUILayout.HelpBox(
                "未启用：本 MotionProfile 不参与 Stop 系统；Action.EnableStopFeature 时 InheritPhysics/MotionProfile 策略将降级。",
                MessageType.None);
            return;
        }

        var previewFromAction = TryGetUniqueReferencingActionStopStrategy(out var actionStrategy);
        if (previewFromAction)
        {
            s_stopPreviewStrategy = actionStrategy;
        }

        using (new EditorGUI.DisabledScope(previewFromAction))
        {
            var next = (StopStrategy)EditorGUILayout.EnumPopup(
                new GUIContent(
                    previewFromAction ? "Action 策略（只读预览）" : "预览策略（矩阵）",
                    previewFromAction
                        ? "来自唯一引用本 Profile 的 Action.StopStrategy。米数权威在 Action，本页只作灰显预览。"
                        : "与 Action.StopStrategy 对齐，仅用于本 Inspector 字段可读/灰显预览。"),
                s_stopPreviewStrategy);
            if (!previewFromAction)
            {
                s_stopPreviewStrategy = next;
            }
        }

        switch (s_stopPreviewStrategy)
        {
            case StopStrategy.Snap:
                EditorGUILayout.HelpBox(
                    "Snap：不产生停止位移 — ZXY 曲线锁定（Action 侧 Snap 时不读 MotionProfile 位移）。",
                    MessageType.Info);
                break;
            case StopStrategy.InheritPhysics:
                EditorGUILayout.HelpBox(
                    "InheritPhysics：ZXY 曲线只表表现节奏；米数由入场速度恒定减速度积分决定，不再是节奏×Lerp距离。Scale(m) 不是停止距离。",
                    MessageType.Info);
                break;
            case StopStrategy.MotionProfile:
                EditorGUILayout.HelpBox(
                    "MotionProfile：ZXY 曲线表作者米数（旧默认 Motion 行为）；AnimSpeedCurve 仍叠加。",
                    MessageType.Info);
                break;
        }
    }

    void DrawAxisCurvesPreviewSection(MotionProfileSO profile)
    {
        var axisProp = serializedObject.FindProperty(nameof(MotionProfileSO.AxisCurves));
        if (axisProp == null)
        {
            return;
        }

        EditorGUILayout.Space(4f);
        var locked = IsStopCurvePreviewLocked(profile);
        if (ShouldLockAxisCurvesForStopPreview(profile))
        {
            EditorGUILayout.HelpBox(
                locked
                    ? "ActionData 只读预览：InheritPhysics / Snap 下 ZXY 与 Scale 灰显，避免把节奏幅度当成停止米数。需要改表现节奏时再解锁。"
                    : "已解锁编辑表现节奏。InheritPhysics 运行时米数仍由 Action.D_ref 与 v_entry 积分决定。",
                locked ? MessageType.None : MessageType.Warning);
            s_unlockStopRhythmCurves = EditorGUILayout.Toggle(
                new GUIContent("解锁编辑表现节奏", "默认锁定。仅改曲线形状/节奏时打开，不要把 Z Scale 当成 D_ref。"),
                s_unlockStopRhythmCurves);
        }

        var oldColor = GUI.color;
        if (locked)
        {
            GUI.color = StopPreviewTint;
        }

        using (new EditorGUI.DisabledScope(locked))
        {
            EditorGUILayout.PropertyField(
                axisProp,
                new GUIContent("XYZ 局部空间位置曲线"),
                true);
        }

        GUI.color = oldColor;
    }

    bool TryGetUniqueReferencingActionStopStrategy(out StopStrategy strategy)
    {
        strategy = s_stopPreviewStrategy;
        if (_referencingActions == null || _referencingActions.Length != 1)
        {
            return false;
        }

        var action = _referencingActions[0];
        if (action == null || !action.EnableStopFeature)
        {
            return false;
        }

        strategy = action.StopStrategy;
        return true;
    }

    bool ShouldLockAxisCurvesForStopPreview(MotionProfileSO profile) =>
        profile.EnableStopAuthoring
        && (s_stopPreviewStrategy == StopStrategy.Snap
            || s_stopPreviewStrategy == StopStrategy.InheritPhysics);

    bool IsStopCurvePreviewLocked(MotionProfileSO profile) =>
        ShouldLockAxisCurvesForStopPreview(profile) && !s_unlockStopRhythmCurves;
}
#endif

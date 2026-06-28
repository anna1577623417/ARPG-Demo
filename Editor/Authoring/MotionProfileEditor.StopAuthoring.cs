#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed partial class MotionProfileEditor
{
    static StopStrategy s_stopPreviewStrategy = StopStrategy.InheritPhysics;

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

        s_stopPreviewStrategy = (StopStrategy)EditorGUILayout.EnumPopup(
            new GUIContent(
                "预览策略（矩阵）",
                "与 Action.StopStrategy 对齐，仅用于本 Inspector 字段可读/灰显预览。"),
            s_stopPreviewStrategy);

        switch (s_stopPreviewStrategy)
        {
            case StopStrategy.Snap:
                EditorGUILayout.HelpBox(
                    "Snap：不产生停止位移 — ZXY 曲线锁定（Action 侧 Snap 时不读 MotionProfile 位移）。",
                    MessageType.Info);
                break;
            case StopStrategy.InheritPhysics:
                EditorGUILayout.HelpBox(
                    "InheritPhysics：ZXY 曲线表 0~1 节奏；米数由 Action.InheritPhysics 运行时 Distance 决定。",
                    MessageType.Info);
                break;
            case StopStrategy.MotionProfile:
                EditorGUILayout.HelpBox(
                    "MotionProfile：ZXY 曲线表作者米数（旧默认 Motion 行为）；AnimSpeedCurve 仍叠加。",
                    MessageType.Info);
                break;
        }
    }

    bool ShouldLockAxisCurvesForStopPreview(MotionProfileSO profile) =>
        profile.EnableStopAuthoring && s_stopPreviewStrategy == StopStrategy.Snap;
}
#endif

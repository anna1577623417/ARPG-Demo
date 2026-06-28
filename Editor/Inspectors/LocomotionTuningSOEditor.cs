#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>183.1 W3：LocomotionTuning Rotation 段校验与 Tooltip 补充。</summary>
[CustomEditor(typeof(LocomotionTuningSO))]
public sealed class LocomotionTuningSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspector();

        var tuning = (LocomotionTuningSO)target;
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("183.1 Rotation 对齐提示", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "SnapSpeedThreshold 须与 Player Prefab → State Manager → Turn → LockSpeedThreshold 一致（默认 0.2 m/s）。\n" +
            "184.1：TapMaxDuration/HoldEnterDelay 驱动 Tap/Hold；VisualMaxAngularSpeedDeg 驱动 VisualRoot 缓追。\n" +
            "法环档：RotationMode=SnapAlways，SkipTurnPresentationWhenWantsRun=true，Turn90ThresholdDeg≈60°。\n" +
            "Press 起步 gate 与 Turn90ThresholdDeg 共用同一阈值 —— 大角 WalkStart 延后，由 TurnResolver 播 pivot。",
            MessageType.Info);

        if (tuning.RotationMode == LocomotionRotationMode.Smooth
            && !tuning.UseTuningRotationSpeed)
        {
            EditorGUILayout.HelpBox(
                "当前为兼容档：Smooth + UseTuningRotationSpeed=false → Locomotion 仍读 Stats.RotationSpeed。",
                MessageType.None);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif

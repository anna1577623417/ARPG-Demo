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
        EditorGUILayout.LabelField("234.5 FreeLocomotion Direction Authority", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Free Walk/Run 的方向、LogicFacing 与普通 VisualRoot 已固定为同 Tick 对齐。\n" +
            "DirectionTurnResponseTime、MotionFacingAngularSpeedDeg、RotationMode、RotationSpeedDegPerSec、" +
            "VisualMaxAngularSpeedDeg 仅保留旧资产序列化/诊断，不再驱动 FreeLocomotion 水平转向。\n" +
            "235.1：Turn90/180 大角差输入同 Tick 提交 LogicFacing，并进入显式 Turn-first 原地 Gate。" +
            "Gate 期间平面速度为 0，Turn90/180InPlaceDuration 同时约束表现 speed-to-fit；结束后才交给 Walk/Run。" +
            "普通同向启动低于阈值，不进入 Gate。Turn Clip 仍不得写 Root/KCC/Camera。",
            MessageType.Info);

        if (tuning.RotationMode == LocomotionRotationMode.Smooth
            && !tuning.UseTuningRotationSpeed)
        {
            EditorGUILayout.HelpBox(
                "Legacy 配置仍为 Smooth，但 234.5 Runtime 已忽略该策略。建议资产后续迁移为 SnapAlways，" +
                "不要通过提高角速度模拟即时转向。",
                MessageType.Warning);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif

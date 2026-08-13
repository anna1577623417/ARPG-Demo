#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed partial class ActionDataInspector
{
    void DrawMotionAuthoritySection(ActionDataSO action)
    {
        var isLocomotion = action.IntentCategory == ActionIntentCategory.Locomotion;
        var bindings = isLocomotion
            ? LocomotionActionBindingIndex.GetBindings(action)
            : (IReadOnlyList<LocomotionActionBindingIndex.Entry>)System.Array.Empty<LocomotionActionBindingIndex.Entry>();

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                isLocomotion ? "Locomotion Authoring (227.4)" : "Motion Authority (227.4)",
                EditorStyles.boldLabel);

            if (isLocomotion)
            {
                DrawBindingContext(bindings);
                DrawContinuousRole(serializedObject, bindings);
            }

            var driverProp = serializedObject.FindProperty(nameof(ActionDataSO.MotionDriverMode));
            EditorGUILayout.PropertyField(driverProp, new GUIContent("Motion Driver"));

            var plan = ActionMotionDriverResolver.Resolve(action);
            EditorGUILayout.HelpBox(
                $"Effective: {plan.EffectiveMode}\n" +
                $"Motor: executor={plan.UsesMotionExecutor}, baseTick={plan.RequiresBaseMotorTick}, " +
                $"clipRoot={plan.UsesClipRootMotion}\n" +
                $"Physics: vertical={plan.MaintainsVerticalPhysics}, grounding={plan.MaintainsGrounding}\n" +
                $"Reason: {plan.ResolutionReason}",
                plan.IsValid ? MessageType.Info : MessageType.Error);

            DrawDriverConditionalFields(action, plan);
            if (action.MotionDriverMode == ActionMotionDriverMode.LegacyAuto)
            {
                DrawLegacyMigrationButton(action, plan);
            }
        }
    }

    static void DrawBindingContext(IReadOnlyList<LocomotionActionBindingIndex.Entry> bindings)
    {
        EditorGUILayout.LabelField("Context / Binding", EditorStyles.boldLabel);
        if (bindings.Count == 0)
        {
            EditorGUILayout.HelpBox("尚未被任何 LocomotionProfile State 绑定。", MessageType.Info);
            return;
        }

        var hasContinuous = false;
        var hasDiscrete = false;
        for (var i = 0; i < bindings.Count; i++)
        {
            var entry = bindings[i];
            EditorGUILayout.LabelField(
                $"{entry.Profile.name} / {entry.State} / {(entry.IsContinuous ? "Continuous" : "Discrete")}",
                EditorStyles.miniLabel);
            hasContinuous |= entry.IsContinuous;
            hasDiscrete |= entry.IsDiscrete;
        }

        if (hasContinuous && hasDiscrete)
        {
            EditorGUILayout.HelpBox("同一 Action 同时绑定连续与离散 State，语义冲突。", MessageType.Error);
        }
    }

    static void DrawContinuousRole(
        SerializedObject so,
        IReadOnlyList<LocomotionActionBindingIndex.Entry> bindings)
    {
        var hasContinuous = false;
        for (var i = 0; i < bindings.Count; i++)
        {
            hasContinuous |= bindings[i].IsContinuous;
        }

        if (!hasContinuous && bindings.Count > 0)
        {
            return;
        }

        var prop = so.FindProperty(nameof(ActionDataSO.IsContinuousLocomotion));
        if (prop != null)
        {
            EditorGUILayout.PropertyField(prop, new GUIContent("Is Continuous Locomotion"));
        }
    }

    void DrawDriverConditionalFields(ActionDataSO action, ActionMotionExecutionPlan plan)
    {
        if (action.MotionDriverMode == ActionMotionDriverMode.LegacyAuto)
        {
            var rootProp = serializedObject.FindProperty(nameof(ActionDataSO.UseClipRootMotion));
            EditorGUILayout.PropertyField(rootProp, new GUIContent("Legacy Use Clip Root Motion"));
        }

        if (action.MotionDriverMode != ActionMotionDriverMode.MotionProfile
            && action.MotionDriverMode != ActionMotionDriverMode.LegacyAuto
            && action.MotionProfile != null)
        {
            EditorGUILayout.HelpBox(
                "当前 Driver 不读取 MotionProfile，但资产仍保留引用；请确认后解除，避免误判权威。",
                MessageType.Warning);
        }

        switch (action.MotionDriverMode)
        {
            case ActionMotionDriverMode.InheritStateMotor:
                EditorGUILayout.HelpBox("合法无 MP：Action 继续使用当前 Grounded/Airborne 基础 Motor。", MessageType.None);
                break;
            case ActionMotionDriverMode.ClipRootMotion:
                EditorGUILayout.HelpBox("请同时检查 MainClip Importer 的 Root Transform 设置；本模式不读取 MP。", MessageType.Warning);
                break;
            case ActionMotionDriverMode.Stationary:
                EditorGUILayout.HelpBox("入口清理平面速度；期间仍维护重力、垂直速度和接地。JumpStart/RunStart 不应使用。", MessageType.Warning);
                break;
        }
    }

    static void DrawLegacyMigrationButton(ActionDataSO action, ActionMotionExecutionPlan plan)
    {
        var target = plan.EffectiveMode == ActionMotionDriverMode.LegacyAuto
            && action.IntentCategory == ActionIntentCategory.Locomotion
            ? ActionMotionDriverMode.InheritStateMotor
            : plan.EffectiveMode;
        if (target == ActionMotionDriverMode.LegacyAuto) return;

        if (GUILayout.Button($"迁移为显式 {target}"))
        {
            Undo.RecordObject(action, "Migrate Explicit Motion Driver");
            action.MotionDriverMode = target;
            EditorUtility.SetDirty(action);
        }
    }
}
#endif

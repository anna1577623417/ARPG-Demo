#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed partial class ActionDataInspector
{
    static bool s_stopFoldout = false; // 198.x — 默认收起，与 EnableStopFeature 同步默认禁用

    void DrawStopAuthoringSection(ActionDataSO action)
    {
        EditorGUILayout.Space(8f);

        var enableProp = serializedObject.FindProperty(nameof(ActionDataSO.EnableStopFeature));
        var stopEnabled = enableProp != null && enableProp.boolValue;

        // 198.x — Toggle-driven 展开：未启用时仅显示 toggle + 一句话提示，避免子字段污染 Inspector
        // 竖直布局：标题独占一行，Enable 独占一行；窄 Inspector 也不截断
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Stop Authoring (182.1)", EditorStyles.boldLabel);

            if (enableProp != null)
            {
                EditorGUILayout.PropertyField(enableProp,
                    new GUIContent("Enable Stop Feature",
                        "启用 = 本 Action 参与 Stop 系统；关闭 = 完全旁路保持旧行为"));
            }

            if (!stopEnabled)
            {
                EditorGUILayout.HelpBox(
                    "未启用 — 本 Action 不参与 Stop 系统（动画照常 / 无独立刹车曲线）。",
                    MessageType.None);
                return;
            }

            // 启用后展开子字段（用紧凑 Foldout 进一步控制可见性）
            EditorGUILayout.Space(2f);
            s_stopFoldout = EditorGUILayout.Foldout(s_stopFoldout, "▸ 展开 Stop 子配置", true);
            if (!s_stopFoldout)
            {
                var stratIdx = serializedObject.FindProperty(nameof(ActionDataSO.StopStrategy)).enumValueIndex;
                EditorGUILayout.HelpBox(
                    $"✓ 已启用,Strategy = {(StopStrategy)stratIdx}",
                    MessageType.None);
                return;
            }

            EditorGUI.indentLevel++;
            DrawProperty(nameof(ActionDataSO.StopStrategy));
            DrawStopStrategyFields(action, stopEnabled);
            EditorGUI.indentLevel--;
        }
    }

    void DrawStopStrategyFields(ActionDataSO action, bool stopEnabled)
    {
        var strategy = (StopStrategy)serializedObject
            .FindProperty(nameof(ActionDataSO.StopStrategy)).enumValueIndex;
        var mfReady = action.MotionProfile != null && action.MotionProfile.EnableStopAuthoring;

        DrawStopStrategyHelpBox(strategy);

        // 198.x — MotionProfile 字段不再二次渲染：直接复用顶部 Action 默认 Inspector 的 MotionProfile 字段。
        // 仅保留 Stop 关联性 warning。
        DrawMotionProfileStopWarning(action, strategy, stopEnabled);

        switch (strategy)
        {
            case StopStrategy.Snap:
                EditorGUILayout.HelpBox(
                    "Snap：不产生停止位移；RunEnd 动画按 Action.Duration + AnimSpeed 正常播放。",
                    MessageType.Info);
                break;

            case StopStrategy.InheritPhysics:
                if (!mfReady)
                {
                    EditorGUILayout.HelpBox(
                        "需在 MotionProfile 上勾选 EnableStopAuthoring；曲线仅表节奏，米数由速度→Distance 运行时决定。",
                        MessageType.Warning);
                }

                DrawProperty(nameof(ActionDataSO.ReferenceDuration));
                DrawInheritPhysicsSettings();
                DrawInheritPhysicsRuntimePreview(action);
                break;

            case StopStrategy.MotionProfile:
                if (!mfReady)
                {
                    EditorGUILayout.HelpBox(
                        "需在 MotionProfile 上勾选 EnableStopAuthoring；ZXY 曲线表作者米数（旧默认 Motion 行为）。",
                        MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "MotionProfile 策略：完整 ZXY 位移 + AnimSpeedCurve 仍生效；与未开 Stop 的 Motion Action 一致。",
                        MessageType.Info);
                }

                break;
        }
    }

    /// <summary>198.x — MotionProfile 字段在顶部 Inspector 已渲染，本处仅做策略关联性提示。</summary>
    void DrawMotionProfileStopWarning(ActionDataSO action, StopStrategy strategy, bool stopEnabled)
    {
        if (!stopEnabled) return;

        if (strategy == StopStrategy.Snap)
        {
            EditorGUILayout.HelpBox(
                "Snap 策略：Stop 位移关闭；顶部 Motion Profile 引用可保留供其它用途。",
                MessageType.None);
            return;
        }

        if (action.MotionProfile == null)
        {
            EditorGUILayout.HelpBox(
                $"{strategy} 策略需要顶部 Motion Profile 字段配置一个 MotionProfileSO。",
                MessageType.Warning);
            return;
        }

        if (!action.MotionProfile.EnableStopAuthoring)
        {
            EditorGUILayout.HelpBox(
                "顶部 Motion Profile 未勾选 EnableStopAuthoring — InheritPhysics / MotionProfile 策略运行时将被跳过。",
                MessageType.Warning);
        }
    }

    void DrawInheritPhysicsSettings()
    {
        var inheritProp = serializedObject.FindProperty(nameof(ActionDataSO.InheritPhysics));
        if (inheritProp == null)
        {
            return;
        }

        EditorGUILayout.HelpBox(
            "速度参考（与 StatType 系统对齐，单位 m/s）：\n" +
            "  · StatType.WalkSpeed 默认 5 m/s\n" +
            "  · StatType.RunSpeed  默认 8 m/s\n" +
            "入场速度处于 MinSpeed→MaxSpeed 之间时，距离/时长按线性插值。",
            MessageType.None);

        EditorGUILayout.LabelField("入场速度区间 (m/s)", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.MinSpeed)),
            new GUIContent("最小入场速度", "玩家速度 ≤ 此值 → 按最短滑行；参考 WalkSpeed × 0.2 ≈ 1"));
        EditorGUILayout.PropertyField(inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.MaxSpeed)),
            new GUIContent("最大入场速度", "玩家速度 ≥ 此值 → 按最长滑行；参考 RunSpeed ≈ 8"));

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("滑行距离区间 (m)", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.MinDistance)),
            new GUIContent("最短滑行距离", "约 1/3 步 ≈ 0.2m"));
        EditorGUILayout.PropertyField(inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.MaxDistance)),
            new GUIContent("最长滑行距离", "约 4 步 ≈ 2.5m"));

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("滑行时长区间 (s)", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.MinDuration)),
            new GUIContent("最短滑行时长"));
        EditorGUILayout.PropertyField(inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.MaxDuration)),
            new GUIContent("最长滑行时长", "典型急停 0.3~0.6 秒"));

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("位移轴向", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.AffectX)),
            new GUIContent("X · 左右"));
        EditorGUILayout.PropertyField(inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.AffectY)),
            new GUIContent("Y · 垂直", "一般不勾选"));
        EditorGUILayout.PropertyField(inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.AffectZ)),
            new GUIContent("Z · 前后", "Run/Walk 急停默认勾选"));
    }

    static void DrawInheritPhysicsRuntimePreview(ActionDataSO action)
    {
        if (action == null || !action.EnableStopFeature)
        {
            return;
        }

        const float previewSpeed = 6f;
        var ctx = StopMotionRuntime.Build(action, action.MotionProfile, previewSpeed);
        if (!ctx.IsActive)
        {
            return;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.FloatField("Preview Entry Speed (m/s)", previewSpeed);
            EditorGUILayout.FloatField("runtimeDuration @ preview", ctx.RuntimeDuration);
            EditorGUILayout.FloatField("runtimeDistance @ preview", ctx.RuntimeDistance);
            EditorGUILayout.FloatField("baseAnimSpeed @ preview", ctx.BaseAnimSpeed);
        }
    }

    static void DrawStopStrategyHelpBox(StopStrategy strategy)
    {
        var msg = strategy switch
        {
            StopStrategy.Snap => "立即清零位移；动画照常播完，不跳过 Clip。",
            StopStrategy.InheritPhysics =>
                "速度→Distance/Duration 动态映射；MotionProfile 曲线表节奏；baseAnimSpeed = ReferenceDuration/runtimeDuration。",
            StopStrategy.MotionProfile => "固定作者位移：MotionProfile 完整接管 ZXY 米数与节奏（旧默认）。",
            _ => string.Empty,
        };

        if (!string.IsNullOrEmpty(msg))
        {
            EditorGUILayout.HelpBox(msg, MessageType.None);
        }
    }
}
#endif

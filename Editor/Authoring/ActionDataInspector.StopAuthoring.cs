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
        DrawStopPresentationSettings();

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
                        "需在 MotionProfile 上勾选 EnableStopAuthoring。曲线只表表现节奏，米数由 v_entry 与制动积分决定。",
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

                // TapStop 配置属于 Action 的 Stop Authoring 数据，不随 IP/MP 位移策略切换；Snap 才隐藏。
                DrawTapStopSettings(
                    serializedObject.FindProperty(nameof(ActionDataSO.InheritPhysics)),
                    "点按 TapStop 配置（IP / MP 共用）");

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

    /// <summary>
    /// ActionData 制作页上的 MotionProfile 只读预览。引用可改，曲线与 EnableStopAuthoring 不可在此改写。
    /// </summary>
    void DrawMotionProfileReadOnlyPreview(ActionDataSO action)
    {
        var profile = action != null ? action.MotionProfile : null;
        if (profile == null)
        {
            return;
        }

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("MotionProfile 只读预览", EditorStyles.miniBoldLabel);
            var lockMeters = action.EnableStopFeature
                && (action.StopStrategy == StopStrategy.InheritPhysics
                    || action.StopStrategy == StopStrategy.Snap);
            EditorGUILayout.HelpBox(
                lockMeters
                    ? "InheritPhysics / Snap：ZXY 与 Scale 只作节奏预览。停止米数在本 Action 的 D_ref / 积分，不在 MP Scale。"
                    : "此处灰显，避免在 Action 页改写 MotionProfile。要改曲线请打开 MP 资产。",
                MessageType.None);

            var profileSo = new SerializedObject(profile);
            profileSo.Update();
            var oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Enable Stop Authoring", profile.EnableStopAuthoring);
                var axisProp = profileSo.FindProperty(nameof(MotionProfileSO.AxisCurves));
                if (axisProp != null)
                {
                    EditorGUILayout.PropertyField(
                        axisProp,
                        new GUIContent("XYZ 局部空间位置曲线"),
                        true);
                }
            }

            GUI.color = oldColor;

            if (GUILayout.Button("打开 MotionProfile 资产", EditorStyles.miniButton))
            {
                Selection.activeObject = profile;
                EditorGUIUtility.PingObject(profile);
            }
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
            "234.6.3：Loop 封顶积分，v0=min(v,V_max)。点按丢掉入场速度，用 D_tap 铺满尾段（v0=2D/T）。连点默认不进 RunStart。\n" +
            "点按起播/尾部用下方 Segment 拖条。两端 0 = Auto（最后 T_tap 秒）。与 Timeline Time Authority 的 Clip Segment 互不影响。",
            MessageType.None);

        EditorGUILayout.LabelField("输入字段", EditorStyles.miniBoldLabel);
        var tuningModeProp = inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.ContinuousTuningMode));
        var durationProp = inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.FullSpeedStopDuration));
        if (tuningModeProp != null)
        {
            EditorGUILayout.PropertyField(
                tuningModeProp,
                new GUIContent("连续 Stop 主调参", "FullSpeedDistance 保持旧距离语义；FullSpeedDuration 用满速收停时间反推 D_ref"));
        }
        var tuningMode = tuningModeProp != null
            ? (ContinuousStopTuningMode)tuningModeProp.enumValueIndex
            : ContinuousStopTuningMode.FullSpeedDistance;
        if (tuningMode == ContinuousStopTuningMode.FullSpeedDistance)
        {
            EditorGUILayout.PropertyField(
                inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.FullSpeedStopDistance)),
                new GUIContent("满速停止距离 D_ref", "积分标定。0 = 未填，运行时回退 MaxDistance"));
        }
        else if (durationProp != null)
        {
            EditorGUILayout.PropertyField(
                durationProp,
                new GUIContent("满速停止时间 T_ref", "PhysicsStop 下作为满速标定；运行时 D_ref=0.5×V_ref×T_ref"));
            var maxSpeedProp = inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.MaxSpeed));
            var vRef = maxSpeedProp != null ? Mathf.Max(0f, maxSpeedProp.floatValue) : 0f;
            var tRef = Mathf.Max(0f, durationProp.floatValue);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField("推导 D_ref（按 Editor V_max）", 0.5f * vRef * tRef);
            }
        }
        EditorGUILayout.PropertyField(inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.MaxSpeed)),
            new GUIContent("参考满速 V_max 回退", "Play 使用松开时的 WalkSpeed/RunSpeed；此处仅 Editor 未传 gait 时回退"));
        EditorGUILayout.PropertyField(inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.MaxDistance)),
            new GUIContent("D_ref 回退 MaxDistance", "仅 FullSpeedStopDistance 未填时使用"));
        EditorGUILayout.PropertyField(inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.MaxBrakeSeconds)),
            new GUIContent("最大刹车时间 T_max", "0 = 运行时用 2·D_ref/V_max。只约束 Loop 物理段"));

        DrawTapStopSettings(inheritProp, "点按 TapStop 配置（IP / MP 共用）");

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("位移轴向", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.AffectX)),
            new GUIContent("X · 左右"));
        EditorGUILayout.PropertyField(inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.AffectY)),
            new GUIContent("Y · 垂直", "一般不勾选"));
        EditorGUILayout.PropertyField(inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.AffectZ)),
            new GUIContent("Z · 前后", "Run/Walk 急停默认勾选"));
    }

    static void DrawTapStopSettings(SerializedProperty inheritProp, string heading)
    {
        if (inheritProp == null)
        {
            return;
        }

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField(heading, EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(
            inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.TapWindowSeconds)),
            new GUIContent("点按判定窗", "heldSec≤此值走 Tap。0 = 0.15"));
        EditorGUILayout.PropertyField(
            inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.TapPresentationSeconds)),
            new GUIContent("点按表现租约 T_tap", "Tap 不吃满段 Clip 墙钟。0 = 0.15"));
        EditorGUILayout.PropertyField(
            inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.TapStopDistance)),
            new GUIContent("点按固定位移", "与入场速度无关。0 = 运行时 0.1m"));
        StopTapTailSegmentEditor.Draw(
            inheritProp.serializedObject,
            inheritProp,
            inheritProp.serializedObject.targetObject as ActionDataSO);
        DrawTapChainUnlimitedToggle(inheritProp);
    }

    static void DrawTapChainUnlimitedToggle(SerializedProperty inheritProp)
    {
        var unlimitedProp = inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.TapChainUnlimited));
        var maxProp = inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.TapChainMax));
        if (unlimitedProp == null || maxProp == null)
        {
            return;
        }

        var displayUnlimited = unlimitedProp.boolValue || maxProp.intValue <= 0;
        EditorGUI.BeginChangeCheck();
        var nextUnlimited = EditorGUILayout.Toggle(
            new GUIContent(
                "无限连点",
                "开启后忽略连点最大发。关闭后采用最大发：1 = 仅首发。旧资产 max=0 且未写本字段时运行时仍无限。"),
            displayUnlimited);
        if (EditorGUI.EndChangeCheck())
        {
            unlimitedProp.boolValue = nextUnlimited;
            if (!nextUnlimited && maxProp.intValue <= 0)
            {
                maxProp.intValue = 1;
            }
        }

        using (new EditorGUI.DisabledScope(unlimitedProp.boolValue || maxProp.intValue <= 0))
        {
            EditorGUILayout.PropertyField(
                maxProp,
                new GUIContent("连点最大发数", "仅无限连点关闭时采用。1 = 仅首发。0 对既有资产仍视为无限。"));
        }
    }

    void DrawStopPresentationSettings()
    {
        var presentationProp = serializedObject.FindProperty(nameof(ActionDataSO.StopPresentation));
        if (presentationProp == null)
        {
            return;
        }

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Stop 时钟与动画同步（238.1）", EditorStyles.miniBoldLabel);
        var durationProp = presentationProp.FindPropertyRelative(nameof(StopPresentationSettings.DurationAuthority));
        var animSpeedProp = presentationProp.FindPropertyRelative(nameof(StopPresentationSettings.AnimSpeedAuthority));
        var fixedSpeedProp = presentationProp.FindPropertyRelative(nameof(StopPresentationSettings.FixedAnimSpeed));
        var strictProp = presentationProp.FindPropertyRelative(nameof(StopPresentationSettings.RequireSynchronization));
        if (durationProp != null)
        {
            EditorGUILayout.PropertyField(durationProp, new GUIContent("Stop 时长权威"));
        }
        if (animSpeedProp != null)
        {
            EditorGUILayout.PropertyField(animSpeedProp, new GUIContent("End 动画速率权威"));
        }
        if (fixedSpeedProp != null && animSpeedProp != null
            && (StopAnimSpeedAuthority)animSpeedProp.enumValueIndex == StopAnimSpeedAuthority.FixedOverride)
        {
            EditorGUILayout.PropertyField(
                fixedSpeedProp,
                new GUIContent("固定 End AnimSpeed", "仅 FixedOverride 使用；严格同步失败时运行时回退 AutoFit"));
        }
        if (strictProp != null)
        {
            EditorGUILayout.PropertyField(
                strictProp,
                new GUIContent("要求严格同步", "仅 PhysicsStop + AutoFit/FixedOverride 进入严格同步门禁"));
        }
    }

    static void DrawInheritPhysicsRuntimePreview(ActionDataSO action)
    {
        if (action == null || !action.EnableStopFeature)
        {
            return;
        }

        const float previewSpeed = 6f;
        var inherit = action.InheritPhysics;
        var vRef = inherit.MaxSpeed > 0.01f ? inherit.MaxSpeed : 8f;
        var ctx = StopMotionRuntime.Build(action, action.MotionProfile, previewSpeed, vRef);
        if (!ctx.IsActive)
        {
            return;
        }

        EditorGUILayout.LabelField("积分推导（只读，不写进资产）", EditorStyles.miniBoldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.FloatField("Preview Entry Speed (Editor only)", previewSpeed);
            EditorGUILayout.FloatField("a (m/s²)", ctx.BrakeDeceleration);
            EditorGUILayout.FloatField("predictedDistance", ctx.RuntimeDistance);
            EditorGUILayout.FloatField("physicsDuration", ctx.PhysicsDuration);
            EditorGUILayout.Toggle("physicsDuration capped", ctx.PhysicsDurationCapped);
            EditorGUILayout.FloatField("effectiveActionDuration", ctx.EffectiveActionDuration);
            EditorGUILayout.FloatField("clipWindowWallSeconds", ctx.ClipWindowWallSeconds);
            EditorGUILayout.FloatField("baseAnimSpeed @ preview", ctx.BaseAnimSpeed);
            EditorGUILayout.TextField("durationAuthority", ctx.DurationAuthority.ToString());
            EditorGUILayout.TextField("animSpeedAuthority", ctx.AnimSpeedAuthority.ToString());
            EditorGUILayout.TextField("syncResult", ctx.SyncResult.ToString());
            EditorGUILayout.FloatField("syncDeltaSeconds", ctx.SyncDeltaSeconds);
            EditorGUILayout.Toggle("D_ref 来自 MaxDistance 回退", ctx.DerivedFromLegacyMaxDistance);
            var tapCtx = StopMotionRuntime.Build(
                action,
                action.MotionProfile,
                previewSpeed,
                vRef,
                default,
                StopSessionTier.MicroTap);
            EditorGUILayout.FloatField("Tap D (runtime default 0.1)", tapCtx.RuntimeDistance);
            EditorGUILayout.FloatField("Tap v0 = 2D/T", tapCtx.RemainingSpeed);
            EditorGUILayout.FloatField("Tap T_lease", tapCtx.RuntimeDuration);
            EditorGUILayout.FloatField("Tap AnimSpeed", tapCtx.BaseAnimSpeed);
            EditorGUILayout.FloatField("Tap Clip startNt", tapCtx.PresentationStartNormalized);
            EditorGUILayout.TextField("Tap tailMode", tapCtx.AuthorTail ? "Author" : "Auto");
        }

        if (ctx.SyncResult == StopSyncResult.Rejected)
        {
            EditorGUILayout.HelpBox(
                $"严格同步拒绝：有效时长 {ctx.EffectiveActionDuration:F3}s，Clip 窗口 {ctx.ClipWindowWallSeconds:F3}s，" +
                $"最终差值 {ctx.SyncDeltaSeconds:F4}s。运行时会回退 AutoFit；请调整 FixedOverride 或 Stop 时长。",
                MessageType.Warning);
        }

        DrawInheritPhysicsGaitTable(action, vRef);
    }

    static void DrawInheritPhysicsGaitTable(ActionDataSO action, float vRef)
    {
        if (action == null || vRef <= 0.01f)
        {
            return;
        }

        EditorGUILayout.LabelField("gait 比例推导 D/T（只读）", EditorStyles.miniBoldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            DrawGaitRow(action, vRef, 0.25f);
            DrawGaitRow(action, vRef, 0.50f);
            DrawGaitRow(action, vRef, 0.75f);
            DrawGaitRow(action, vRef, 1.00f);
        }
    }

    static void DrawGaitRow(ActionDataSO action, float vRef, float ratio)
    {
        var speed = vRef * ratio;
        var ctx = StopMotionRuntime.Build(action, action.MotionProfile, speed, vRef);
        var physT = StopIntegrator.PredictDuration(speed, ctx.BrakeDeceleration);
        EditorGUILayout.LabelField(
            $"{ratio:0.00}×gait  v={speed:F2}",
            ctx.IsActive ? $"D={ctx.RuntimeDistance:F3}m  Tphys={physT:F3}s" : "inactive");
    }

    static void DrawStopStrategyHelpBox(StopStrategy strategy)
    {
        var msg = strategy switch
        {
            StopStrategy.Snap => "立即清零位移；动画照常播完，不跳过 Clip。",
            StopStrategy.InheritPhysics =>
                "234.6.3：Loop 封顶积分；点按丢掉余速，D_tap 铺满尾段；无限连点 Toggle 开则忽略最大发。",
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

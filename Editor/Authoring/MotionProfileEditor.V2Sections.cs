#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 174.2 — MotionProfile V2 七小节 Inspector：每节一个 Foldout + HelpBox 功能说明。
/// 字段已从 DrawPropertiesExcluding 排除，仅在此渲染。
/// </summary>
public sealed partial class MotionProfileEditor
{
    public void DrawV2Sections(MotionProfileSO profile)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("V2 · 动作导演层（174.2）", EditorStyles.boldLabel);

        DrawV2PhysicsWeightSection();
        DrawV2RotationSection();
        DrawV2ControlSection();
        DrawV2TargetTrackingSection();
        DrawV2RootMotionBlendSection();
        DrawV2HitstopSection();
        DrawV2RuntimeStrategySection();
    }

    // ── ① Physics Weight ─────────────────────────────────────────
    void DrawV2PhysicsWeightSection()
    {
        s_v2PhysicsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            s_v2PhysicsFoldout, "▸ V2 · Physics Weight / 重力权重");
        if (s_v2PhysicsFoldout)
        {
            EditorGUILayout.HelpBox(
                "控制本动作期间「重力」的参与程度。\n" +
                "• DefaultPolicy（默认）→ 不启用 V2 曲线，沿用 YAxis 那一组旧 GravityMode 三档（Suspend / Use / Additive）。\n" +
                "• Curve → 按 V2GravityWeight 曲线连续加权：0=失重 / 1=正常 / >1=强化下坠。\n" +
                "典型用例：上升段 0→末段 >1 = 跳劈；中段 0 = 升龙 hover；全程 0.2 = 滑翔。",
                MessageType.Info);

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(nameof(MotionProfileSO.V2GravityWeightMode)),
                new GUIContent("Gravity Weight Mode"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(nameof(MotionProfileSO.V2GravityWeight)),
                new GUIContent("Gravity Weight Curve"));
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // ── ② Rotation ───────────────────────────────────────────────
    void DrawV2RotationSection()
    {
        s_v2RotationFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            s_v2RotationFoldout, "▸ 210.5 · Action Yaw / 水平旋转（表现层）");
        if (s_v2RotationFoldout)
        {
            EditorGUILayout.HelpBox(
                "Action 期绕 Y 轴的朝向偏移（相对起手 forward）。\n" +
                "• None — 不参与 Action Yaw（默认）。\n" +
                "• Constant — 恒为 Start 角度；End 锁定只读。\n" +
                "• Curve — Start° → End°，Yaw Blend 曲线 0→1 为插值进度。\n" +
                "【重要】Yaw 不改变 MP 位移轨迹；位移请编辑 XYZ 曲线 + Motion Space。",
                MessageType.Info);

            var policyProp = serializedObject.FindProperty(nameof(MotionProfileSO.YawPolicy));
            EditorGUILayout.PropertyField(policyProp, new GUIContent("Yaw Policy"));

            var policy = (YawPolicyMode)policyProp.enumValueIndex;
            if (policy != YawPolicyMode.None)
            {
                var startProp = serializedObject.FindProperty(nameof(MotionProfileSO.YawStartDegrees));
                EditorGUILayout.PropertyField(startProp, new GUIContent("Yaw Start (deg)"));

                if (policy == YawPolicyMode.Constant)
                {
                    serializedObject.FindProperty(nameof(MotionProfileSO.YawEndDegrees)).floatValue =
                        startProp.floatValue;
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.FloatField(
                            new GUIContent("Yaw End (deg)"),
                            startProp.floatValue);
                    }
                }
                else
                {
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty(nameof(MotionProfileSO.YawEndDegrees)),
                        new GUIContent("Yaw End (deg)"));
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty(nameof(MotionProfileSO.YawBlendOverTime)),
                        new GUIContent("Yaw Blend Over Time"));
                }
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // ── ③ Control ────────────────────────────────────────────────
    void DrawV2ControlSection()
    {
        s_v2ControlFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            s_v2ControlFoldout, "▸ V2 · Control / 玩家输入权重");
        if (s_v2ControlFoldout)
        {
            EditorGUILayout.HelpBox(
                "动作期间「玩家输入」对角色的影响程度（0=完全锁 / 1=完全跟随）。\n" +
                "• Facing Input Weight → 输入对角色「朝向」的影响。默认 1，前摇结束后压到 0 = 锁朝向。\n" +
                "• Move Input Weight → 输入对「位移」的叠加权重。默认 0 = 动作期间不允许移动；空中翻滚可改为 0.3。\n" +
                "曲线值按归一化 motionT 采样。",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Facing + Move → 全 0", GUILayout.Height(22f)))
                {
                    ApplyControlInputWeightPreset(facing: 0f, move: 0f);
                }

                if (GUILayout.Button("Facing + Move → 全 1", GUILayout.Height(22f)))
                {
                    ApplyControlInputWeightPreset(facing: 1f, move: 1f);
                }

                if (GUILayout.Button("默认 (Facing=1, Move=0)", GUILayout.Height(22f)))
                {
                    ApplyControlInputWeightPreset(facing: 1f, move: 0f);
                }
            }

            DrawControlWeightCurveRow(
                "Facing Input Weight",
                nameof(MotionProfileSO.V2FacingInputWeight));
            DrawControlWeightCurveRow(
                "Move Input Weight",
                nameof(MotionProfileSO.V2MoveInputWeight));
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    void DrawControlWeightCurveRow(string label, string propName)
    {
        var prop = serializedObject.FindProperty(propName);
        if (prop == null)
        {
            return;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(new GUIContent(label));
        if (GUILayout.Button("0", GUILayout.Width(28f)))
        {
            SetControlWeightCurve(propName, 0f);
        }

        if (GUILayout.Button("1", GUILayout.Width(28f)))
        {
            SetControlWeightCurve(propName, 1f);
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.PropertyField(prop, GUIContent.none);
    }

    void SetControlWeightCurve(string propName, float value)
    {
        var profile = (MotionProfileSO)target;
        var prop = serializedObject.FindProperty(propName);
        if (profile == null || prop == null)
        {
            return;
        }

        Undo.RecordObject(profile, $"Control Weight → {value:F0}: {propName}");
        prop.animationCurveValue = AnimationCurve.Constant(0f, 1f, value);
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(profile);
    }

    void ApplyControlInputWeightPreset(float facing, float move)
    {
        var profile = (MotionProfileSO)target;
        if (profile == null)
        {
            return;
        }

        Undo.RecordObject(profile, $"Control Weights F={facing:F0} M={move:F0}");
        serializedObject.FindProperty(nameof(MotionProfileSO.V2FacingInputWeight)).animationCurveValue =
            AnimationCurve.Constant(0f, 1f, facing);
        serializedObject.FindProperty(nameof(MotionProfileSO.V2MoveInputWeight)).animationCurveValue =
            AnimationCurve.Constant(0f, 1f, move);
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(profile);
    }

    // ── ④ Target Tracking ────────────────────────────────────────
    void DrawV2TargetTrackingSection()
    {
        s_v2TrackingFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            s_v2TrackingFoldout, "▸ V2 · Target Tracking / 锁定追踪");
        if (s_v2TrackingFoldout)
        {
            EditorGUILayout.HelpBox(
                "仅当 MotionSpace = LockTarget 时生效。\n" +
                "• 0 → 锁定起手时的方向，之后不再修正（适合一次性投掷）。\n" +
                "• 1 → 每帧重新对准目标（适合追踪斩、磁力突刺）。\n" +
                "曲线让追踪强度随时间渐变：前摇高、末段降为 0 = 蓄力出招瞬间方向不再改变。",
                MessageType.Info);

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(nameof(MotionProfileSO.V2TargetTrackingWeight)),
                new GUIContent("Target Tracking Weight"));
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // ── ⑤ Animator Root Motion Blend ─────────────────────────────
    void DrawV2RootMotionBlendSection()
    {
        s_v2RootMotionBlendFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            s_v2RootMotionBlendFoldout, "▸ V2 · Animator Root Motion Blend / 与原生 RM 混合");
        if (s_v2RootMotionBlendFoldout)
        {
            EditorGUILayout.HelpBox(
                "MotionProfile 位移 与 Animator Root Motion 的混合权重。\n" +
                "• 0 → 纯 MotionProfile 驱动（默认，本框架推荐路径）。\n" +
                "• 1 → 纯 Animator Root Motion（让动画师手作的位移完全接管）。\n" +
                "• 中间值 → 两者按权重融合。用于「外购动画 RootMotion 大致正确，只想微调」的情形。",
                MessageType.Info);

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(nameof(MotionProfileSO.V2RootMotionBlend)),
                new GUIContent("Root Motion Blend"));
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // ── ⑥ Hitstop Response ───────────────────────────────────────
    void DrawV2HitstopSection()
    {
        s_v2HitstopFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            s_v2HitstopFoldout, "▸ V2 · Hitstop Response / 受顿挫响应");
        if (s_v2HitstopFoldout)
        {
            EditorGUILayout.HelpBox(
                "本动作对外部 Hitstop（命中顿挫）事件的响应倍率。\n" +
                "• 0 → Hitstop 不影响本动作（适合 BackStep 闪避，不要被命中卡住）。\n" +
                "• 1 → 默认，1× 减速。\n" +
                "• >1 → 放大顿挫感（终结技 / 大招专用，1.5~2.0）。\n" +
                "注意：本字段不主动产生 Hitstop，仅响应。Hitstop 的产生在 Combat 层。",
                MessageType.Info);

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(nameof(MotionProfileSO.V2HitstopMultiplier)),
                new GUIContent("Hitstop Multiplier"));
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // ── ⑦ Runtime Strategy ───────────────────────────────────────
    void DrawV2RuntimeStrategySection()
    {
        s_v2StrategyFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            s_v2StrategyFoldout, "▸ V2 · Runtime Strategy / 高级 Y 策略");
        if (s_v2StrategyFoldout)
        {
            EditorGUILayout.HelpBox(
                "Y 轴运行时塑形高级策略（叠加在 YMotion 之上）。\n" +
                "• Default → 沿用 YMotionMode 行为（默认）。\n" +
                "• HoverHold → 升龙 hover：Y 到达峰值后保持高度直到曲线尾段（DMC 升龙斩、悬停斩）。\n" +
                "• ApexSnap → 即时锚定 ApexHeight，曲线归一化映射到「起手高度 ↔ ApexHeight」（保证跳跃绝对高度）。",
                MessageType.Info);

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(nameof(MotionProfileSO.V2YStrategy)),
                new GUIContent("Y Strategy"));
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }
}
#endif

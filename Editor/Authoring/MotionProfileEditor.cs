#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MotionProfileSO))]
public sealed partial class MotionProfileEditor : Editor
{
    private CurvePresetType _preset = CurvePresetType.EaseInOut;
    private float _power = 2f;
    private float _defaultForwardMeters = 0f;

    static bool s_yAxisFoldout;
    static bool s_landingFoldout;
    static bool s_animSpeedFoldout = true;
    static bool s_clipExtractFoldout;
    static bool s_clipExtractAdvancedFoldout;
    static bool s_perAxisClipFoldout;
    static bool s_scenePreviewFoldout;
    static bool s_wholeCurveFoldout;

    MotionCurveFitMode _fitMode = MotionCurveFitMode.CatmullRom;
    MotionCurveFilterMode _filterMode = MotionCurveFilterMode.MovingAverage;
    int _filterWindow = 5;
    float _errorTolerance = 0.01f;
    MotionExtractMode _extractMode = MotionExtractMode.Auto;
    string _sampleBonePath = "Hips";
    int _samplingFps = 30;

    static readonly string[] s_excludeCustom =
    {
        "m_Script",
        "SourceClip",
        "XSourceClip",
        "YSourceClip",
        "ZSourceClip",
        "XExtractSource",
        "YExtractSource",
        "ZExtractSource",
        "LandingOffset",
        "LandingCurve",
        "LandingDetectionRadius",
        "YMotion",
        "Gravity",
        "GroundConstraint",
        "yAxisV2Configured",
        "legacyYPolicyRaw",
        "AnimSpeedMode",
        "SpeedOverTime",
        "BurstDurationSeconds",
        "LegacyConstantPlanarSpeed",
        "UsePlanarVelocityShape",
        "PlanarVelocityMultiplier",
        "PlanarPeakSpeed",
        // 174.2 V2 七小节 —— 各自折叠组渲染
        nameof(MotionProfileSO.V2GravityWeightMode),
        nameof(MotionProfileSO.V2GravityWeight),
        nameof(MotionProfileSO.YawPolicy),
        nameof(MotionProfileSO.YawStartDegrees),
        nameof(MotionProfileSO.YawEndDegrees),
        nameof(MotionProfileSO.YawBlendOverTime),
        nameof(MotionProfileSO.V2FacingInputWeight),
        nameof(MotionProfileSO.V2MoveInputWeight),
        nameof(MotionProfileSO.V2TargetTrackingWeight),
        nameof(MotionProfileSO.V2RootMotionBlend),
        nameof(MotionProfileSO.V2HitstopMultiplier),
        nameof(MotionProfileSO.V2YStrategy),
    };

    static bool s_v2PhysicsFoldout;
    static bool s_v2RotationFoldout;
    static bool s_v2ControlFoldout;
    static bool s_v2TrackingFoldout;
    static bool s_v2RootMotionBlendFoldout;
    static bool s_v2HitstopFoldout;
    static bool s_v2StrategyFoldout;

    void OnDisable()
    {
        // 204.1 W1：Inspector 失焦 / 切换 / 销毁时兜底清空 Scene 折线，避免 sticky
        if (MotionPathGizmoDrawer.IsPreviewing(target as MotionProfileSO))
        {
            MotionPathGizmoDrawer.ClearPreviewProfile();
        }
    }

    // ───────────────────────────────────────────────────────────────────
    // 204.1 W8：MF → Action 反向导航（与 Action → MF 的"传递 MainClip"对偶）
    // ───────────────────────────────────────────────────────────────────

    ActionDataSO[] _referencingActions;
    MotionProfileSO _referenceCachedFor;

    void DrawActionNavigationSection(MotionProfileSO profile)
    {
        EnsureReferencingActionsCache(profile);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    $"被引用：{_referencingActions.Length} 个 Action",
                    EditorStyles.boldLabel);
                if (GUILayout.Button("刷新", EditorStyles.miniButton, GUILayout.Width(48f)))
                {
                    _referenceCachedFor = null;
                    EnsureReferencingActionsCache(profile);
                }
            }

            if (_referencingActions.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "工程中没有 Action 引用此 MotionProfile。",
                    MessageType.Info);
            }
            else
            {
                foreach (var action in _referencingActions)
                {
                    if (action == null) continue;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(true))
                        {
                            EditorGUILayout.ObjectField(action, typeof(ActionDataSO), false);
                        }

                        if (GUILayout.Button("跳转", EditorStyles.miniButton, GUILayout.Width(50f)))
                        {
                            Selection.activeObject = action;
                            EditorGUIUtility.PingObject(action);
                        }
                    }
                }
            }
        }

        EditorGUILayout.Space(4f);
    }

    void EnsureReferencingActionsCache(MotionProfileSO profile)
    {
        if (_referenceCachedFor == profile && _referencingActions != null) return;
        _referenceCachedFor = profile;
        _referencingActions = FindActionsReferencing(profile);
    }

    static ActionDataSO[] FindActionsReferencing(MotionProfileSO profile)
    {
        if (profile == null) return System.Array.Empty<ActionDataSO>();

        var guids = AssetDatabase.FindAssets("t:ActionDataSO");
        var list = new System.Collections.Generic.List<ActionDataSO>(8);
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var action = AssetDatabase.LoadAssetAtPath<ActionDataSO>(path);
            if (action != null && action.MotionProfile == profile)
            {
                list.Add(action);
            }
        }
        return list.ToArray();
    }

    public override void OnInspectorGUI()
    {
        var profile = (MotionProfileSO)target;
        serializedObject.Update();

        // 204.1 W8：反向跳转 — Action ⇄ MotionProfile 双向导航
        DrawActionNavigationSection(profile);

        EditorGUILayout.HelpBox(
            "【三轴位置曲线】局部空间：Evaluate(t)×Scale=米。\n" +
            "逻辑时长 / Clip 对齐 → Action Time Authority；本页 SpeedOverTime 仅作 motionT 局部节奏（先慢后快等）。",
            MessageType.Info);

        DrawStopAuthoringSection(profile);

        if (!profile.UsesAxisCurves && !profile.UsesGroundTargetedLanding)
        {
            EditorGUILayout.HelpBox(
                "未配置 AxisCurves — 运行时位移为 0。请生成默认 Z 轴，或从 Clip 提取 / 手调曲线。",
                MessageType.Warning);

            _defaultForwardMeters = EditorGUILayout.FloatField("默认前进距离 (m)", _defaultForwardMeters);
            if (GUILayout.Button("生成默认 Z 轴 (0→1 × 距离)"))
            {
                Undo.RecordObject(profile, "Default Z Axis");
                profile.ApplyDefaultForwardAxis(_defaultForwardMeters);
                EditorUtility.SetDirty(profile);
            }
        }

        DrawPropertiesExcluding(serializedObject, s_excludeCustom);
        DrawV2Sections(profile);
        DrawYAxisSection(profile);
        DrawLandingSettingsSection(profile);
        DrawAnimationSpeedSection(profile);
        DrawClipExtractSection(profile);
        using (new EditorGUI.DisabledScope(ShouldLockAxisCurvesForStopPreview(profile)))
        {
            DrawManualCurveSection(profile);
        }
        MotionCurveSegmentPresetGUI.Draw(profile, serializedObject);
        DrawV2CurveLibrarySection();
        DrawScenePreviewSection(profile);
        DrawWholeCurveGenerationSection(profile);

        serializedObject.ApplyModifiedProperties();
    }

    void DrawYAxisSection(MotionProfileSO profile)
    {
        s_yAxisFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(s_yAxisFoldout, "Y Axis / 三权分离（Runtime）");
        if (!s_yAxisFoldout)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("YMotion"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Gravity"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("GroundConstraint"));

        var yMotion = (YMotionMode)serializedObject.FindProperty("YMotion").enumValueIndex;
        if (yMotion == YMotionMode.GroundTargeted)
        {
            EditorGUILayout.HelpBox("GroundTargeted：YCurve 不参与 Runtime，请在 Landing Settings 配置落地。", MessageType.Info);
        }
        else if (yMotion == YMotionMode.Curve && profile.GroundConstraint == GroundConstraintMode.ClampToGround)
        {
            EditorGUILayout.HelpBox(
                "翻滚/滑步推荐：Curve + UseGravity + ClampToGround — 保留 Y 蹬地感且接地不浮空。",
                MessageType.None);
        }

        if (EditorGUI.EndChangeCheck())
        {
            profile.SetYAxisV2Configured(true);
        }

        if (!serializedObject.FindProperty("yAxisV2Configured").boolValue)
        {
            EditorGUILayout.HelpBox(
                "yAxisV2Configured 未标记：修改上方 YMotion / Gravity / GroundConstraint 后将自动写入三权。",
                MessageType.Info);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    void DrawLandingSettingsSection(MotionProfileSO profile)
    {
        var usesLanding = profile.GetYAxisConfig().YMotion == YMotionMode.GroundTargeted;
        s_landingFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            s_landingFoldout,
            "Landing Settings（Ground Targeted · 可选）");
        if (!s_landingFoldout)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        using (new EditorGUI.DisabledScope(!usesLanding))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("LandingOffset"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("LandingCurve"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("LandingDetectionRadius"));
        }

        if (!usesLanding)
        {
            EditorGUILayout.HelpBox("将 Y Motion 设为 Ground Targeted 后生效。", MessageType.None);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    void DrawAnimationSpeedSection(MotionProfileSO profile)
    {
        s_animSpeedFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            s_animSpeedFoldout,
            "Animation Speed / 局部节奏");
        if (!s_animSpeedFoldout)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(MotionProfileSO.AnimSpeedMode)));
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(MotionProfileSO.SpeedOverTime)));
        EditorGUILayout.HelpBox(
            "finalClipSpeed = Action.AnimSpeed × SpeedOverTime(motionT)。\n" +
            "Constant 恒 1；Curve 按归一化 Motion 时间塑形段内节奏。\n" +
            "【171.7】SpeedOverTime 仅当绑定 Action 的 ClipAnimSpeedMode=Free 时参与运行时；AutoFitDuration 下忽略。",
            MessageType.None);
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    void DrawClipExtractSection(MotionProfileSO profile)
    {
        EditorGUILayout.Space(6f);
        s_clipExtractFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            s_clipExtractFoldout,
            "Clip → XYZ 位移提取（可选）");
        if (!s_clipExtractFoldout)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        profile.SourceClip = (AnimationClip)EditorGUILayout.ObjectField(
            "Source Clip",
            profile.SourceClip,
            typeof(AnimationClip),
            false);

        s_perAxisClipFoldout = EditorGUILayout.Foldout(s_perAxisClipFoldout, "按轴不同 Clip（可选）", true);
        if (s_perAxisClipFoldout)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                profile.XSourceClip = (AnimationClip)EditorGUILayout.ObjectField(
                    "X Source Clip",
                    profile.XSourceClip,
                    typeof(AnimationClip),
                    false);
                profile.YSourceClip = (AnimationClip)EditorGUILayout.ObjectField(
                    "Y Source Clip",
                    profile.YSourceClip,
                    typeof(AnimationClip),
                    false);
                profile.ZSourceClip = (AnimationClip)EditorGUILayout.ObjectField(
                    "Z Source Clip",
                    profile.ZSourceClip,
                    typeof(AnimationClip),
                    false);
            }
        }

        var hasClip = profile.SourceClip != null
            || profile.XSourceClip != null
            || profile.YSourceClip != null
            || profile.ZSourceClip != null;

        using (new EditorGUI.DisabledScope(!hasClip))
        {
            if (GUILayout.Button("一键提取 XYZ From Source Clip", GUILayout.Height(24f)))
            {
                RunClipExtract(profile);
            }
        }

        using (new EditorGUI.DisabledScope(profile == null))
        {
            if (GUILayout.Button("打开曲线编辑器（全屏）", GUILayout.Height(22f)))
            {
                MotionAxisCurveEditorWindow.Open(profile);
            }
        }

        DrawLastExtractReport();

        s_clipExtractAdvancedFoldout = EditorGUILayout.Foldout(
            s_clipExtractAdvancedFoldout,
            "提取高级选项",
            true);
        profile.XExtractSource = (MotionAxisExtractSource)EditorGUILayout.EnumPopup("X Source", profile.XExtractSource);
        profile.YExtractSource = (MotionAxisExtractSource)EditorGUILayout.EnumPopup("Y Source", profile.YExtractSource);
        profile.ZExtractSource = (MotionAxisExtractSource)EditorGUILayout.EnumPopup("Z Source", profile.ZExtractSource);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("预设：翻滚 X/Z=RootT Y=0", EditorStyles.miniButtonLeft))
            {
                profile.XExtractSource = MotionAxisExtractSource.RootT;
                profile.YExtractSource = MotionAxisExtractSource.ConstantZero;
                profile.ZExtractSource = MotionAxisExtractSource.RootT;
            }

            if (GUILayout.Button("预设：跳跃 Y=Bone", EditorStyles.miniButtonRight))
            {
                profile.XExtractSource = MotionAxisExtractSource.RootT;
                profile.YExtractSource = MotionAxisExtractSource.SampleBone;
                profile.ZExtractSource = MotionAxisExtractSource.RootT;
            }
        }

        if (s_clipExtractAdvancedFoldout)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                _extractMode = (MotionExtractMode)EditorGUILayout.EnumPopup("Extract Mode", _extractMode);
                _sampleBonePath = EditorGUILayout.TextField("Sample Bone Path", _sampleBonePath);
                _samplingFps = EditorGUILayout.IntSlider("Sample FPS", _samplingFps, 8, 120);
                _fitMode = (MotionCurveFitMode)EditorGUILayout.EnumPopup("Curve Fit Mode", _fitMode);
                _filterMode = (MotionCurveFilterMode)EditorGUILayout.EnumPopup("Filter Mode", _filterMode);
                _filterWindow = EditorGUILayout.IntSlider("Filter Window", _filterWindow, 5, 11);
                _errorTolerance = EditorGUILayout.Slider("Error Tolerance", _errorTolerance, 0.001f, 0.05f);
            }
        }

        EditorGUILayout.HelpBox(
            "Auto：优先 RootT 曲线（Humanoid RM）→ 骨骼 Hips → Animator RootMotion。\n" +
            "仅 RootT 模式可不选 Hierarchy；其余模式需选中 Rig（如 Armature）。",
            MessageType.None);
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    void RunClipExtract(MotionProfileSO profile)
    {
        var rig = Selection.activeGameObject;
        var needsRig = _extractMode is MotionExtractMode.SampleBone
            or MotionExtractMode.AnimatorRootMotion
            or MotionExtractMode.LegacyRootTransform;
        if (needsRig && rig == null)
        {
            EditorUtility.DisplayDialog(
                "Clip Extract",
                "请在 Hierarchy 选中预览 Rig（如 Armature），再提取。",
                "OK");
            return;
        }

        var clip = profile.SourceClip
            ?? profile.XSourceClip
            ?? profile.YSourceClip
            ?? profile.ZSourceClip;
        if (clip == null)
        {
            EditorUtility.DisplayDialog("Clip Extract", "请至少指定 Source Clip 或某一轴 Clip。", "OK");
            return;
        }

        var opt = ClipMotionExtractor.OptionsFromProfile(profile, ClipMotionExtractor.Options.Default);
        opt.ExtractMode = _extractMode;
        opt.SampleBonePath = _sampleBonePath;
        opt.SamplingFps = _samplingFps;
        opt.FitMode = _fitMode;
        opt.FilterMode = _filterMode;
        opt.FilterWindow = _filterWindow;
        opt.ErrorTolerance = _errorTolerance;
        ClipMotionExtractor.ExtractInto(clip, rig, profile, opt);
        EditorUtility.SetDirty(profile);
    }

    static void DrawLastExtractReport()
    {
        var report = ClipMotionExtractor.LastReport;
        if (string.IsNullOrEmpty(report.Message) && report.RawSamples <= 0)
        {
            return;
        }

        var msgType = report.Succeeded ? MessageType.Info : MessageType.Warning;
        var body = report.Succeeded
            ? $"src=({report.SourceX},{report.SourceY},{report.SourceZ}) raw={report.RawSamples} " +
              $"max=({report.MaxAbsMeters.x:F2},{report.MaxAbsMeters.y:F2},{report.MaxAbsMeters.z:F2})m " +
              $"keys=({report.KeysX},{report.KeysY},{report.KeysZ}) " +
              $"quality=({report.Quality.x:P0},{report.Quality.y:P0},{report.Quality.z:P0})"
            : report.Message;
        EditorGUILayout.HelpBox($"Last Extract: {body}", msgType);
    }

    void DrawScenePreviewSection(MotionProfileSO profile)
    {
        s_scenePreviewFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            s_scenePreviewFoldout,
            "Scene 预览轨迹（可选）");
        if (!s_scenePreviewFoldout)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        using (new EditorGUI.DisabledScope(!profile.UsesAxisCurves))
        {
            if (GUILayout.Button("Scene 预览轨迹（折线 Gizmo）"))
            {
                MotionPathGizmoDrawer.SetPreviewProfile(profile);
            }
        }

        if (!profile.UsesAxisCurves)
        {
            EditorGUILayout.HelpBox("需先配置 AxisCurves。", MessageType.Info);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    void DrawWholeCurveGenerationSection(MotionProfileSO profile)
    {
        EditorGUILayout.Space(4f);
        s_wholeCurveFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            s_wholeCurveFoldout,
            "整段曲线生成（可选 · 替换整条曲线）");
        if (!s_wholeCurveFoldout)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        _preset = (CurvePresetType)EditorGUILayout.EnumPopup("Preset", _preset);
        _power = EditorGUILayout.Slider("Power", _power, 1f, 6f);

        if (GUILayout.Button("Generate Z Curve (Forward)"))
        {
            Undo.RecordObject(profile, "Generate Z Curve");
            profile.AxisCurves.ZCurve = MotionCurveGenerator.Generate(_preset, _power);
            if (profile.AxisCurves.ZScale < 0.001f)
            {
                profile.AxisCurves.ZScale = _defaultForwardMeters;
            }

            EditorUtility.SetDirty(profile);
        }

        if (GUILayout.Button("Generate Speed Curve (局部节奏)"))
        {
            Undo.RecordObject(profile, "Generate Motion Speed Curve");
            profile.AnimSpeedMode = AnimSpeedMode.Curve;
            profile.SpeedOverTime = MotionCurveGenerator.Generate(_preset, _power);
            EditorUtility.SetDirty(profile);
        }

        EditorGUILayout.HelpBox(
            "【段预设】只改两 Key 之间切线；【整段生成】会替换整条曲线。",
            MessageType.Info);
        EditorGUILayout.EndFoldoutHeaderGroup();
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MotionProfileSO))]
public sealed class MotionProfileEditor : Editor
{
    private CurvePresetType _preset = CurvePresetType.EaseInOut;
    private float _power = 2f;
    private float _defaultForwardMeters = 4f;

    static bool s_yAxisFoldout;
    static bool s_landingFoldout;
    static bool s_timeAuthorityFoldout;
    static bool s_clipExtractFoldout;
    static bool s_scenePreviewFoldout;
    static bool s_wholeCurveFoldout;

    MotionCurveFitMode _fitMode = MotionCurveFitMode.Smooth;
    MotionCurveFilterMode _filterMode = MotionCurveFilterMode.MovingAverage;
    int _filterWindow = 5;
    float _errorTolerance = 0.01f;

    static readonly string[] s_excludeCustom =
    {
        "m_Script",
        "SourceClip",
        "LandingOffset",
        "LandingCurve",
        "LandingDetectionRadius",
        "YMotion",
        "Gravity",
        "GroundConstraint",
        "yAxisV2Configured",
        "legacyYPolicy",
        "UseActionDuration",
        "TimeSync",
        "Duration_AuthoringReference",
        "Distance_AuthoringReference",
        "AuthoringReferenceAnimSpeed",
        "MatchAnimationSpeed",
        "BurstDurationSeconds",
        "LegacyConstantPlanarSpeed",
        "UsePlanarVelocityShape",
        "PlanarVelocityMultiplier",
        "PlanarPeakSpeed",
    };

    public override void OnInspectorGUI()
    {
        var profile = (MotionProfileSO)target;
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "【三轴位置曲线】局部空间：Evaluate(t)×Scale=米。Logic Duration 为 Motion 唯一时间源（Use Action Duration）。",
            MessageType.Info);

        if (!profile.UsesAxisCurves && !profile.UsesGroundTargetedLanding)
        {
            EditorGUILayout.HelpBox(
                "未配置 AxisCurves — 运行时位移为 0。请点下方迁移或生成默认 Z 轴。",
                MessageType.Warning);
            if (GUILayout.Button("从旧字段迁移（若资产仍含 DisplacementCurve 序列化数据）"))
            {
                Undo.RecordObject(profile, "Migrate MotionProfile");
                var r = MotionProfileLegacyMigration.TryMigrate(profile);
                Debug.Log($"[MotionXYZ] {profile.name}: {r.Note}");
            }

            _defaultForwardMeters = EditorGUILayout.FloatField("默认前进距离 (m)", _defaultForwardMeters);
            if (GUILayout.Button("生成默认 Z 轴 (0→1 × 距离)"))
            {
                Undo.RecordObject(profile, "Default Z Axis");
                profile.ApplyDefaultForwardAxis(_defaultForwardMeters);
                EditorUtility.SetDirty(profile);
            }
        }

        DrawPropertiesExcluding(serializedObject, s_excludeCustom);
        DrawYAxisSection(profile);
        DrawLandingSettingsSection(profile);
        DrawTimeAuthoritySection(profile);
        DrawClipExtractSection(profile);
        MotionCurveSegmentPresetGUI.Draw(profile, serializedObject);
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
                "当前仍从旧 YPolicy 映射；修改上方字段或点「从 YPolicy 烘焙」后写入三权。",
                MessageType.Warning);
            if (GUILayout.Button("从 YPolicy 烘焙为三权（本资产）"))
            {
                MotionYAxisV2Applicator.ApplyLegacyToV2(profile);
            }
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

    void DrawTimeAuthoritySection(MotionProfileSO profile)
    {
        s_timeAuthorityFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            s_timeAuthorityFoldout,
            "Time Authority / 时间对齐（Runtime）");
        if (!s_timeAuthorityFoldout)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("UseActionDuration"));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("TimeSync"),
            new GUIContent("Time Sync / 时间对齐"));
        EditorGUILayout.HelpBox(
            "None — Logic Duration 与 Action.AnimSpeed 各走各的。\n" +
                    "当Logic Duaration< ClipLength时\n，并使用【None】可以截断【后摇】\n" +
            "Match Motion / 匹配运动 — 保持 Logic Duration；动画倍率 = clip.length / AnimSpeed / logicDur。\n" +
            "Match Animation / 匹配动画 — 保持 Clip 墙钟；Logic/Motion 时长拉伸至 clip.length÷AnimSpeed。\n" +
            "MatchMotion / MatchAnimation 需 Action 绑定 MainClip。",
            MessageType.Info);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Authoring Reference（仅 Editor 计算器 · Runtime 不读）", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Duration_AuthoringReference"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Distance_AuthoringReference"));

        var avg = profile.AuthoringAverageSpeed;
        EditorGUILayout.LabelField("Average Speed (authoring)", $"{avg:F2} m/s");

        EditorGUILayout.PropertyField(serializedObject.FindProperty("AuthoringReferenceAnimSpeed"));
        if (GUILayout.Button("Generate Reference Speed（从曲线末端位移 + Reference Duration）"))
        {
            Undo.RecordObject(profile, "Generate Reference Speed");
            var end = profile.AxisCurves.SampleLocalPosition(1f, 1f);
            profile.Distance_AuthoringReference = new Vector3(end.x, 0f, end.z).magnitude;
            if (profile.SourceClip != null && profile.Duration_AuthoringReference > 0.001f)
            {
                profile.AuthoringReferenceAnimSpeed =
                    profile.SourceClip.length / profile.Duration_AuthoringReference;
            }

            EditorUtility.SetDirty(profile);
        }

        EditorGUILayout.HelpBox(
            "Runtime 时钟：Action.LogicDuration（Use Action Duration=ON）。\n" +
            "上方 Authoring 字段只用于离线估算平均速率 / 参考 AnimSpeed，不参与 Play Mode。",
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

        _fitMode = (MotionCurveFitMode)EditorGUILayout.EnumPopup("Curve Fit Mode", _fitMode);
        _filterMode = (MotionCurveFilterMode)EditorGUILayout.EnumPopup("Filter Mode", _filterMode);
        _filterWindow = EditorGUILayout.IntSlider("Filter Window", _filterWindow, 5, 11);
        _errorTolerance = EditorGUILayout.Slider("Error Tolerance", _errorTolerance, 0.001f, 0.05f);

        using (new EditorGUI.DisabledScope(profile.SourceClip == null))
        {
            if (GUILayout.Button("Extract XYZ From Source Clip"))
            {
                var root = Selection.activeGameObject;
                if (root == null)
                {
                    EditorUtility.DisplayDialog(
                        "Clip Extract",
                        "请在 Hierarchy 选中预览 Rig（角色根节点），再提取。",
                        "OK");
                    return;
                }

                var opt = ClipMotionExtractor.Options.Default;
                opt.FitMode = _fitMode;
                opt.FilterMode = _filterMode;
                opt.FilterWindow = _filterWindow;
                opt.ErrorTolerance = _errorTolerance;
                ClipMotionExtractor.ExtractInto(profile.SourceClip, root.transform, profile, opt);
            }
        }

        var report = ClipMotionExtractor.LastReport;
        if (report.RawSamples > 0)
        {
            EditorGUILayout.LabelField(
                "Last Extract",
                $"raw={report.RawSamples} keys=({report.KeysX},{report.KeysY},{report.KeysZ})");
        }

        EditorGUILayout.HelpBox(
            "Fit Pipeline：None / MovingAverage / SavitzkyGolay → Key Reduction → Spline。需选中 Hierarchy Rig。",
            MessageType.None);
        EditorGUILayout.EndFoldoutHeaderGroup();
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

        if (GUILayout.Button("Generate Speed Curve"))
        {
            Undo.RecordObject(profile, "Generate Motion Speed Curve");
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

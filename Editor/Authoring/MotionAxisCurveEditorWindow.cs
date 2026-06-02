#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// MotionProfile 三轴曲线全屏编辑（143.1 P4）：大曲线区 + Scene 轨迹 + Clip 提取。
/// </summary>
public sealed class MotionAxisCurveEditorWindow : EditorWindow
{
    const float CurveRowHeight = 140f;
    const float ToolbarHeight = 22f;

    [SerializeField] MotionProfileSO _profile;
    [SerializeField] bool _showExtractPanel = true;
    [SerializeField] bool _showPerAxisClips = true;
    [SerializeField] int _selectedAxis;

    MotionCurveFitMode _fitMode = MotionCurveFitMode.CatmullRom;
    MotionCurveFilterMode _filterMode = MotionCurveFilterMode.MovingAverage;
    int _filterWindow = 5;
    float _errorTolerance = 0.01f;
    MotionExtractMode _extractMode = MotionExtractMode.Auto;
    string _sampleBonePath = "Hips";
    int _samplingFps = 30;

    Vector2 _scroll;

    public static void Open(MotionProfileSO profile)
    {
        var win = GetWindow<MotionAxisCurveEditorWindow>(true, "Motion XYZ Curves", true);
        win.minSize = new Vector2(720f, 520f);
        win._profile = profile;
        win.Show();
        win.Focus();
    }

    [MenuItem("Tools/GameMain/Motion Axis Curve Editor")]
    static void OpenFromMenu()
    {
        var profile = Selection.activeObject as MotionProfileSO;
        if (profile == null)
        {
            EditorUtility.DisplayDialog(
                "Motion Axis Curve Editor",
                "请在 Project 选中一个 MotionProfileSO，或在 Inspector 点「打开曲线编辑器」。",
                "OK");
            return;
        }

        Open(profile);
    }

    void OnEnable()
    {
        if (_profile == null && Selection.activeObject is MotionProfileSO mp)
        {
            _profile = mp;
        }
    }

    void OnSelectionChange()
    {
        if (Selection.activeObject is MotionProfileSO mp)
        {
            _profile = mp;
            Repaint();
        }
    }

    void OnGUI()
    {
        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            var next = (MotionProfileSO)EditorGUILayout.ObjectField(
                _profile,
                typeof(MotionProfileSO),
                false,
                GUILayout.MinWidth(200f));
            if (next != _profile)
            {
                _profile = next;
            }

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(_profile == null))
            {
                if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                {
                    SaveProfile();
                }

                if (GUILayout.Button("Scene 轨迹", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                {
                    MotionPathGizmoDrawer.SetPreviewProfile(_profile);
                }

                if (GUILayout.Button("提取 XYZ", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                {
                    RunExtract();
                }
            }
        }

        if (_profile == null)
        {
            EditorGUILayout.HelpBox("绑定 MotionProfileSO 后开始编辑。", MessageType.Info);
            return;
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        DrawExtractPanel();
        EditorGUILayout.Space(6f);
        DrawCurveRows();
        DrawLastExtractReport();
        EditorGUILayout.EndScrollView();
    }

    void DrawExtractPanel()
    {
        _showExtractPanel = EditorGUILayout.BeginFoldoutHeaderGroup(_showExtractPanel, "Clip 提取");
        if (!_showExtractPanel)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        _profile.SourceClip = (AnimationClip)EditorGUILayout.ObjectField(
            "Source Clip",
            _profile.SourceClip,
            typeof(AnimationClip),
            false);

        _showPerAxisClips = EditorGUILayout.Foldout(_showPerAxisClips, "按轴不同 Clip（可选）", true);
        if (_showPerAxisClips)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                _profile.XSourceClip = (AnimationClip)EditorGUILayout.ObjectField(
                    "X Source Clip",
                    _profile.XSourceClip,
                    typeof(AnimationClip),
                    false);
                _profile.YSourceClip = (AnimationClip)EditorGUILayout.ObjectField(
                    "Y Source Clip",
                    _profile.YSourceClip,
                    typeof(AnimationClip),
                    false);
                _profile.ZSourceClip = (AnimationClip)EditorGUILayout.ObjectField(
                    "Z Source Clip",
                    _profile.ZSourceClip,
                    typeof(AnimationClip),
                    false);
            }
        }

        _profile.XExtractSource = (MotionAxisExtractSource)EditorGUILayout.EnumPopup(
            "X Source",
            _profile.XExtractSource);
        _profile.YExtractSource = (MotionAxisExtractSource)EditorGUILayout.EnumPopup(
            "Y Source",
            _profile.YExtractSource);
        _profile.ZExtractSource = (MotionAxisExtractSource)EditorGUILayout.EnumPopup(
            "Z Source",
            _profile.ZExtractSource);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("翻滚 X/Z=RootT Y=0", EditorStyles.miniButtonLeft))
            {
                _profile.XExtractSource = MotionAxisExtractSource.RootT;
                _profile.YExtractSource = MotionAxisExtractSource.ConstantZero;
                _profile.ZExtractSource = MotionAxisExtractSource.RootT;
            }

            if (GUILayout.Button("跳跃 Y=Bone", EditorStyles.miniButtonRight))
            {
                _profile.XExtractSource = MotionAxisExtractSource.RootT;
                _profile.YExtractSource = MotionAxisExtractSource.SampleBone;
                _profile.ZExtractSource = MotionAxisExtractSource.RootT;
            }
        }

        _extractMode = (MotionExtractMode)EditorGUILayout.EnumPopup("Extract Mode", _extractMode);
        _sampleBonePath = EditorGUILayout.TextField("Sample Bone Path", _sampleBonePath);
        _samplingFps = EditorGUILayout.IntSlider("Sample FPS", _samplingFps, 8, 120);
        _fitMode = (MotionCurveFitMode)EditorGUILayout.EnumPopup("Curve Fit Mode", _fitMode);
        _filterMode = (MotionCurveFilterMode)EditorGUILayout.EnumPopup("Filter Mode", _filterMode);
        _filterWindow = EditorGUILayout.IntSlider("Filter Window", _filterWindow, 5, 11);
        _errorTolerance = EditorGUILayout.Slider("Error Tolerance", _errorTolerance, 0.001f, 0.05f);

        EditorGUILayout.EndFoldoutHeaderGroup();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(_profile);
        }
    }

    void DrawCurveRows()
    {
        EditorGUILayout.LabelField("Axis Curves（Evaluate×Scale = 米）", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Toggle(_selectedAxis == 0, "X", EditorStyles.miniButtonLeft))
            {
                _selectedAxis = 0;
            }

            if (GUILayout.Toggle(_selectedAxis == 1, "Y", EditorStyles.miniButtonMid))
            {
                _selectedAxis = 1;
            }

            if (GUILayout.Toggle(_selectedAxis == 2, "Z", EditorStyles.miniButtonRight))
            {
                _selectedAxis = 2;
            }
        }

        DrawLargeAxisRow("X（左右）", 0, ref _profile.AxisCurves.XCurve, ref _profile.AxisCurves.XScale);
        DrawLargeAxisRow("Y（上下）", 1, ref _profile.AxisCurves.YCurve, ref _profile.AxisCurves.YScale);
        DrawLargeAxisRow("Z（前后）", 2, ref _profile.AxisCurves.ZCurve, ref _profile.AxisCurves.ZScale);

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Z 冲刺 Ease"))
            {
                Undo.RecordObject(_profile, "Z Forward Preset");
                _profile.AxisCurves.ZCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
                if (_profile.AxisCurves.ZScale < 0.001f)
                {
                    _profile.AxisCurves.ZScale = 4f;
                }
            }

            if (GUILayout.Button("三轴归零"))
            {
                Undo.RecordObject(_profile, "Zero Axes");
                var flat = AnimationCurve.Constant(0f, 0f, 1f);
                _profile.AxisCurves.XCurve = flat;
                _profile.AxisCurves.YCurve = flat;
                _profile.AxisCurves.ZCurve = flat;
                _profile.AxisCurves.XScale = 0f;
                _profile.AxisCurves.YScale = 0f;
                _profile.AxisCurves.ZScale = 0f;
            }
        }

        if (GUI.changed)
        {
            EditorUtility.SetDirty(_profile);
            if (_profile.UsesAxisCurves)
            {
                MotionPathGizmoDrawer.SetPreviewProfile(_profile);
            }
        }
    }

    void DrawLargeAxisRow(string label, int axisIndex, ref AnimationCurve curve, ref float scale)
    {
        var highlight = _selectedAxis == axisIndex;
        var rect = EditorGUILayout.GetControlRect(false, CurveRowHeight + ToolbarHeight);
        if (highlight)
        {
            EditorGUI.DrawRect(rect, new Color(0.25f, 0.45f, 0.7f, 0.12f));
        }

        var labelRect = new Rect(rect.x, rect.y, 72f, ToolbarHeight);
        var scaleRect = new Rect(rect.xMax - 64f, rect.y, 60f, ToolbarHeight);
        var curveRect = new Rect(rect.x + 76f, rect.y + ToolbarHeight, rect.width - 76f, CurveRowHeight);

        EditorGUI.LabelField(labelRect, label, EditorStyles.boldLabel);
        scale = EditorGUI.FloatField(scaleRect, scale);

        var prevColor = GUI.color;
        if (highlight)
        {
            GUI.color = new Color(1f, 1f, 1f, 1f);
        }

        curve = EditorGUI.CurveField(curveRect, curve);
        GUI.color = prevColor;
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

    void RunExtract()
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

        var clip = _profile.SourceClip
            ?? _profile.XSourceClip
            ?? _profile.YSourceClip
            ?? _profile.ZSourceClip;
        if (clip == null)
        {
            EditorUtility.DisplayDialog("Clip Extract", "请至少指定 Source Clip 或某一轴 Clip。", "OK");
            return;
        }

        var opt = ClipMotionExtractor.OptionsFromProfile(_profile, ClipMotionExtractor.Options.Default);
        opt.ExtractMode = _extractMode;
        opt.SampleBonePath = _sampleBonePath;
        opt.SamplingFps = _samplingFps;
        opt.FitMode = _fitMode;
        opt.FilterMode = _filterMode;
        opt.FilterWindow = _filterWindow;
        opt.ErrorTolerance = _errorTolerance;

        if (ClipMotionExtractor.ExtractInto(clip, rig, _profile, opt))
        {
            MotionPathGizmoDrawer.SetPreviewProfile(_profile);
        }

        SaveProfile();
        Repaint();
    }

    void SaveProfile()
    {
        if (_profile == null)
        {
            return;
        }

        EditorUtility.SetDirty(_profile);
        AssetDatabase.SaveAssets();
    }
}
#endif

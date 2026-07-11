#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed partial class ActionDataTimelineEditor
{
    // 171.7：预览区三档快速倍率
    static readonly float[] PlaybackSpeedPresets = { 0.25f, 0.5f, 1f };
    const float PlaybackSpeedMin = 0.05f;
    const float PlaybackSpeedMax = 4f;

    [SerializeField] MotionPreviewMode _motionPreviewMode = MotionPreviewMode.MotionDriven;
    [SerializeField] bool _loopPlayback = true;        // 172.2 W3：默认循环

    // 204.1 W5：移除 [SerializeField]，每次开窗回默认档 0.25×（策划手感档）
    float _playbackSpeed = 0.25f;
    int _playbackSpeedPresetIndex = 0;

    // 204.x 视觉镜像（独立于 clip 的 Mirror 导入设置）— scale.x=-1 的物理镜像，100% 可靠
    [SerializeField] bool _forceMirrorPreview;

    // 204.x Foot IK — 防止预览时角色脚陷地面。默认开启。
    [SerializeField] bool _enableFootIK = true;

    bool _isPlaying;
    double _playbackLastRealtime;

    // 204.1 W6 档 2：固定步长 Sample 累加器，消除步长抖动
    const float FixedSampleHz = 60f;
    double _sampleAccumulator;

    static GUIContent GetPlaybackToggleContent(bool isPlaying)
    {
        var icon = isPlaying
            ? EditorGUIUtility.IconContent("PauseButton")
            : EditorGUIUtility.IconContent("PlayButton");
        if (icon?.image != null)
        {
            icon.tooltip = isPlaying ? "暂停" : "播放";
            return icon;
        }

        return isPlaying
            ? new GUIContent("||", "暂停")
            : new GUIContent(">", "播放");
    }

    void DrawPreviewPlaybackControls()
    {
        _previewTime = EditorGUILayout.Slider(
            new GUIContent("时间", "Action 0~1 进度；Scene / 时间轴黄线同步"),
            _previewTime,
            0f,
            1f);

        using (new EditorGUILayout.HorizontalScope())
        {
            var playContent = GetPlaybackToggleContent(_isPlaying);
            if (GUILayout.Button(playContent, EditorStyles.miniButtonLeft, GUILayout.Width(28f), GUILayout.Height(20f)))
            {
                TogglePlayback();
            }

            var nextLoop = GUILayout.Toggle(
                _loopPlayback,
                new GUIContent("Loop", "循环播放 Action 0~1"),
                EditorStyles.miniButtonMid,
                GUILayout.Width(44f));
            if (nextLoop != _loopPlayback)
            {
                _loopPlayback = nextLoop;
            }

            GUILayout.Space(6f);
            GUILayout.Label("Speed", EditorStyles.miniLabel, GUILayout.Width(36f));

            var newSpeed = GUILayout.HorizontalSlider(
                _playbackSpeed,
                PlaybackSpeedMin,
                PlaybackSpeedMax,
                GUILayout.Width(88f));
            if (!Mathf.Approximately(newSpeed, _playbackSpeed))
            {
                _playbackSpeed = newSpeed;
                _playbackSpeedPresetIndex = -1;
            }

            var typed = EditorGUILayout.DelayedFloatField(_playbackSpeed, GUILayout.Width(44f));
            if (!Mathf.Approximately(typed, _playbackSpeed))
            {
                _playbackSpeed = Mathf.Clamp(typed, PlaybackSpeedMin, PlaybackSpeedMax);
                _playbackSpeedPresetIndex = -1;
            }

            EditorGUILayout.LabelField("×", EditorStyles.miniLabel, GUILayout.Width(10f));

            for (var i = 0; i < PlaybackSpeedPresets.Length; i++)
            {
                var speed = PlaybackSpeedPresets[i];
                var label = speed >= 1f ? $"{speed:0}x" : $"{speed:0.##}x";
                var style = i switch
                {
                    0 => EditorStyles.miniButtonLeft,
                    1 => EditorStyles.miniButtonMid,
                    _ => EditorStyles.miniButtonRight,
                };
                if (GUILayout.Button(label, style, GUILayout.Width(40f)))
                {
                    _playbackSpeedPresetIndex = i;
                    _playbackSpeed = speed;
                }
            }

            GUILayout.FlexibleSpace();

            var duration = _action != null ? _action.ResolveLogicalDurationSeconds() : 0f;
            var wallSeconds = duration * _previewTime;
            EditorGUILayout.LabelField(
                $"t={wallSeconds:0.000}s  Anim×{ctxProfileAnimSpeed:F2}",
                EditorStyles.miniLabel,
                GUILayout.MinWidth(120f));
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Motion", EditorStyles.miniLabel, GUILayout.Width(42f));
            DrawMotionPreviewModePopup();

            if (_motionPreviewMode == MotionPreviewMode.MotionDriven)
            {
                if (GUILayout.Button(new GUIContent("Reset", "还原预览起点"), EditorStyles.miniButton, GUILayout.Width(48f)))
                {
                    _previewController?.ResetDrivenAnchorPosition();
                    _previewTime = 0f;
                }
            }

            // 204.x：视觉镜像 — 与 clip Mirror 导入设置独立的开关
            var nextMirror = GUILayout.Toggle(
                _forceMirrorPreview,
                new GUIContent("Mirror", "视觉镜像（scale.x=-1）— 与 clip Import 设置无关，所见即所得"),
                EditorStyles.miniButton,
                GUILayout.Width(52f));
            if (nextMirror != _forceMirrorPreview)
            {
                _forceMirrorPreview = nextMirror;
                _previewController?.SetForceMirrorPreview(_forceMirrorPreview);
                SceneView.RepaintAll();
                Repaint();
            }

            // 204.x：Foot IK — 防止脚陷地（Hips 抬升 + 双脚旋转贴地）
            var nextIK = GUILayout.Toggle(
                _enableFootIK,
                new GUIContent("Foot IK", "防止预览角色脚陷入地面 — Hips 抬升 + 脚旋转贴地"),
                EditorStyles.miniButton,
                GUILayout.Width(56f));
            if (nextIK != _enableFootIK)
            {
                _enableFootIK = nextIK;
                _previewController?.SetFootIKEnabled(_enableFootIK);
                SceneView.RepaintAll();
                Repaint();
            }

            DrawPreviewVisibilityDropdown();
            DrawSceneTracksToolbar();
        }
    }

    void DrawMotionPreviewModePopup()
    {
        _motionPreviewMode = MotionPreviewModeUtility.Normalize(_motionPreviewMode);

        var currentIndex = 0;
        for (var i = 0; i < MotionPreviewModeUtility.EditorVisibleModes.Length; i++)
        {
            if (MotionPreviewModeUtility.EditorVisibleModes[i] == _motionPreviewMode)
            {
                currentIndex = i;
                break;
            }
        }

        var nextIndex = EditorGUILayout.Popup(
            currentIndex,
            MotionPreviewModeUtility.EditorVisibleLabels,
            GUILayout.Width(120f));
        if (nextIndex == currentIndex)
        {
            return;
        }

        var nextMode = MotionPreviewModeUtility.EditorVisibleModes[nextIndex];
        _motionPreviewMode = nextMode;
        ActionTimelineRootMotionSampler.InvalidateCache();
        if (nextMode != MotionPreviewMode.MotionDriven)
        {
            _previewController?.ResetDrivenAnchorPosition();
        }
    }

    void DrawPlaybackTransportBar() => DrawPreviewPlaybackControls();

    void SyncPreviewControllerSettings()
    {
        if (_previewController == null)
        {
            return;
        }

        _previewController.SetForceMirrorPreview(_forceMirrorPreview);
        _previewController.SetFootIKEnabled(_enableFootIK);
    }

    void DrawSceneTracksToolbar()
    {
        GUILayout.Label("Tracks", EditorStyles.miniLabel, GUILayout.Width(42f));
        _previewTrackVisibility.Motion = GUILayout.Toggle(_previewTrackVisibility.Motion, "Motion", EditorStyles.miniButtonLeft, GUILayout.Width(52f));
        _previewTrackVisibility.Combat = GUILayout.Toggle(_previewTrackVisibility.Combat, "Combat", EditorStyles.miniButtonMid, GUILayout.Width(52f));
        _previewTrackVisibility.GhostTrail = GUILayout.Toggle(_previewTrackVisibility.GhostTrail, "Ghost", EditorStyles.miniButtonMid, GUILayout.Width(48f));
        _previewTrackVisibility.Teleport = GUILayout.Toggle(_previewTrackVisibility.Teleport, "TP", EditorStyles.miniButtonMid, GUILayout.Width(36f));
        _previewTrackVisibility.Fx = GUILayout.Toggle(_previewTrackVisibility.Fx, "FX", EditorStyles.miniButtonMid, GUILayout.Width(36f));
        _previewTrackVisibility.Audio = GUILayout.Toggle(_previewTrackVisibility.Audio, "SFX", EditorStyles.miniButtonMid, GUILayout.Width(36f));
        _previewTrackVisibility.Camera = GUILayout.Toggle(_previewTrackVisibility.Camera, "Cam", EditorStyles.miniButtonMid, GUILayout.Width(40f));
        _previewTrackVisibility.TimeScale = GUILayout.Toggle(_previewTrackVisibility.TimeScale, "Time", EditorStyles.miniButtonRight, GUILayout.Width(44f));
    }

    void DrawPreviewVisibilityDropdown()
    {
        var label = ActionTimelinePreviewVisibility.GetSummaryLabel(_previewVisibilityMask);
        var rect = GUILayoutUtility.GetRect(new GUIContent($"Show [{label} ▾]"), EditorStyles.toolbarDropDown, GUILayout.Width(110f));
        if (GUI.Button(rect, $"Show [{label} ▾]", EditorStyles.toolbarDropDown))
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("预设/仅动作"), false, () => ApplyPreviewPreset(ActionTimelinePreviewVisibility.PresetPoseOnly));
            menu.AddItem(new GUIContent("预设/仅轨迹"), false, () => ApplyPreviewPreset(ActionTimelinePreviewVisibility.PresetTrajectoryOnly));
            menu.AddItem(new GUIContent("预设/仅信息"), false, () => ApplyPreviewPreset(ActionTimelinePreviewVisibility.PresetInfoOnly));
            menu.AddItem(new GUIContent("预设/全部"), false, () => ApplyPreviewPreset(PreviewVisibilityMask.All));
            menu.AddSeparator(string.Empty);
            AddMaskToggle(menu, "角色 Pose", PreviewVisibilityMask.Pose);
            AddMaskToggle(menu, "真实位移 (MotionDriven)", PreviewVisibilityMask.MotionDriven);
            menu.AddSeparator(string.Empty);
            AddMaskToggle(menu, "合成轨迹 (橙黄)", PreviewVisibilityMask.Composite);
            AddMaskToggle(menu, "X 轴轨迹 (红)", PreviewVisibilityMask.XAxis);
            AddMaskToggle(menu, "Y 轴轨迹 (绿)", PreviewVisibilityMask.YAxis);
            AddMaskToggle(menu, "Z 轴轨迹 (蓝)", PreviewVisibilityMask.ZAxis);
            AddMaskToggle(menu, "Future Markers", PreviewVisibilityMask.FutureMarkers);
            menu.AddSeparator(string.Empty);
            AddMaskToggle(menu, "Window / Phase Bar", PreviewVisibilityMask.Windows);
            AddMaskToggle(menu, "Hitbox", PreviewVisibilityMask.Hitbox);
            AddMaskToggle(menu, "Combat 攻击盒 (B+C)", PreviewVisibilityMask.CombatVolume);
            AddMaskToggle(menu, "Attack HitClip (Active Shape)", PreviewVisibilityMask.AttackHitClip);
            AddMaskToggle(menu, "Attack Coverage (攻击云)", PreviewVisibilityMask.AttackCoverage);
            AddMaskToggle(menu, "Attack Ghost (Trace 插值)", PreviewVisibilityMask.AttackGhost);
            AddMaskToggle(menu, "Attack Hit Preview (Dummy 变色)", PreviewVisibilityMask.AttackHitPreview);
            AddMaskToggle(menu, "Hurtbox", PreviewVisibilityMask.Hurtbox);
            AddMaskToggle(menu, "Invincible / Interrupt", PreviewVisibilityMask.InvuInter);
            menu.AddSeparator(string.Empty);
            AddMaskToggle(menu, "Scene 空间信息", PreviewVisibilityMask.SceneInfo);
            AddMaskToggle(menu, "Scene 锚点十字", PreviewVisibilityMask.SceneAnchor);
            AddMaskToggle(menu, "Ghost Trail", PreviewVisibilityMask.GhostTrail);
            AddMaskToggle(menu, "Teleport", PreviewVisibilityMask.Teleport);
            AddMaskToggle(menu, "Presentation (FX/SFX/Cam/Time)", PreviewVisibilityMask.Presentation);
            menu.DropDown(rect);
        }
    }

    void AddMaskToggle(GenericMenu menu, string label, PreviewVisibilityMask flag)
    {
        var on = ActionTimelinePreviewVisibility.Has(_previewVisibilityMask, flag);
        menu.AddItem(new GUIContent(label), on, () =>
        {
            if (on)
            {
                _previewVisibilityMask &= ~flag;
            }
            else
            {
                _previewVisibilityMask |= flag;
            }

            if (flag == PreviewVisibilityMask.Pose)
            {
                _enablePosePreview = ActionTimelinePreviewVisibility.Has(_previewVisibilityMask, PreviewVisibilityMask.Pose);
            }

            ActionTimelineEditorUI.SavePreviewVisibilityMask(_previewVisibilityMask);
            SceneView.RepaintAll();
            Repaint();
        });
    }

    void ApplyPreviewPreset(PreviewVisibilityMask preset)
    {
        _previewVisibilityMask = preset;
        _enablePosePreview = ActionTimelinePreviewVisibility.Has(preset, PreviewVisibilityMask.Pose);
        if (preset == ActionTimelinePreviewVisibility.PresetPoseOnly)
        {
            _motionPreviewMode = MotionPreviewMode.MotionDriven;
            ActionTimelineRootMotionSampler.InvalidateCache();
        }

        ActionTimelineEditorUI.SavePreviewVisibilityMask(_previewVisibilityMask);
        SceneView.RepaintAll();
        Repaint();
    }

    float ctxProfileAnimSpeed =>
        _action != null && _action.MotionProfile != null
            ? _action.MotionProfile.SampleAnimSpeed(_action, _previewTime)
            : 1f;

    void TogglePlayback()
    {
        _isPlaying = !_isPlaying;
        if (_isPlaying)
        {
            ActionTimelineRootMotionSampler.InvalidateCache();
            _playbackLastRealtime = EditorApplication.timeSinceStartup;
            _sampleAccumulator = 0.0;

            // 204.1 W3：起播前同步一次 Pose 到 _previewTime，避免 T0+ε 渲染脏 Pose 闪现 Clip 末帧
            SyncPoseToCurrentPreviewTime();

            EditorApplication.update += OnPlaybackEditorUpdate;
        }
        else
        {
            EditorApplication.update -= OnPlaybackEditorUpdate;
        }
    }

    /// <summary>
    /// 204.1 W3：起播前/Reset 后强制 Sample 一次 Pose，使 anchor.position 落到 _previewTime 对应位置。
    /// 避免 SceneView 下一帧渲染时仍用上次缓存的脏 anchor（如残留 nt=1 末帧）。
    /// </summary>
    void SyncPoseToCurrentPreviewTime()
    {
        if (_action == null) return;
        var ctx = BuildCurrentPreviewContext();
        if (ActionTimelinePreviewVisibility.Has(_previewVisibilityMask, PreviewVisibilityMask.Pose))
        {
            _previewController.Enabled = true;
            _previewController.SamplePose(in ctx, applyMotionDisplacement: true);
        }
        SceneView.RepaintAll();
        Repaint();
    }

    void StopPlayback()
    {
        _isPlaying = false;
        EditorApplication.update -= OnPlaybackEditorUpdate;
        _previewTime = 0f;
        _playbackLastRealtime = EditorApplication.timeSinceStartup;
        Repaint();
        SceneView.RepaintAll();
    }

    void OnPlaybackEditorUpdate()
    {
        if (!_isPlaying || _action == null)
        {
            return;
        }

        var duration = _action.ResolveLogicalDurationSeconds();
        if (duration <= 0.001f)
        {
            _isPlaying = false;
            EditorApplication.update -= OnPlaybackEditorUpdate;
            return;
        }

        // Action nt 恒为 0~1；Segment 仅映射 Clip，不参与播放循环上界。
        const float loopStart = 0f;
        const float loopEnd = 1f;

        var now = EditorApplication.timeSinceStartup;
        var delta = now - _playbackLastRealtime;
        _playbackLastRealtime = now;

        // 204.1 W6 档 2：固定步长累加 + 真实步长重绘，消除步长抖动
        _sampleAccumulator += delta;
        var fixedStep = 1.0 / FixedSampleHz;
        // 防御：长 stall（Domain Reload 后）一次最多消耗 4 步，避免大跳
        var maxStepsPerTick = 4;
        while (_sampleAccumulator >= fixedStep && maxStepsPerTick-- > 0)
        {
            _previewTime += (float)(fixedStep * _playbackSpeed / duration);
            _sampleAccumulator -= fixedStep;
        }
        if (_sampleAccumulator > fixedStep * 4)
        {
            _sampleAccumulator = 0.0; // 异常长帧 → 丢弃残余，下一帧重新起步
        }

        if (_previewTime >= loopEnd)
        {
            if (_loopPlayback)
            {
                var overflow = _previewTime - loopEnd;
                _previewTime = loopStart + Mathf.Repeat(overflow, loopEnd - loopStart);
            }
            else
            {
                _previewTime = loopEnd;
                _isPlaying = false;
                EditorApplication.update -= OnPlaybackEditorUpdate;
            }
        }

        // 204.1 W6 档 1：抗失焦节流 — 强制所有 View 重绘
        Repaint();
        SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    void HandlePlaybackKeyboardShortcuts()
    {
        var e = Event.current;
        if (e.type != EventType.KeyDown || EditorGUIUtility.editingTextField)
        {
            return;
        }

        switch (e.keyCode)
        {
            case KeyCode.Space:
                if (e.shift)
                {
                    StopPlayback();
                }
                else
                {
                    TogglePlayback();
                }

                e.Use();
                Repaint();
                break;
            case KeyCode.LeftBracket:
                StepPlaybackSpeed(-1);
                e.Use();
                Repaint();
                break;
            case KeyCode.RightBracket:
                StepPlaybackSpeed(1);
                e.Use();
                Repaint();
                break;
        }
    }

    void StepPlaybackSpeed(int direction)
    {
        _playbackSpeedPresetIndex = Mathf.Clamp(
            _playbackSpeedPresetIndex + direction,
            0,
            PlaybackSpeedPresets.Length - 1);
        _playbackSpeed = PlaybackSpeedPresets[_playbackSpeedPresetIndex];
    }

    void TeardownPlayback()
    {
        _isPlaying = false;
        EditorApplication.update -= OnPlaybackEditorUpdate;
    }
}
#endif

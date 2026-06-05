#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed partial class ActionDataTimelineEditor
{
    static bool s_foldActionSummary = true;
    Vector2 _timelineScroll;
    Vector2 _propertiesScroll;
    float _propertiesColumnWidth = 340f;
    bool _foldQuickAdd;
    bool _splitterDragging;
    float _splitterDragStartMouseX;
    float _splitterDragStartPropertyWidth;

    void DrawMainLayout()
    {
        var edgePad = ActionTimelineEditorUI.ColumnContentPadding;

        DrawTopBar();

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(edgePad);
            using (new EditorGUILayout.VerticalScope())
            {
                s_foldActionSummary = ActionTimelineEditorUI.Foldout(s_foldActionSummary, "动作摘要");
                if (s_foldActionSummary)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        DrawActionHeaderCompact();
                    }

                    EditorGUILayout.Space(4f);
                }

                DrawPreviewTimeRow();

                var totalWidth = Mathf.Max(
                    position.width - edgePad * 2f - 4f,
                    ActionTimelineEditorUI.TimelineColumnMinWidth
                    + ActionTimelineEditorUI.PropertiesColumnMinWidth
                    + ActionTimelineEditorUI.SplitterWidth);
                _propertiesColumnWidth = ActionTimelineEditorUI.ClampPropertyWidth(_propertiesColumnWidth, totalWidth);
                var propsW = _propertiesColumnWidth;
                var timelineW = totalWidth - propsW - ActionTimelineEditorUI.SplitterWidth;

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(timelineW)))
                    {
                        DrawTimelineColumn(timelineW);
                    }

                    ActionTimelineEditorUI.DrawVerticalSplitter(
                        this,
                        ref _propertiesColumnWidth,
                        totalWidth,
                        ref _splitterDragging,
                        ref _splitterDragStartMouseX,
                        ref _splitterDragStartPropertyWidth);

                    propsW = _propertiesColumnWidth;
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(propsW)))
                    {
                        DrawPropertiesColumn(propsW);
                    }
                }

                DrawStatusBar();
            }

            GUILayout.Space(edgePad);
        }
    }

    void DrawTopBar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            var next = (ActionDataSO)EditorGUILayout.ObjectField(
                _action,
                typeof(ActionDataSO),
                false,
                GUILayout.MinWidth(100f),
                GUILayout.MaxWidth(220f));
            if (next != _action)
            {
                Bind(next);
            }

            if (GUILayout.Button(new GUIContent("保存", "写入磁盘"), EditorStyles.toolbarButton, GUILayout.Width(40f)))
            {
                EditorUtility.SetDirty(_action);
                AssetDatabase.SaveAssets();
            }

            if (GUILayout.Button(new GUIContent("刷新", "重读 SerializedObject"), EditorStyles.toolbarButton, GUILayout.Width(40f)))
            {
                _so?.Update();
                RefreshProperties();
                Repaint();
            }

            if (GUILayout.Button(new GUIContent("预览", "Scene 预览与 Pose 采样"), EditorStyles.toolbarButton, GUILayout.Width(40f)))
            {
                SyncSceneBridge();
                SceneView.RepaintAll();
                Repaint();
            }

            GUILayout.FlexibleSpace();

            GUILayout.Label("缩放", EditorStyles.miniLabel, GUILayout.Width(28f));
            var nextZoom = GUILayout.HorizontalSlider(_zoom, 0.5f, 3f, GUILayout.Width(88f));
            if (!Mathf.Approximately(nextZoom, _zoom))
            {
                _zoom = nextZoom;
                ActionTimelineEditorUI.SaveZoom(_zoom);
            }
        }
    }

    void DrawActionHeaderCompact()
    {
        EditorGUILayout.PropertyField(_so.FindProperty(nameof(ActionDataSO.Category)));
        EditorGUILayout.PropertyField(_so.FindProperty(nameof(ActionDataSO.MainClip)),
            new GUIContent("Main Clip"));
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PropertyField(_so.FindProperty(nameof(ActionDataSO.Duration)),
                new GUIContent("时长"));
            EditorGUILayout.PropertyField(_so.FindProperty(nameof(ActionDataSO.InterruptPriority)),
                new GUIContent("优先级"));
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PropertyField(_so.FindProperty(nameof(ActionDataSO.InterruptStability)),
                new GUIContent("强韧"));
            EditorGUILayout.PropertyField(_so.FindProperty(nameof(ActionDataSO.AllowSelfInterrupt)),
                new GUIContent("自打断"));
        }
    }

    void DrawPreviewTimeRow()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            _previewTime = EditorGUILayout.Slider(
                new GUIContent("预览", "时间轴黄线；Scene 同步 Pose 与 Handles"),
                _previewTime,
                0f,
                1f);

            _enablePosePreview = EditorGUILayout.ToggleLeft(
                new GUIContent("Pose", "AnimationMode 采样 MainClip"),
                _enablePosePreview,
                GUILayout.Width(52f));
            _enableSceneOverlay = EditorGUILayout.ToggleLeft(
                new GUIContent("Scene", "Handles 绘制 Hitbox/标记等"),
                _enableSceneOverlay,
                GUILayout.Width(58f));
        }
    }

    void DrawTimelineColumn(float width)
    {
        var pad = ActionTimelineEditorUI.ColumnContentPadding;
        var innerW = width - pad * 2f;

        ActionTimelineEditorUI.SectionHeader("时间轴", "左栏：轨道与片段；选中后在右侧编辑属性");

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(pad);
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(innerW)))
            {
                var timelineHeight = Mathf.Max(180f, position.height * 0.38f);
                _timelineScroll = ActionTimelineEditorUI.BeginVerticalScrollView(
                    _timelineScroll,
                    GUILayout.Height(timelineHeight),
                    GUILayout.MaxWidth(innerW));
                var timelineRect = GUILayoutUtility.GetRect(
                    innerW,
                    ActiveTracks.Length * (RowHeight + 2f) + RulerHeight + 8f);
                DrawTimeline(timelineRect);
                ActionTimelineEditorUI.EndScrollView();
                _timelineScroll.x = 0f;
            }

            GUILayout.Space(pad);
        }
    }

    void DrawPropertiesColumn(float width)
    {
        var pad = ActionTimelineEditorUI.ColumnContentPadding;
        var innerW = width - pad * 2f;

        ActionTimelineEditorUI.SectionHeader("属性", "仅显示当前选中片段/标记");

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(pad);
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(innerW)))
            {
                DrawToolbarCompact();

                EditorGUILayout.Space(4f);
                ActionTimelineEditorUI.CompactPropertyContext = true;
                using (new ActionTimelineEditorUI.PropertyLayoutScope(innerW))
                {
                    try
                    {
                        _propertiesScroll = ActionTimelineEditorUI.BeginVerticalScrollView(
                            _propertiesScroll,
                            GUILayout.ExpandHeight(true),
                            GUILayout.MaxWidth(innerW));
                        DrawSelectionInspector();
                        EditorGUILayout.Space(8f);
                        DrawQuickAddCompact();
                        ActionTimelineEditorUI.EndScrollView();
                        _propertiesScroll.x = 0f;
                    }
                    finally
                    {
                        ActionTimelineEditorUI.CompactPropertyContext = false;
                    }
                }
            }

            GUILayout.Space(pad);
        }
    }

    void DrawToolbarCompact()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ Window", EditorStyles.miniButtonLeft))
            {
                AddBlankWindow(Snap(_previewTime), Snap(Mathf.Min(1f, _previewTime + DefaultCombatClipLength)));
            }

            using (new EditorGUI.DisabledScope(!HasDeletableSelection()))
            {
                if (GUILayout.Button("−", EditorStyles.miniButtonMid, GUILayout.Width(22f)))
                {
                    TryDeleteSelection();
                }
            }

            if (GUILayout.Button("+ TP", EditorStyles.miniButtonMid))
            {
                Undo.RecordObject(_action, "Add Teleport");
                _teleports.arraySize++;
                _teleports.GetArrayElementAtIndex(_teleports.arraySize - 1)
                    .FindPropertyRelative(nameof(TeleportTrigger.TriggerTime)).floatValue = Snap(_previewTime);
                _selectedTeleport = _teleports.arraySize - 1;
                _selectedWindow = -1;
            }

            if (GUILayout.Button("+ ◆", EditorStyles.miniButtonRight))
            {
                var kind = TrackToDefaultMarkerKind(_lastClickedTrack);
                if (kind != ActionTimelineMarkerKind.None)
                {
                    AddMarkerAtPreview(kind);
                }
            }
        }
    }

    void DrawQuickAddCompact()
    {
        _foldQuickAdd = ActionTimelineEditorUI.Foldout(_foldQuickAdd, "快捷添加（预览时刻）");
        if (!_foldQuickAdd)
        {
            return;
        }

        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.LabelField("战斗片段", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Hitbox", EditorStyles.miniButton)) AddCombatClip(TrackId.Hitbox, _previewTime);
                if (GUILayout.Button("Hurt", EditorStyles.miniButton)) AddCombatClip(TrackId.Hurtbox, _previewTime);
                if (GUILayout.Button("Invuln", EditorStyles.miniButton)) AddCombatClip(TrackId.Invincible, _previewTime);
            }

            DrawCameraTrackQuickAdd();
            DrawTimeScaleTrackQuickAdd();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("FX", EditorStyles.miniButton)) AddMarkerAtPreview(ActionTimelineMarkerKind.SpawnVfx);
                if (GUILayout.Button("SFX", EditorStyles.miniButton)) AddMarkerAtPreview(ActionTimelineMarkerKind.PlaySfx);
            }

            ActionTimelineEditorUI.MiniHint($"焦点轨：{GetTrackLabel(_lastClickedTrack)}");
        }
    }

    void DrawStatusBar()
    {
        EditorGUILayout.Space(2f);
        var pad = ActionTimelineEditorUI.ColumnContentPadding;
        var barRect = EditorGUILayout.GetControlRect(false, ActionTimelineEditorUI.StatusBarHeight);
        EditorGUI.DrawRect(barRect, new Color(0.18f, 0.18f, 0.18f, 1f));

        var style = EditorStyles.miniLabel;
        var x = barRect.x + pad;
        var y = barRect.y + 3f;
        var lineH = barRect.height - 4f;

        void DrawSegment(string text, float minW = 0f)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var size = style.CalcSize(new GUIContent(text));
            var w = Mathf.Max(minW, size.x + 8f);
            GUI.Label(new Rect(x, y, w, lineH), text, style);
            x += w + 4f;
        }

        var clipLen = _action.MainClip != null
            ? _action.MainClip.length
            : _action.ResolveLogicalDurationSeconds();
        DrawSegment($"Action: {_action.name}", 120f);
        DrawSegment($"Clip: {clipLen:0.00}s", 72f);
        DrawSegment($"t={_previewTime:0.00}", 52f);
        DrawSegment($"Track: {GetTrackLabel(_lastClickedTrack)}", 100f);

        if (_selectedWindow >= 0)
        {
            DrawSegment($"Window #{_selectedWindow}", 72f);
        }
        else if (_selectedMarker >= 0)
        {
            DrawSegment($"Marker #{_selectedMarker}", 72f);
        }
        else if (_selectedTeleport >= 0)
        {
            DrawSegment($"TP #{_selectedTeleport}", 56f);
        }

        var anchorRect = new Rect(barRect.xMax - 168f - pad, barRect.y + 1f, 160f, barRect.height - 2f);
        _gizmoAnchorOverride = (Transform)EditorGUI.ObjectField(
            anchorRect,
            _gizmoAnchorOverride,
            typeof(Transform),
            true);
    }

    void LoadEditorLayoutPrefs()
    {
        _propertiesColumnWidth = ActionTimelineEditorUI.LoadPropertyWidth(340f);
        _zoom = ActionTimelineEditorUI.LoadZoom(1f);
        _timelineScroll = ActionTimelineEditorUI.LoadTimelineScroll();
        _propertiesScroll = new Vector2(0f, ActionTimelineEditorUI.LoadPropertiesScrollY(0f));
    }

    void SaveEditorLayoutPrefs()
    {
        ActionTimelineEditorUI.SavePropertyWidth(_propertiesColumnWidth);
        ActionTimelineEditorUI.SaveZoom(_zoom);
        ActionTimelineEditorUI.SaveTimelineScroll(_timelineScroll);
        ActionTimelineEditorUI.SavePropertiesScrollY(_propertiesScroll.y);
    }
}
#endif

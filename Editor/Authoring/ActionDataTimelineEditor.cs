#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// ActionDataSO 时间轴（139.2）。
/// P1：Interrupt + Combat；P2/P3：FX / Audio / Camera / TimeScale + Scene Gizmo。
/// </summary>
public sealed partial class ActionDataTimelineEditor : EditorWindow
{
    enum TrackId
    {
        Interrupt,
        PhaseStartup,
        PhaseActive,
        PhaseRecovery,
        Hitbox,
        Hurtbox,
        Invincible,
        ComboInput,
        RootMotion,
        RuntimeEvent,
        Teleport,
        Fx,
        Audio,
        Camera,
        TimeScale,
    }

    enum DragMode
    {
        None,
        MoveClip,
        ResizeStart,
        ResizeEnd,
        ScrubPlayhead,
        MoveMarker,
    }

    static readonly TrackId[] ActiveTracks =
    {
        TrackId.Interrupt,
        TrackId.PhaseStartup,
        TrackId.PhaseActive,
        TrackId.PhaseRecovery,
        TrackId.Hitbox,
        TrackId.Hurtbox,
        TrackId.Invincible,
        TrackId.ComboInput,
        TrackId.RootMotion,
        TrackId.RuntimeEvent,
        TrackId.Teleport,
        TrackId.Fx,
        TrackId.Audio,
        TrackId.Camera,
        TrackId.TimeScale,
    };

    const float LabelWidth = 124f;
    const float RowHeight = 20f;
    const float RulerHeight = 24f;
    const float HandleWidth = 6f;
    const float MinClipDuration = 0.02f;
    const float SnapStep = 0.01f;
    const float DefaultCombatClipLength = 0.12f;

    ActionDataSO _action;
    SerializedObject _so;
    SerializedProperty _windows;
    SerializedProperty _teleports;
    SerializedProperty _markers;
    int _selectedWindow = -1;
    int _selectedTeleport = -1;
    int _selectedMarker = -1;
    float _zoom = 1f;
    float _previewTime;

    Rect _laneRect;
    float _firstRowY;
    TrackId _lastClickedTrack = TrackId.Hitbox;
    ActionTimelineMarkerKind _preferredCameraKind = ActionTimelineMarkerKind.CameraShake;

    DragMode _dragMode;
    int _dragWindowIndex = -1;
    int _dragMarkerIndex = -1;
    float _dragAnchorNorm;
    float _dragOrigStart;
    float _dragOrigEnd;
    float _dragOrigMarkerTime;

    [SerializeField] Transform _gizmoAnchorOverride;
    [SerializeField] bool _enablePosePreview = true;
    [SerializeField] bool _enableSceneOverlay = true;

    static ActionDataTimelineEditor s_activeEditor;
    ActionTimelinePreviewController _previewController = new();

    [MenuItem("Tools/GameMain/Action Timeline Editor")]
    static void OpenFromMenu()
    {
        var w = GetWindow<ActionDataTimelineEditor>(false, "Action Timeline", true);
        w.minSize = new Vector2(940f, 480f);
        w.Show();
    }

    public static void Open(ActionDataSO action)
    {
        var w = GetWindow<ActionDataTimelineEditor>(false, "Action Timeline", true);
        w.minSize = new Vector2(940f, 480f);
        w.Bind(action);
        w.Show();
    }

    void OnFocus()
    {
        s_activeEditor = this;
    }

    void OnEnable()
    {
        s_activeEditor = this;
        LoadEditorLayoutPrefs();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable()
    {
        SaveEditorLayoutPrefs();
        SceneView.duringSceneGui -= OnSceneGUI;
        _previewController?.Stop();
        ActionDataTimelineSceneBridge.Clear();
        if (s_activeEditor == this)
        {
            s_activeEditor = null;
        }
    }

    void Bind(ActionDataSO action)
    {
        _action = action;
        _so = action != null ? new SerializedObject(action) : null;
        RefreshProperties();
        _selectedWindow = -1;
        _selectedTeleport = -1;
        _selectedMarker = -1;
        _dragMode = DragMode.None;
        SyncSceneBridge();
    }

    void SyncSceneBridge()
    {
        ActionDataTimelineSceneBridge.Action = _action;
        ActionDataTimelineSceneBridge.PreviewTime = _previewTime;
        ActionDataTimelineSceneBridge.Anchor = _gizmoAnchorOverride;
    }

    void OnSelectionChange()
    {
        if (Selection.activeObject is ActionDataSO action)
        {
            Bind(action);
            Repaint();
        }
    }

    void OnGUI()
    {
        if (_action == null || _so == null)
        {
            EditorGUILayout.Space(4f);
            var picked = (ActionDataSO)EditorGUILayout.ObjectField("Action", _action, typeof(ActionDataSO), false);
            if (picked != _action)
            {
                Bind(picked);
            }

            EditorGUILayout.HelpBox("选择 ActionDataSO 以编辑时间轴。", MessageType.Info);
            return;
        }

        _so.Update();
        HandleKeyboardShortcuts();
        DrawMainLayout();

        if (_so.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(_action);
        }

        SyncSceneBridge();
        SceneView.RepaintAll();
    }

    void OnSceneGUI(SceneView view)
    {
        if (s_activeEditor != this || _action == null)
        {
            return;
        }

        var ctx = ActionTimelinePreviewContext.Build(_action, _previewTime, _gizmoAnchorOverride);
        if (_enablePosePreview)
        {
            _previewController.Enabled = true;
            _previewController.SamplePose(in ctx);
        }
        else
        {
            _previewController.Stop();
        }

        if (_enableSceneOverlay)
        {
            ActionDataTimelineSceneBridge.DrawSceneGUI(in ctx);
        }
    }

    void RefreshProperties()
    {
        _windows = _so?.FindProperty(nameof(ActionDataSO.Windows));
        _teleports = _so?.FindProperty(nameof(ActionDataSO.TeleportTriggers));
        _markers = _so?.FindProperty(nameof(ActionDataSO.TimelineMarkers));
    }

    void DrawSelectionInspector()
    {
        if (_selectedWindow >= 0 && _selectedWindow < _windows.arraySize)
        {
            DrawWindowClipProperties(_windows.GetArrayElementAtIndex(_selectedWindow), _selectedWindow);
            return;
        }

        if (_selectedTeleport >= 0 && _selectedTeleport < _teleports.arraySize)
        {
            DrawTeleportProperties(_teleports.GetArrayElementAtIndex(_selectedTeleport), _selectedTeleport);
            return;
        }

        if (_selectedMarker >= 0 && _markers != null && _selectedMarker < _markers.arraySize)
        {
            DrawMarkerInspector(_markers.GetArrayElementAtIndex(_selectedMarker));
            return;
        }

        if (_lastClickedTrack == TrackId.Camera)
        {
            DrawCameraTrackInspectorHint();
            return;
        }

        DrawEmptyPropertiesHint();
    }

    void DrawCombatTagToggles(SerializedProperty windowElem)
    {
        var pad = ActionTimelineEditorUI.ColumnContentPadding;
        EditorGUILayout.LabelField("战斗轨快捷开关", EditorStyles.miniBoldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(pad);
            DrawTagToggle(windowElem, StateTag.HitboxActive_Window, "Hitbox");
            DrawTagToggle(windowElem, StateTag.HurtboxActive_Window, "Hurtbox");
            DrawTagToggle(windowElem, StateTag.Invulnerable, "Invuln");
            DrawInterruptToggle(windowElem);
            GUILayout.Space(pad);
        }
    }

    void DrawInterruptToggle(SerializedProperty windowElem)
    {
        var p = windowElem.FindPropertyRelative(nameof(ActionWindow.InterruptibleByCategories));
        var on = (ActionCategory)p.enumValueFlag != ActionCategory.None;
        var next = GUILayout.Toggle(on, "Interrupt", EditorStyles.miniButton);
        if (next == on)
        {
            return;
        }

        Undo.RecordObject(_action, "Toggle Interrupt Window");
        p.enumValueFlag = next ? (int)ActionCategory.Movement : (int)ActionCategory.None;
    }

    void HandleKeyboardShortcuts()
    {
        var e = Event.current;
        if (e.type != EventType.KeyDown
            || (e.keyCode != KeyCode.Delete && e.keyCode != KeyCode.Backspace))
        {
            return;
        }

        if (!TryDeleteSelection())
        {
            return;
        }

        e.Use();
        GUI.changed = true;
        Repaint();
    }

    bool HasDeletableSelection()
    {
        if (_selectedWindow >= 0 && _windows != null && _selectedWindow < _windows.arraySize)
        {
            return true;
        }

        if (_selectedTeleport >= 0 && _teleports != null && _selectedTeleport < _teleports.arraySize)
        {
            return true;
        }

        return _selectedMarker >= 0 && _markers != null && _selectedMarker < _markers.arraySize;
    }

    bool TryDeleteSelection()
    {
        if (_selectedWindow >= 0 && _windows != null && _selectedWindow < _windows.arraySize)
        {
            Undo.RecordObject(_action, "Delete Action Window");
            _windows.DeleteArrayElementAtIndex(_selectedWindow);
            _selectedWindow = _windows.arraySize > 0
                ? Mathf.Clamp(_selectedWindow, 0, _windows.arraySize - 1)
                : -1;
            return true;
        }

        if (_selectedTeleport >= 0 && _teleports != null && _selectedTeleport < _teleports.arraySize)
        {
            Undo.RecordObject(_action, "Delete Teleport");
            _teleports.DeleteArrayElementAtIndex(_selectedTeleport);
            _selectedTeleport = _teleports.arraySize > 0
                ? Mathf.Clamp(_selectedTeleport, 0, _teleports.arraySize - 1)
                : -1;
            return true;
        }

        if (_selectedMarker >= 0 && _markers != null && _selectedMarker < _markers.arraySize)
        {
            Undo.RecordObject(_action, "Delete Timeline Marker");
            _markers.DeleteArrayElementAtIndex(_selectedMarker);
            _selectedMarker = _markers.arraySize > 0
                ? Mathf.Clamp(_selectedMarker, 0, _markers.arraySize - 1)
                : -1;
            return true;
        }

        return false;
    }

    static void ClearWindowElement(SerializedProperty elem, float start, float end)
    {
        elem.FindPropertyRelative(nameof(ActionWindow.NormalizedStart)).floatValue = start;
        elem.FindPropertyRelative(nameof(ActionWindow.NormalizedEnd)).floatValue = end;
        elem.FindPropertyRelative(nameof(ActionWindow.WindowSlotMask)).longValue = 0L;
        elem.FindPropertyRelative(nameof(ActionWindow.InterruptibleByCategories)).enumValueFlag =
            (int)ActionCategory.None;
        elem.FindPropertyRelative(nameof(ActionWindow.MinIncomingPriority)).intValue = 0;
        elem.FindPropertyRelative(nameof(ActionWindow.AllowSelfInterrupt)).boolValue = false;

        var events = elem.FindPropertyRelative(nameof(ActionWindow.RuntimeEvents));
        if (events != null && events.isArray)
        {
            events.arraySize = 0;
        }
    }

    void DrawTagToggle(SerializedProperty windowElem, StateTag tag, string label)
    {
        var on = WindowHasTag(windowElem, tag);
        var next = GUILayout.Toggle(on, label, EditorStyles.miniButton);
        if (next != on)
        {
            Undo.RecordObject(_action, "Toggle Window Tag");
            ActionDataTimelineSlots.SetTag(windowElem, tag, next);
        }
    }

    void DrawTimeline(Rect outer)
    {
        var trackArea = new Rect(outer.x, outer.y, outer.width, outer.height);
        var laneGap = ActionTimelineEditorUI.TimelineLabelLaneGap;
        _laneRect = new Rect(
            trackArea.x + LabelWidth + laneGap,
            trackArea.y,
            Mathf.Max(80f, trackArea.width - LabelWidth - laneGap),
            trackArea.height);

        DrawRuler(_laneRect);

        _firstRowY = _laneRect.y + RulerHeight + 4f;
        var y = _firstRowY;
        for (var i = 0; i < ActiveTracks.Length; i++)
        {
            var track = ActiveTracks[i];
            var row = new Rect(trackArea.x, y, trackArea.width, RowHeight);
            DrawTrackRow(row, _laneRect, track);
            y += RowHeight + 2f;
        }

        DrawPlayhead(_laneRect, _firstRowY, y - _firstRowY);
        HandleTimelineInput();
    }

    void DrawRuler(Rect lane)
    {
        var ruler = new Rect(lane.x, lane.y, lane.width, RulerHeight);
        EditorGUI.DrawRect(ruler, new Color(0.18f, 0.18f, 0.18f, 1f));
        Handles.color = new Color(1f, 1f, 1f, 0.25f);
        for (var tick = 0; tick <= 10; tick++)
        {
            var t = tick / 10f;
            var x = TimeToX(lane, t);
            Handles.DrawLine(new Vector3(x, ruler.yMax - 4f), new Vector3(x, ruler.yMax));
            GUI.Label(new Rect(x - 12f, ruler.y, 24f, 16f), t.ToString("0.0"), EditorStyles.miniLabel);
        }

        var scrub = new Rect(lane.x, lane.y, lane.width, RulerHeight);
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && scrub.Contains(Event.current.mousePosition))
        {
            _previewTime = Snap(XToTime(lane, Event.current.mousePosition.x));
            _dragMode = DragMode.ScrubPlayhead;
            Event.current.Use();
            Repaint();
        }
    }

    void DrawPlayhead(Rect lane, float top, float height)
    {
        var x = TimeToX(lane, _previewTime);
        Handles.color = new Color(1f, 0.92f, 0.2f, 0.9f);
        Handles.DrawLine(new Vector3(x, top - 2f), new Vector3(x, top + height));
    }

    void DrawTrackRow(Rect row, Rect lane, TrackId track)
    {
        var labelPad = 2f;
        var labelRect = new Rect(row.x + labelPad, row.y, LabelWidth - labelPad - 2f, RowHeight);
        var isPresentation = IsPresentationTrack(track);
        var style = EditorStyles.label;
        if (IsCombatCoreTrack(track))
        {
            style = EditorStyles.boldLabel;
        }
        else if (isPresentation)
        {
            style = EditorStyles.miniBoldLabel;
        }

        if (GUI.Button(labelRect, GetTrackLabel(track), style))
        {
            _lastClickedTrack = track;
            if (track == TrackId.Camera && Event.current.button == 1)
            {
                ShowCameraTrackContextMenu();
                Event.current.Use();
            }
        }

        var barRect = new Rect(lane.x, row.y, lane.width, RowHeight);
        EditorGUI.DrawRect(barRect, IsCombatCoreTrack(track)
            ? new Color(0.14f, 0.12f, 0.12f, 1f)
            : isPresentation
                ? new Color(0.1f, 0.12f, 0.16f, 1f)
                : new Color(0.12f, 0.12f, 0.12f, 1f));

        if (track == TrackId.Teleport)
        {
            DrawTeleportMarkers(barRect);
            return;
        }

        if (IsPresentationTrack(track))
        {
            DrawPresentationMarkers(barRect, track);
            return;
        }

        if (track == TrackId.RuntimeEvent)
        {
            DrawRuntimeEventTrackExtras(barRect);
        }

        if (_windows == null)
        {
            return;
        }

        for (var i = 0; i < _windows.arraySize; i++)
        {
            var elem = _windows.GetArrayElementAtIndex(i);
            var w = ReadWindow(elem);
            if (track == TrackId.RuntimeEvent)
            {
                if (!HasRuntimeEvents(elem.FindPropertyRelative(nameof(ActionWindow.RuntimeEvents))))
                {
                    continue;
                }
            }
            else if (!WindowContributesToTrack(w, track))
            {
                continue;
            }

            var start = TimeToX(barRect, w.NormalizedStart);
            var end = TimeToX(barRect, w.NormalizedEnd);
            var seg = new Rect(start, barRect.y + 2f, Mathf.Max(2f, end - start), barRect.height - 4f);
            EditorGUI.DrawRect(seg, GetTrackColor(track));

            if (_selectedWindow == i)
            {
                Handles.color = Color.white;
                Handles.DrawWireCube(seg.center, new Vector3(seg.width, seg.height, 0f));
                DrawResizeHandles(seg);
            }
        }
    }

    static void DrawResizeHandles(Rect seg)
    {
        var left = new Rect(seg.xMin - 1f, seg.y, HandleWidth, seg.height);
        var right = new Rect(seg.xMax - HandleWidth + 1f, seg.y, HandleWidth, seg.height);
        EditorGUI.DrawRect(left, new Color(1f, 1f, 1f, 0.55f));
        EditorGUI.DrawRect(right, new Color(1f, 1f, 1f, 0.55f));
    }

    void DrawTeleportMarkers(Rect barRect)
    {
        if (_teleports == null)
        {
            return;
        }

        for (var i = 0; i < _teleports.arraySize; i++)
        {
            var elem = _teleports.GetArrayElementAtIndex(i);
            var t = elem.FindPropertyRelative(nameof(TeleportTrigger.TriggerTime)).floatValue;
            var x = TimeToX(barRect, t);
            var r = new Rect(x - 4f, barRect.y + 3f, 8f, barRect.height - 6f);
            EditorGUI.DrawRect(r, _selectedTeleport == i ? new Color(0.3f, 1f, 1f) : new Color(0.2f, 0.75f, 0.95f));
        }
    }

    void HandleTimelineInput()
    {
        var e = Event.current;
        if (e == null)
        {
            return;
        }

        if (e.type == EventType.MouseDrag && _dragMode == DragMode.ScrubPlayhead)
        {
            _previewTime = Snap(XToTime(_laneRect, e.mousePosition.x));
            e.Use();
            Repaint();
            return;
        }

        if (e.type == EventType.MouseDrag && _dragMode == DragMode.MoveMarker)
        {
            ApplyMarkerDrag();
            e.Use();
            Repaint();
            return;
        }

        if (e.type == EventType.MouseDrag && _dragMode != DragMode.None && _dragWindowIndex >= 0)
        {
            ApplyClipDrag(e);
            e.Use();
            Repaint();
            return;
        }

        if (e.type == EventType.MouseUp && e.button == 0)
        {
            _dragMode = DragMode.None;
            _dragWindowIndex = -1;
            _dragMarkerIndex = -1;
            return;
        }

        if (e.type != EventType.MouseDown || e.button != 0)
        {
            return;
        }

        var mp = e.mousePosition;
        if (!_laneRect.Contains(mp))
        {
            return;
        }

        var norm = Snap(XToTime(_laneRect, mp.x));

        if (TryPickTeleport(norm))
        {
            e.Use();
            Repaint();
            return;
        }

        var rowIndex = Mathf.FloorToInt((mp.y - _firstRowY) / (RowHeight + 2f));
        if (rowIndex < 0 || rowIndex >= ActiveTracks.Length)
        {
            return;
        }

        var track = ActiveTracks[rowIndex];
        _lastClickedTrack = track;

        if (IsPresentationTrack(track))
        {
            if (TryBeginMarkerDrag(norm, track))
            {
                e.Use();
                Repaint();
                return;
            }

            if (e.clickCount >= 2 && TryCreateMarkerOnTrack(track, norm))
            {
                e.Use();
                Repaint();
                return;
            }

            _selectedMarker = -1;
            e.Use();
            Repaint();
            return;
        }

        if (track == TrackId.Teleport)
        {
            Undo.RecordObject(_action, "Add Teleport");
            _teleports.arraySize++;
            var elem = _teleports.GetArrayElementAtIndex(_teleports.arraySize - 1);
            elem.FindPropertyRelative(nameof(TeleportTrigger.TriggerTime)).floatValue = norm;
            _selectedTeleport = _teleports.arraySize - 1;
            _selectedWindow = -1;
            e.Use();
            Repaint();
            return;
        }

        if (TryBeginClipDrag(mp, norm, track))
        {
            e.Use();
            Repaint();
            return;
        }

        if (e.clickCount >= 2 && TryCreateClipOnTrack(track, norm))
        {
            e.Use();
            Repaint();
            return;
        }

        _selectedWindow = -1;
        _selectedTeleport = -1;
        e.Use();
        Repaint();
    }

    bool TryPickTeleport(float norm)
    {
        if (_teleports == null)
        {
            return false;
        }

        for (var i = 0; i < _teleports.arraySize; i++)
        {
            var t = _teleports.GetArrayElementAtIndex(i)
                .FindPropertyRelative(nameof(TeleportTrigger.TriggerTime)).floatValue;
            if (Mathf.Abs(norm - t) < 0.015f / _zoom)
            {
                _selectedTeleport = i;
                _selectedWindow = -1;
                _selectedMarker = -1;
                return true;
            }
        }

        return false;
    }

    bool TryBeginClipDrag(Vector2 mp, float norm, TrackId track)
    {
        if (_windows == null)
        {
            return false;
        }

        for (var i = 0; i < _windows.arraySize; i++)
        {
            var elem = _windows.GetArrayElementAtIndex(i);
            var w = ReadWindow(elem);
            if (!WindowHitOnTrack(elem, w, track, norm))
            {
                continue;
            }

            var barRect = new Rect(_laneRect.x, 0f, _laneRect.width, RowHeight);
            var start = TimeToX(barRect, w.NormalizedStart);
            var end = TimeToX(barRect, w.NormalizedEnd);
            var seg = new Rect(start, 0f, Mathf.Max(2f, end - start), RowHeight);

            _selectedWindow = i;
            _selectedTeleport = -1;
            _dragWindowIndex = i;
            _dragAnchorNorm = norm;
            _dragOrigStart = w.NormalizedStart;
            _dragOrigEnd = w.NormalizedEnd;

            if (mp.x <= seg.xMin + HandleWidth)
            {
                _dragMode = DragMode.ResizeStart;
            }
            else if (mp.x >= seg.xMax - HandleWidth)
            {
                _dragMode = DragMode.ResizeEnd;
            }
            else
            {
                _dragMode = DragMode.MoveClip;
            }

            Undo.RecordObject(_action, "Edit Action Window");
            return true;
        }

        return false;
    }

    void ApplyClipDrag(Event e)
    {
        if (_dragWindowIndex < 0 || _dragWindowIndex >= _windows.arraySize)
        {
            return;
        }

        var elem = _windows.GetArrayElementAtIndex(_dragWindowIndex);
        var pStart = elem.FindPropertyRelative(nameof(ActionWindow.NormalizedStart));
        var pEnd = elem.FindPropertyRelative(nameof(ActionWindow.NormalizedEnd));
        var norm = Snap(XToTime(_laneRect, e.mousePosition.x));
        var delta = norm - _dragAnchorNorm;

        switch (_dragMode)
        {
            case DragMode.MoveClip:
            {
                var len = _dragOrigEnd - _dragOrigStart;
                var s = Snap(Mathf.Clamp(_dragOrigStart + delta, 0f, 1f - len));
                pStart.floatValue = s;
                pEnd.floatValue = s + len;
                break;
            }
            case DragMode.ResizeStart:
            {
                var end = _dragOrigEnd;
                var s = Snap(Mathf.Clamp(norm, 0f, end - MinClipDuration));
                pStart.floatValue = s;
                pEnd.floatValue = end;
                break;
            }
            case DragMode.ResizeEnd:
            {
                var start = _dragOrigStart;
                var end = Snap(Mathf.Clamp(norm, start + MinClipDuration, 1f));
                pStart.floatValue = start;
                pEnd.floatValue = end;
                break;
            }
        }
    }

    bool TryCreateClipOnTrack(TrackId track, float norm)
    {
        if (track == TrackId.Teleport)
        {
            return false;
        }

        if (IsCombatCoreTrack(track) || IsPhaseTrack(track) || track == TrackId.Interrupt)
        {
            AddCombatClip(track, norm);
            return true;
        }

        if (track == TrackId.ComboInput || track == TrackId.RootMotion)
        {
            AddCombatClip(track, norm);
            return true;
        }

        return false;
    }

    void AddCombatClip(TrackId track, float normStart)
    {
        Undo.RecordObject(_action, "Add Action Window");
        _windows.arraySize++;
        var elem = _windows.GetArrayElementAtIndex(_windows.arraySize - 1);
        var start = Snap(normStart);
        var end = Snap(Mathf.Min(1f, start + DefaultCombatClipLength));
        ClearWindowElement(elem, start, end);

        ApplyTrackPreset(elem, track);
        _selectedWindow = _windows.arraySize - 1;
        _selectedTeleport = -1;
        _selectedMarker = -1;
    }

    void AddBlankWindow(float start, float end)
    {
        Undo.RecordObject(_action, "Add Action Window");
        _windows.arraySize++;
        var elem = _windows.GetArrayElementAtIndex(_windows.arraySize - 1);
        ClearWindowElement(elem, start, end);
        _selectedWindow = _windows.arraySize - 1;
        _selectedTeleport = -1;
        _selectedMarker = -1;
    }

    static void ApplyTrackPreset(SerializedProperty windowElem, TrackId track)
    {
        switch (track)
        {
            case TrackId.Hitbox:
                ActionDataTimelineSlots.SetTag(windowElem, StateTag.HitboxActive_Window, true);
                ActionDataTimelineSlots.SetTag(windowElem, StateTag.PhaseActive, true);
                break;
            case TrackId.Hurtbox:
                ActionDataTimelineSlots.SetTag(windowElem, StateTag.HurtboxActive_Window, true);
                break;
            case TrackId.Invincible:
                ActionDataTimelineSlots.SetTag(windowElem, StateTag.Invulnerable, true);
                break;
            case TrackId.PhaseStartup:
                ActionDataTimelineSlots.SetTag(windowElem, StateTag.PhaseStartup, true);
                break;
            case TrackId.PhaseActive:
                ActionDataTimelineSlots.SetTag(windowElem, StateTag.PhaseActive, true);
                break;
            case TrackId.PhaseRecovery:
                ActionDataTimelineSlots.SetTag(windowElem, StateTag.PhaseRecovery, true);
                break;
            case TrackId.ComboInput:
                ActionDataTimelineSlots.SetTag(windowElem, StateTag.ComboInput_Window, true);
                break;
            case TrackId.RootMotion:
                ActionDataTimelineSlots.SetTag(windowElem, StateTag.RootMotion_Window, true);
                break;
            case TrackId.Interrupt:
                windowElem.FindPropertyRelative(nameof(ActionWindow.InterruptibleByCategories)).enumValueFlag =
                    (int)ActionCategory.Movement;
                break;
        }
    }

    static bool WindowHitOnTrack(SerializedProperty elem, ActionWindow w, TrackId track, float norm)
    {
        if (norm < w.NormalizedStart || norm > w.NormalizedEnd)
        {
            return false;
        }

        return track == TrackId.RuntimeEvent
            ? HasRuntimeEvents(elem.FindPropertyRelative(nameof(ActionWindow.RuntimeEvents)))
            : WindowContributesToTrack(w, track);
    }

    static bool WindowHasTag(SerializedProperty windowElem, StateTag tag)
    {
        var w = ReadWindow(windowElem);
        return (w.ToInternalTagMask() & (ulong)tag) != 0;
    }

    static ActionWindow ReadWindow(SerializedProperty elem) => new ActionWindow
    {
        NormalizedStart = elem.FindPropertyRelative(nameof(ActionWindow.NormalizedStart)).floatValue,
        NormalizedEnd = elem.FindPropertyRelative(nameof(ActionWindow.NormalizedEnd)).floatValue,
        WindowSlotMask = (ulong)elem.FindPropertyRelative(nameof(ActionWindow.WindowSlotMask)).longValue,
        InterruptibleByCategories = (ActionCategory)elem
            .FindPropertyRelative(nameof(ActionWindow.InterruptibleByCategories))
            .enumValueFlag,
    };

    static bool HasRuntimeEvents(SerializedProperty listProp) =>
        listProp != null && listProp.isArray && listProp.arraySize > 0;

    static bool WindowContributesToTrack(ActionWindow w, TrackId track)
    {
        var mask = w.ToInternalTagMask() & ActionWindowTimelineMask.AllContributableBits;
        switch (track)
        {
            case TrackId.Interrupt:
                return w.InterruptibleByCategories != ActionCategory.None;
            case TrackId.PhaseStartup:
                return (mask & (ulong)StateTag.PhaseStartup) != 0;
            case TrackId.PhaseActive:
                return (mask & (ulong)StateTag.PhaseActive) != 0;
            case TrackId.PhaseRecovery:
                return (mask & (ulong)StateTag.PhaseRecovery) != 0;
            case TrackId.Invincible:
                return (mask & (ulong)StateTag.Invulnerable) != 0;
            case TrackId.Hitbox:
                return (mask & (ulong)StateTag.HitboxActive_Window) != 0;
            case TrackId.Hurtbox:
                return (mask & (ulong)StateTag.HurtboxActive_Window) != 0;
            case TrackId.ComboInput:
                return (mask & (ulong)StateTag.ComboInput_Window) != 0;
            case TrackId.RootMotion:
                return (mask & (ulong)StateTag.RootMotion_Window) != 0;
            default:
                return false;
        }
    }

    static bool IsCombatCoreTrack(TrackId track) =>
        track is TrackId.Hitbox or TrackId.Hurtbox or TrackId.Invincible;

    static bool IsPhaseTrack(TrackId track) =>
        track is TrackId.PhaseStartup or TrackId.PhaseActive or TrackId.PhaseRecovery;

    static string GetTrackLabel(TrackId track) => track switch
    {
        TrackId.Interrupt => "Interrupt",
        TrackId.PhaseStartup => "Phase · Startup",
        TrackId.PhaseActive => "Phase · Active",
        TrackId.PhaseRecovery => "Phase · Recovery",
        TrackId.Hitbox => "Hitbox ★",
        TrackId.Hurtbox => "Hurtbox ★",
        TrackId.Invincible => "Invincible ★",
        TrackId.ComboInput => "Combo Input",
        TrackId.RootMotion => "Root Motion",
        TrackId.RuntimeEvent => "Runtime Events",
        TrackId.Teleport => "Teleport ◆",
        TrackId.Fx => "FX ◆",
        TrackId.Audio => "Audio ◆",
        TrackId.Camera => "Camera",
        TrackId.TimeScale => "TimeScale",
        _ => track.ToString(),
    };

    static Color GetTrackColor(TrackId track) => track switch
    {
        TrackId.Interrupt => new Color(0.95f, 0.45f, 0.2f, 0.88f),
        TrackId.PhaseStartup => new Color(0.35f, 0.55f, 0.95f, 0.78f),
        TrackId.PhaseActive => new Color(0.2f, 0.75f, 0.45f, 0.78f),
        TrackId.PhaseRecovery => new Color(0.55f, 0.4f, 0.9f, 0.78f),
        TrackId.Hitbox => new Color(0.95f, 0.22f, 0.22f, 0.9f),
        TrackId.Hurtbox => new Color(0.85f, 0.35f, 0.95f, 0.88f),
        TrackId.Invincible => new Color(0.95f, 0.88f, 0.15f, 0.88f),
        TrackId.ComboInput => new Color(0.3f, 0.85f, 0.9f, 0.75f),
        TrackId.RootMotion => new Color(0.7f, 0.7f, 0.7f, 0.72f),
        TrackId.RuntimeEvent => new Color(0.85f, 0.65f, 0.15f, 0.68f),
        _ => Color.gray,
    };

    float TimeToX(Rect lane, float t) => lane.x + lane.width * Mathf.Clamp01(t);

    float XToTime(Rect lane, float x) => Mathf.Clamp01((x - lane.x) / lane.width);

    static float Snap(float t) => Mathf.Round(t / SnapStep) * SnapStep;
}

/// <summary>时间轴编辑器内 WindowSlotMask 读写。</summary>
static class ActionDataTimelineSlots
{
    public static void SetTag(SerializedProperty windowElem, StateTag tag, bool on)
    {
        var slot = FindSlotIndex(tag);
        if (slot < 0)
        {
            return;
        }

        var p = windowElem.FindPropertyRelative(nameof(ActionWindow.WindowSlotMask));
        var mask = (ulong)p.longValue;
        var bit = 1UL << slot;
        mask = on ? mask | bit : mask & ~bit;
        p.longValue = (long)mask;
    }

    static int FindSlotIndex(StateTag tag)
    {
        var bit = (ulong)tag;
        var map = ActionWindowTagSlots.SlotToInternalBit;
        for (var i = 0; i < ActionWindowTagSlots.SlotCount; i++)
        {
            if (map[i] == bit)
            {
                return i;
            }
        }

        return -1;
    }
}
#endif

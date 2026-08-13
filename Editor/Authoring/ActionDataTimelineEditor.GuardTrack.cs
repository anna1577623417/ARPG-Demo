#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 216.3 M5 L1 — Guard(DefenseClip) 防御轨：Active 拖拽/创建；列表管理。
/// </summary>
public sealed partial class ActionDataTimelineEditor
{
    const float DefaultDefenseClipLength = 0.2f;

    static readonly Color DefenseClipActiveColor = new Color(0.35f, 0.72f, 0.98f, 0.95f);
    static readonly Color DefenseClipSelectedColor = new Color(0.55f, 0.9f, 1f, 0.98f);
    static readonly Color DefenseClipDimColor = new Color(0.38f, 0.45f, 0.52f, 0.55f);

    SerializedProperty _defenseClips;
    int _selectedDefenseClip = -1;
    int _dragDefenseClipIndex = -1;
    bool _foldDefenseClipList = true;

    GUIStyle _defenseClipLabelStyle;
    GUIStyle _defenseClipDimLabelStyle;

    partial void RefreshGuardTrackProperties()
    {
        _defenseClips = _so?.FindProperty(nameof(ActionDataSO.DefenseClips));
    }

    void DrawGuardTrack(Rect barRect)
    {
        if (_defenseClips == null)
        {
            return;
        }

        EnsureDefenseLabelStyles();

        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < _defenseClips.arraySize; i++)
            {
                var elem = _defenseClips.GetArrayElementAtIndex(i);
                ReadDefenseActiveRange(elem, out var start, out var end);
                var playheadInside = IsPlayheadInsideDefense(start, end);
                var selected = _selectedDefenseClip == i;
                var isForeground = playheadInside || selected;
                if (pass == 0 && isForeground)
                {
                    continue;
                }

                if (pass == 1 && !isForeground)
                {
                    continue;
                }

                var x0 = TimeToX(barRect, start);
                var x1 = TimeToX(barRect, end);
                var seg = new Rect(x0, barRect.y + 2f, Mathf.Max(2f, x1 - x0), barRect.height - 4f);
                EditorGUI.DrawRect(seg, ResolveDefenseClipColor(selected, playheadInside));

                if (selected)
                {
                    EditorGUI.DrawRect(new Rect(seg.x, seg.y, 2f, seg.height), Color.white);
                    EditorGUI.DrawRect(new Rect(seg.xMax - 2f, seg.y, 2f, seg.height), Color.white);
                }

                if (playheadInside && !selected)
                {
                    EditorGUI.DrawRect(new Rect(seg.x, seg.y, seg.width, 2f), new Color(0.7f, 0.95f, 1f, 0.95f));
                }

                if (seg.width >= 28f)
                {
                    var name = elem.FindPropertyRelative(nameof(DefenseClip.DebugName)).stringValue;
                    var kind = (DefenseKind)elem.FindPropertyRelative(nameof(DefenseClip.Kind)).enumValueIndex;
                    var label = string.IsNullOrEmpty(name) ? kind.ToString() : name;
                    if (playheadInside)
                    {
                        label = $"▶ {label}";
                    }

                    GUI.Label(seg, label, playheadInside || selected ? _defenseClipLabelStyle : _defenseClipDimLabelStyle);
                }
            }
        }
    }

    static Color ResolveDefenseClipColor(bool selected, bool playheadInside)
    {
        if (selected)
        {
            return DefenseClipSelectedColor;
        }

        return playheadInside ? DefenseClipActiveColor : DefenseClipDimColor;
    }

    bool IsPlayheadInsideDefense(float start, float end) =>
        _previewTime >= start && _previewTime <= end;

    static void ReadDefenseActiveRange(SerializedProperty elem, out float start, out float end)
    {
        start = Mathf.Clamp01(elem.FindPropertyRelative(nameof(DefenseClip.ActiveStart)).floatValue);
        end = Mathf.Clamp01(elem.FindPropertyRelative(nameof(DefenseClip.ActiveEnd)).floatValue);
        if (end < start)
        {
            (start, end) = (end, start);
        }
    }

    void EnsureDefenseLabelStyles()
    {
        if (_defenseClipLabelStyle == null)
        {
            _defenseClipLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 1f, 0.95f) },
                fontStyle = FontStyle.Bold,
            };
        }

        if (_defenseClipDimLabelStyle == null)
        {
            _defenseClipDimLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f, 0.55f) },
            };
        }
    }

    partial void HandleGuardTrackInput(TrackId track, float norm, Event e, Rect barRect)
    {
        if (track != TrackId.Guard || _defenseClips == null)
        {
            return;
        }

        if (TryBeginDefenseClipDrag(e.mousePosition, norm, barRect))
        {
            e.Use();
            Repaint();
            return;
        }

        if (e.clickCount >= 2)
        {
            AddDefenseClipAt(norm);
            e.Use();
            Repaint();
            return;
        }

        _selectedDefenseClip = -1;
        e.Use();
        Repaint();
    }

    bool TryBeginDefenseClipDrag(Vector2 mp, float norm, Rect barRect)
    {
        if (_defenseClips == null || !IsTrackEditable(TrackId.Guard))
        {
            return false;
        }

        for (var i = _defenseClips.arraySize - 1; i >= 0; i--)
        {
            var elem = _defenseClips.GetArrayElementAtIndex(i);
            ReadDefenseActiveRange(elem, out var start, out var end);

            if (norm < start - 0.01f / _zoom || norm > end + 0.01f / _zoom)
            {
                continue;
            }

            var x0 = TimeToX(barRect, start);
            var x1 = TimeToX(barRect, end);
            var seg = new Rect(x0, barRect.y, Mathf.Max(2f, x1 - x0), barRect.height);

            ClearNonGuardSelection();
            _selectedDefenseClip = i;
            _dragDefenseClipIndex = i;
            _dragAnchorNorm = norm;
            _dragOrigStart = start;
            _dragOrigEnd = end;

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

            Undo.RecordObject(_action, "Edit DefenseClip Active");
            return true;
        }

        return false;
    }

    void ApplyDefenseClipDrag(Event e)
    {
        if (_defenseClips == null
            || _dragDefenseClipIndex < 0
            || _dragDefenseClipIndex >= _defenseClips.arraySize)
        {
            return;
        }

        var elem = _defenseClips.GetArrayElementAtIndex(_dragDefenseClipIndex);
        var pStart = elem.FindPropertyRelative(nameof(DefenseClip.ActiveStart));
        var pEnd = elem.FindPropertyRelative(nameof(DefenseClip.ActiveEnd));
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

    void AddDefenseClipAt(float normStart)
    {
        if (_defenseClips == null)
        {
            return;
        }

        Undo.RecordObject(_action, "Add DefenseClip");
        _defenseClips.arraySize++;
        var elem = _defenseClips.GetArrayElementAtIndex(_defenseClips.arraySize - 1);
        WriteDefaultDefenseClip(elem, Snap(normStart), _defenseClips.arraySize - 1);

        ClearNonGuardSelection();
        _selectedDefenseClip = _defenseClips.arraySize - 1;
    }

    void WriteDefaultDefenseClip(SerializedProperty elem, float start, int index)
    {
        var end = Snap(Mathf.Min(1f, start + DefaultDefenseClipLength));
        elem.FindPropertyRelative(nameof(DefenseClip.DebugName)).stringValue = $"Guard{index}";
        elem.FindPropertyRelative(nameof(DefenseClip.ActiveStart)).floatValue = start;
        elem.FindPropertyRelative(nameof(DefenseClip.ActiveEnd)).floatValue = end;
        elem.FindPropertyRelative(nameof(DefenseClip.Kind)).enumValueIndex = (int)DefenseKind.Guard;
        elem.FindPropertyRelative(nameof(DefenseClip.GuardAngleDegrees)).floatValue = 120f;
        elem.FindPropertyRelative(nameof(DefenseClip.GuardRange)).floatValue = 1.5f;
        elem.FindPropertyRelative(nameof(DefenseClip.GuardShape)).objectReferenceValue = null;
    }

    bool TryDrawGuardTrackInspector()
    {
        var show = _lastClickedTrack == TrackId.Guard || HasSelectedDefenseClip();
        if (!show || _defenseClips == null)
        {
            return false;
        }

        DrawDefenseClipListManager();

        if (!HasSelectedDefenseClip())
        {
            EditorGUILayout.HelpBox(
                "双击 Guard 轨或点「+ 段」添加 DefenseClip；Kind=Guard 时运行时开前向 Volume（angle 默认 120）。",
                MessageType.Info);
            return true;
        }

        var elem = _defenseClips.GetArrayElementAtIndex(_selectedDefenseClip);
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField($"DefenseClip #{_selectedDefenseClip}", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Guard：Active 内 GuardVolumeProvider 开窗 Log [Resolve] GUARD window on。Parry/Invincible → M5 L2。",
            MessageType.None);

        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(DefenseClip.DebugName)));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(DefenseClip.ActiveStart)));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(DefenseClip.ActiveEnd)));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(DefenseClip.Kind)));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(DefenseClip.GuardAngleDegrees)));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(DefenseClip.GuardRange)));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(DefenseClip.GuardShape)));
        return true;
    }

    void DrawDefenseClipListManager()
    {
        _foldDefenseClipList = ActionTimelineEditorUI.Foldout(
            _foldDefenseClipList,
            $"DefenseClips（{_defenseClips.arraySize}）");
        if (!_foldDefenseClipList)
        {
            return;
        }

        using (new EditorGUI.IndentLevelScope())
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ 段", EditorStyles.miniButtonLeft))
                {
                    AddDefenseClipAt(_previewTime);
                }

                using (new EditorGUI.DisabledScope(!HasSelectedDefenseClip()))
                {
                    if (GUILayout.Button("复制", EditorStyles.miniButtonMid))
                    {
                        DuplicateSelectedDefenseClip();
                    }

                    if (GUILayout.Button("↑", EditorStyles.miniButtonMid))
                    {
                        MoveSelectedDefenseClip(-1);
                    }

                    if (GUILayout.Button("↓", EditorStyles.miniButtonMid))
                    {
                        MoveSelectedDefenseClip(+1);
                    }

                    if (GUILayout.Button("删", EditorStyles.miniButtonRight))
                    {
                        TryDeleteSelectedDefenseClip();
                    }
                }
            }

            for (var i = 0; i < _defenseClips.arraySize; i++)
            {
                var elem = _defenseClips.GetArrayElementAtIndex(i);
                ReadDefenseActiveRange(elem, out var start, out var end);
                var name = elem.FindPropertyRelative(nameof(DefenseClip.DebugName)).stringValue;
                var kind = (DefenseKind)elem.FindPropertyRelative(nameof(DefenseClip.Kind)).enumValueIndex;
                if (string.IsNullOrEmpty(name))
                {
                    name = kind.ToString();
                }

                var playheadInside = IsPlayheadInsideDefense(start, end);
                var selected = _selectedDefenseClip == i;
                var mark = playheadInside ? "▶" : " ";
                var label = $"{mark} [{i}] {name} ({kind})  {start:F2}~{end:F2}";

                var prev = GUI.backgroundColor;
                if (selected)
                {
                    GUI.backgroundColor = new Color(0.55f, 0.9f, 1f, 1f);
                }
                else if (playheadInside)
                {
                    GUI.backgroundColor = new Color(0.4f, 0.7f, 0.95f, 1f);
                }

                if (GUILayout.Button(label, EditorStyles.miniButton))
                {
                    ClearNonGuardSelection();
                    _selectedDefenseClip = i;
                    _lastClickedTrack = TrackId.Guard;
                }

                GUI.backgroundColor = prev;
            }
        }
    }

    void DuplicateSelectedDefenseClip()
    {
        if (!HasSelectedDefenseClip())
        {
            return;
        }

        Undo.RecordObject(_action, "Duplicate DefenseClip");
        var srcIndex = _selectedDefenseClip;
        _defenseClips.InsertArrayElementAtIndex(srcIndex);
        var newIndex = srcIndex + 1;
        if (newIndex >= _defenseClips.arraySize)
        {
            newIndex = _defenseClips.arraySize - 1;
        }

        var copy = _defenseClips.GetArrayElementAtIndex(newIndex);
        var nameProp = copy.FindPropertyRelative(nameof(DefenseClip.DebugName));
        var baseName = nameProp.stringValue;
        if (string.IsNullOrEmpty(baseName))
        {
            baseName = $"Guard{srcIndex}";
        }

        nameProp.stringValue = baseName + "_copy";
        _selectedDefenseClip = newIndex;
    }

    void MoveSelectedDefenseClip(int delta)
    {
        if (!HasSelectedDefenseClip())
        {
            return;
        }

        var from = _selectedDefenseClip;
        var to = from + delta;
        if (to < 0 || to >= _defenseClips.arraySize)
        {
            return;
        }

        Undo.RecordObject(_action, "Reorder DefenseClip");
        _defenseClips.MoveArrayElement(from, to);
        _selectedDefenseClip = to;
    }

    bool HasSelectedDefenseClip() =>
        _selectedDefenseClip >= 0 && _defenseClips != null && _selectedDefenseClip < _defenseClips.arraySize;

    bool TryDeleteSelectedDefenseClip()
    {
        if (!HasSelectedDefenseClip())
        {
            return false;
        }

        Undo.RecordObject(_action, "Delete DefenseClip");
        _defenseClips.DeleteArrayElementAtIndex(_selectedDefenseClip);
        _selectedDefenseClip = _defenseClips.arraySize > 0
            ? Mathf.Clamp(_selectedDefenseClip, 0, _defenseClips.arraySize - 1)
            : -1;
        return true;
    }

    void ClearNonGuardSelection()
    {
        _selectedWindow = -1;
        _selectedTeleport = -1;
        _selectedMarker = -1;
        _selectedCombatEvent = -1;
    }

    void ClearGuardSelection()
    {
        _selectedDefenseClip = -1;
        _dragDefenseClipIndex = -1;
    }
}
#endif

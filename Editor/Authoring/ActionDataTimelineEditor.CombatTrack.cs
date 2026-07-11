#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 214.3 / 214.5 — Action 时间轴 Combat 轨（攻击帧 Marker + Lifecycle 区间）。
/// </summary>
public sealed partial class ActionDataTimelineEditor
{
    SerializedProperty _combatTrack;
    int _selectedCombatEvent = -1;
    float _dragCombatOrigNt;

    partial void RefreshCombatTrackProperties()
    {
        _combatTrack = _so?.FindProperty(nameof(ActionDataSO.CombatTrack));
    }

    Rect GetCombatEventSegmentRect(Rect barRect, int index, out float spawnNt, out float endNt)
    {
        spawnNt = 0f;
        endNt = 0f;
        if (_combatTrack == null || index < 0 || index >= _combatTrack.arraySize)
        {
            return default;
        }

        var elem = _combatTrack.GetArrayElementAtIndex(index);
        spawnNt = elem.FindPropertyRelative(nameof(CombatEvent.NormalizedTime)).floatValue;
        var def = elem.FindPropertyRelative(nameof(CombatEvent.Definition)).objectReferenceValue
            as CombatObjectDefinitionSO;
        var actionDur = CombatHitPreviewResolver.ResolveActionDurationSeconds(_action);
        var life = def != null ? def.Lifecycle.Duration : DefaultCombatClipLength;
        var widthNt = life > 0f ? life / actionDur : DefaultCombatClipLength / actionDur;
        endNt = Mathf.Clamp01(spawnNt + widthNt);

        var startX = TimeToX(barRect, spawnNt);
        var endX = TimeToX(barRect, endNt);
        return new Rect(startX, barRect.y + 2f, Mathf.Max(8f, endX - startX), barRect.height - 4f);
    }

    void DrawCombatMarkers(Rect barRect)
    {
        if (_combatTrack == null)
        {
            return;
        }

        for (var i = 0; i < _combatTrack.arraySize; i++)
        {
            var seg = GetCombatEventSegmentRect(barRect, i, out var spawnNt, out var endNt);
            var selected = _selectedCombatEvent == i;
            EditorGUI.DrawRect(seg, selected ? new Color(1f, 0.75f, 0.2f, 0.95f) : new Color(0.95f, 0.45f, 0.15f, 0.85f));

            // ◆ Spawn 时刻（白菱形）
            var diamondX = seg.xMin + 3f;
            var diamond = new Rect(diamondX, seg.y + 2f, 7f, seg.height - 4f);
            EditorGUI.DrawRect(diamond, Color.white);

            // Lifecycle 结束竖线（尾）
            if (endNt > spawnNt + 0.001f)
            {
                var endX = TimeToX(barRect, endNt);
                var tail = new Rect(endX - 1f, seg.y, 2f, seg.height);
                EditorGUI.DrawRect(tail, new Color(1f, 1f, 1f, 0.85f));
            }
        }
    }

    bool TryPickCombatEvent(Rect barRect, Vector2 mousePos, out int index)
    {
        index = -1;
        if (_combatTrack == null || !barRect.Contains(mousePos))
        {
            return false;
        }

        for (var i = _combatTrack.arraySize - 1; i >= 0; i--)
        {
            var seg = GetCombatEventSegmentRect(barRect, i, out _, out _);
            var pick = seg;
            pick.xMin -= 3f;
            pick.xMax += 3f;
            if (!pick.Contains(mousePos))
            {
                continue;
            }

            index = i;
            _selectedCombatEvent = i;
            _selectedWindow = -1;
            _selectedTeleport = -1;
            _selectedMarker = -1;
            ClearAttackSelection();
            return true;
        }

        return false;
    }

    bool TryBeginCombatEventDrag(Rect barRect, float norm, Vector2 mousePos)
    {
        if (!TryPickCombatEvent(barRect, mousePos, out var idx))
        {
            return false;
        }

        _dragMode = DragMode.MoveMarker;
        _dragCombatEventIndex = idx;
        _dragCombatOrigNt = _combatTrack.GetArrayElementAtIndex(idx)
            .FindPropertyRelative(nameof(CombatEvent.NormalizedTime)).floatValue;
        _dragAnchorNorm = norm;
        return true;
    }

    void ApplyCombatEventDrag()
    {
        if (_dragCombatEventIndex < 0 || _combatTrack == null || _dragCombatEventIndex >= _combatTrack.arraySize)
        {
            return;
        }

        var norm = Snap(XToTime(_laneRect, Event.current.mousePosition.x));
        var delta = norm - _dragAnchorNorm;
        var elem = _combatTrack.GetArrayElementAtIndex(_dragCombatEventIndex);
        elem.FindPropertyRelative(nameof(CombatEvent.NormalizedTime)).floatValue =
            Snap(Mathf.Clamp01(_dragCombatOrigNt + delta));
    }

    bool TryCreateCombatEvent(float norm)
    {
        if (_combatTrack == null)
        {
            return false;
        }

        Undo.RecordObject(_action, "Add Combat Event");
        _combatTrack.arraySize++;
        var elem = _combatTrack.GetArrayElementAtIndex(_combatTrack.arraySize - 1);
        elem.FindPropertyRelative(nameof(CombatEvent.NormalizedTime)).floatValue = norm;
        _selectedCombatEvent = _combatTrack.arraySize - 1;
        _selectedWindow = -1;
        _selectedTeleport = -1;
        _selectedMarker = -1;
        return true;
    }

    bool TryDrawSelectedCombatEventInspector()
    {
        if (_selectedCombatEvent < 0 || _combatTrack == null || _selectedCombatEvent >= _combatTrack.arraySize)
        {
            return false;
        }

        var elem = _combatTrack.GetArrayElementAtIndex(_selectedCombatEvent);
        EditorGUILayout.LabelField($"Combat Event #{_selectedCombatEvent}", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Combat ◆ 轨 = 运行时攻击盒 Spawn 时刻。\n" +
            "◆ 白块 = NormalizedTime（穿越此时刻 Spawn 一次）。\n" +
            "橙色条长度 = CombatObject.Lifecycle.Duration（判定存活时间，非 Hitbox 红条）。",
            MessageType.Info);

        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(CombatEvent.NormalizedTime)));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(CombatEvent.Definition)));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(CombatEvent.OverrideSpawn)));
        if (elem.FindPropertyRelative(nameof(CombatEvent.OverrideSpawn)).boolValue)
        {
            EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(CombatEvent.SpawnSourceOverride)));
            EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(CombatEvent.LocalOffsetOverride)));
            EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(CombatEvent.LocalEulerOffsetOverride)));
        }

        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(CombatEvent.DebugLabel)));

        if (GUILayout.Button("对齐 Hitbox 窗中心"))
        {
            AlignCombatEventToHitboxMid(elem);
        }

        var def = elem.FindPropertyRelative(nameof(CombatEvent.Definition)).objectReferenceValue
            as CombatObjectDefinitionSO;
        if (def != null && GUILayout.Button("在 Scene 中编辑攻击盒"))
        {
            Selection.activeObject = def;
            var anchor = _gizmoAnchorOverride != null ? _gizmoAnchorOverride : Selection.activeTransform;
            CombatHitVolumeSceneEditor.SetActive(def, anchor);
        }

        return true;
    }

    void AlignCombatEventToHitboxMid(SerializedProperty combatElem)
    {
        if (_windows == null)
        {
            EditorGUILayout.HelpBox("Windows 未加载。", MessageType.Warning);
            return;
        }

        for (var i = 0; i < _windows.arraySize; i++)
        {
            var w = ReadWindow(_windows.GetArrayElementAtIndex(i));
            if (!WindowContributesToTrack(w, TrackId.Hitbox))
            {
                continue;
            }

            var mid = (w.NormalizedStart + w.NormalizedEnd) * 0.5f;
            Undo.RecordObject(_action, "Align Combat To Hitbox");
            combatElem.FindPropertyRelative(nameof(CombatEvent.NormalizedTime)).floatValue = Snap(mid);
            _so.ApplyModifiedProperties();
            EditorUtility.SetDirty(_action);
            Repaint();
            Debug.Log($"[CombatTrack] ALIGN hitbox window #{i} mid nt={Snap(mid):F3} ({w.NormalizedStart:F3}~{w.NormalizedEnd:F3})");
            return;
        }

        EditorGUILayout.HelpBox("未找到 Hitbox ★ 轨片段（需 WindowSlot 含 Hitbox 标签）。", MessageType.Warning);
    }

    bool HasSelectedCombatEvent() =>
        _selectedCombatEvent >= 0 && _combatTrack != null && _selectedCombatEvent < _combatTrack.arraySize;

    bool TryDeleteSelectedCombatEvent()
    {
        if (!HasSelectedCombatEvent())
        {
            return false;
        }

        Undo.RecordObject(_action, "Delete Combat Event");
        _combatTrack.DeleteArrayElementAtIndex(_selectedCombatEvent);
        _selectedCombatEvent = _combatTrack.arraySize > 0
            ? Mathf.Clamp(_selectedCombatEvent, 0, _combatTrack.arraySize - 1)
            : -1;
        return true;
    }

    partial void HandleCombatTrackInput(TrackId track, float norm, Event e, Rect barRect)
    {
        if (track != TrackId.Combat)
        {
            return;
        }

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            if (TryBeginCombatEventDrag(barRect, norm, e.mousePosition))
            {
                e.Use();
                Repaint();
                return;
            }

            if (e.clickCount >= 2 && TryCreateCombatEvent(norm))
            {
                e.Use();
                Repaint();
                return;
            }

            _selectedCombatEvent = -1;
            e.Use();
            Repaint();
        }
    }

    partial void ApplyCombatMarkerDragIfNeeded()
    {
        if (_dragMode != DragMode.MoveMarker || _dragCombatEventIndex < 0)
        {
            return;
        }

        ApplyCombatEventDrag();
    }
}
#endif

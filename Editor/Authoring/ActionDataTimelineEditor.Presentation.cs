#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed partial class ActionDataTimelineEditor
{
    static bool IsPresentationTrack(TrackId track) =>
        track is TrackId.Fx or TrackId.Audio or TrackId.Camera or TrackId.TimeScale;

    void AddMarkerAtPreview(ActionTimelineMarkerKind kind)
    {
        if (_markers == null || kind == ActionTimelineMarkerKind.None)
        {
            return;
        }

        Undo.RecordObject(_action, "Add Timeline Marker");
        _markers.arraySize++;
        var elem = _markers.GetArrayElementAtIndex(_markers.arraySize - 1);
        elem.FindPropertyRelative(nameof(ActionTimelineMarker.NormalizedTime)).floatValue = Snap(_previewTime);
        WriteMarkerKind(elem, kind);
        ApplyMarkerDefaults(elem, kind);
        _selectedMarker = _markers.arraySize - 1;
        _selectedWindow = -1;
        _selectedTeleport = -1;
    }

    static ActionTimelineMarkerKind ReadMarkerKind(SerializedProperty elem) =>
        (ActionTimelineMarkerKind)elem.FindPropertyRelative(nameof(ActionTimelineMarker.Kind)).intValue;

    static void WriteMarkerKind(SerializedProperty elem, ActionTimelineMarkerKind kind) =>
        elem.FindPropertyRelative(nameof(ActionTimelineMarker.Kind)).intValue = (int)kind;

    static void ApplyMarkerDefaults(SerializedProperty elem, ActionTimelineMarkerKind kind)
    {
        var duration = elem.FindPropertyRelative(nameof(ActionTimelineMarker.Duration));
        var intensity = elem.FindPropertyRelative(nameof(ActionTimelineMarker.Intensity));
        switch (kind)
        {
            case ActionTimelineMarkerKind.CameraShake:
                duration.floatValue = 0.12f;
                intensity.floatValue = 0.6f;
                break;
            case ActionTimelineMarkerKind.CameraPush:
                duration.floatValue = 0.2f;
                intensity.floatValue = 5f;
                break;
            case ActionTimelineMarkerKind.CameraLock:
                duration.floatValue = 0.15f;
                break;
            case ActionTimelineMarkerKind.TimeScaleHitStop:
                duration.floatValue = 0.06f;
                break;
            case ActionTimelineMarkerKind.TimeScaleSlowMo:
            case ActionTimelineMarkerKind.TimeScaleBulletTime:
                duration.floatValue = 0.12f;
                intensity.floatValue = 0.35f;
                break;
        }
    }

    void DrawPresentationMarkers(Rect barRect, TrackId track)
    {
        if (_markers == null)
        {
            return;
        }

        for (var i = 0; i < _markers.arraySize; i++)
        {
            var elem = _markers.GetArrayElementAtIndex(i);
            var kind = ReadMarkerKind(elem);
            if (!MarkerMatchesTrack(kind, track))
            {
                continue;
            }

            var t = elem.FindPropertyRelative(nameof(ActionTimelineMarker.NormalizedTime)).floatValue;
            var dur = elem.FindPropertyRelative(nameof(ActionTimelineMarker.Duration)).floatValue;
            if (ActionTimelineMarkerKinds.IsZone(kind) && dur > 0.001f)
            {
                var x0 = TimeToX(barRect, t);
                var x1 = TimeToX(barRect, Mathf.Clamp01(t + dur));
                var zone = new Rect(x0, barRect.y + 3f, Mathf.Max(2f, x1 - x0), barRect.height - 6f);
                EditorGUI.DrawRect(zone, GetPresentationZoneColor(kind));
            }

            var x = TimeToX(barRect, t);
            var r = new Rect(x - 4f, barRect.y + 4f, 8f, barRect.height - 8f);
            EditorGUI.DrawRect(r, _selectedMarker == i ? Color.white : GetPresentationMarkerColor(kind));
        }
    }

    bool TryPickMarker(float norm, TrackId track)
    {
        if (_markers == null)
        {
            return false;
        }

        for (var i = 0; i < _markers.arraySize; i++)
        {
            var elem = _markers.GetArrayElementAtIndex(i);
            var kind = ReadMarkerKind(elem);
            if (!MarkerMatchesTrack(kind, track))
            {
                continue;
            }

            var t = elem.FindPropertyRelative(nameof(ActionTimelineMarker.NormalizedTime)).floatValue;
            if (Mathf.Abs(norm - t) < 0.018f / _zoom)
            {
                _selectedMarker = i;
                _selectedWindow = -1;
                _selectedTeleport = -1;
                return true;
            }
        }

        return false;
    }

    bool TryBeginMarkerDrag(float norm, TrackId track)
    {
        if (_markers == null)
        {
            return false;
        }

        for (var i = 0; i < _markers.arraySize; i++)
        {
            var elem = _markers.GetArrayElementAtIndex(i);
            var kind = ReadMarkerKind(elem);
            if (!MarkerMatchesTrack(kind, track))
            {
                continue;
            }

            var t = elem.FindPropertyRelative(nameof(ActionTimelineMarker.NormalizedTime)).floatValue;
            if (Mathf.Abs(norm - t) < 0.018f / _zoom)
            {
                _selectedMarker = i;
                if (IsCameraMarkerKind(kind))
                {
                    _preferredCameraKind = kind;
                }

                _dragMarkerIndex = i;
                _dragOrigMarkerTime = t;
                _dragAnchorNorm = norm;
                _dragMode = DragMode.MoveMarker;
                Undo.RecordObject(_action, "Move Timeline Marker");
                return true;
            }
        }

        return false;
    }

    void ApplyMarkerDrag()
    {
        if (_dragMarkerIndex < 0 || _markers == null || _dragMarkerIndex >= _markers.arraySize)
        {
            return;
        }

        var elem = _markers.GetArrayElementAtIndex(_dragMarkerIndex);
        var norm = Snap(XToTime(_laneRect, Event.current.mousePosition.x));
        elem.FindPropertyRelative(nameof(ActionTimelineMarker.NormalizedTime)).floatValue = norm;
    }

    bool TryCreateMarkerOnTrack(TrackId track, float norm)
    {
        if (!IsPresentationTrack(track))
        {
            return false;
        }

        var kind = TrackToDefaultMarkerKind(track);
        if (kind == ActionTimelineMarkerKind.None)
        {
            return false;
        }

        AddMarkerAtPreview(kind);
        if (_selectedMarker < 0 || _markers == null || _selectedMarker >= _markers.arraySize)
        {
            return false;
        }

        var elem = _markers.GetArrayElementAtIndex(_selectedMarker);
        elem.FindPropertyRelative(nameof(ActionTimelineMarker.NormalizedTime)).floatValue = norm;
        return true;
    }

    static bool IsCameraMarkerKind(ActionTimelineMarkerKind kind) =>
        kind is ActionTimelineMarkerKind.CameraShake
            or ActionTimelineMarkerKind.CameraPush
            or ActionTimelineMarkerKind.CameraLock;

    ActionTimelineMarkerKind TrackToDefaultMarkerKind(TrackId track) => track switch
    {
        TrackId.Fx => ActionTimelineMarkerKind.SpawnVfx,
        TrackId.Audio => ActionTimelineMarkerKind.PlaySfx,
        TrackId.Camera => _preferredCameraKind,
        TrackId.TimeScale => _preferredTimeScaleKind,
        _ => ActionTimelineMarkerKind.None,
    };

    static bool MarkerMatchesTrack(ActionTimelineMarkerKind kind, TrackId track) => track switch
    {
        TrackId.Fx => kind == ActionTimelineMarkerKind.SpawnVfx,
        TrackId.Audio => kind == ActionTimelineMarkerKind.PlaySfx,
        TrackId.Camera => kind is ActionTimelineMarkerKind.CameraShake
            or ActionTimelineMarkerKind.CameraPush
            or ActionTimelineMarkerKind.CameraLock,
        TrackId.TimeScale => kind is ActionTimelineMarkerKind.TimeScaleHitStop
            or ActionTimelineMarkerKind.TimeScaleSlowMo
            or ActionTimelineMarkerKind.TimeScaleBulletTime,
        _ => false,
    };

    static Color GetPresentationMarkerColor(ActionTimelineMarkerKind kind) => kind switch
    {
        ActionTimelineMarkerKind.SpawnVfx => new Color(0.95f, 0.4f, 0.95f),
        ActionTimelineMarkerKind.PlaySfx => new Color(0.45f, 0.85f, 1f),
        ActionTimelineMarkerKind.CameraShake => new Color(0.75f, 0.75f, 1f),
        ActionTimelineMarkerKind.CameraPush => new Color(0.55f, 0.65f, 1f),
        ActionTimelineMarkerKind.CameraLock => new Color(0.35f, 0.45f, 0.95f),
        ActionTimelineMarkerKind.TimeScaleHitStop => new Color(1f, 0.55f, 0.2f),
        ActionTimelineMarkerKind.TimeScaleSlowMo => new Color(1f, 0.75f, 0.35f),
        ActionTimelineMarkerKind.TimeScaleBulletTime => new Color(1f, 0.35f, 0.35f),
        _ => Color.gray,
    };

    static Color GetPresentationZoneColor(ActionTimelineMarkerKind kind)
    {
        var c = GetPresentationMarkerColor(kind);
        c.a = 0.35f;
        return c;
    }

    void DrawRuntimeEventTrackExtras(Rect barRect)
    {
        DrawMarkersOfKind(barRect, ActionTimelineMarkerKind.HitFrame);

        if (_windows == null)
        {
            return;
        }

        for (var wi = 0; wi < _windows.arraySize; wi++)
        {
            var elem = _windows.GetArrayElementAtIndex(wi);
            var events = elem.FindPropertyRelative(nameof(ActionWindow.RuntimeEvents));
            if (events == null || !events.isArray)
            {
                continue;
            }

            var s = elem.FindPropertyRelative(nameof(ActionWindow.NormalizedStart)).floatValue;
            var e = elem.FindPropertyRelative(nameof(ActionWindow.NormalizedEnd)).floatValue;
            for (var i = 0; i < events.arraySize; i++)
            {
                var ev = events.GetArrayElementAtIndex(i);
                var off = ev.FindPropertyRelative(nameof(ActionWindowEvent.NormalizedOffset)).floatValue;
                var t = Mathf.Lerp(Mathf.Min(s, e), Mathf.Max(s, e), Mathf.Clamp01(off));
                var x = TimeToX(barRect, t);
                var r = new Rect(x - 3f, barRect.y + 5f, 6f, 6f);
                EditorGUI.DrawRect(r, new Color(1f, 0.85f, 0.2f, 0.9f));
            }
        }
    }

    ActionTimelineMarkerKind _preferredTimeScaleKind = ActionTimelineMarkerKind.TimeScaleHitStop;

    void DrawCameraTrackQuickAdd()
    {
        EditorGUILayout.LabelField("镜头", EditorStyles.miniLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawCameraKindButton("Shake", ActionTimelineMarkerKind.CameraShake);
            DrawCameraKindButton("Push", ActionTimelineMarkerKind.CameraPush);
            DrawCameraKindButton("Lock", ActionTimelineMarkerKind.CameraLock);
        }
    }

    void DrawTimeScaleTrackQuickAdd()
    {
        EditorGUILayout.LabelField("时间缩放", EditorStyles.miniLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("HitStop", EditorStyles.miniButtonLeft, GUILayout.Height(20f)))
            {
                _preferredTimeScaleKind = ActionTimelineMarkerKind.TimeScaleHitStop;
                AddMarkerAtPreview(ActionTimelineMarkerKind.TimeScaleHitStop);
            }

            if (GUILayout.Button("SlowMo 区间", EditorStyles.miniButtonMid, GUILayout.Height(20f)))
            {
                _preferredTimeScaleKind = ActionTimelineMarkerKind.TimeScaleSlowMo;
                AddMarkerAtPreview(ActionTimelineMarkerKind.TimeScaleSlowMo);
            }

            if (GUILayout.Button("Bullet 区间", EditorStyles.miniButtonRight, GUILayout.Height(20f)))
            {
                _preferredTimeScaleKind = ActionTimelineMarkerKind.TimeScaleBulletTime;
                AddMarkerAtPreview(ActionTimelineMarkerKind.TimeScaleBulletTime);
            }
        }
    }

    void DrawCameraKindButton(string label, ActionTimelineMarkerKind kind)
    {
        var active = _preferredCameraKind == kind;
        var prevBg = GUI.backgroundColor;
        if (active)
        {
            GUI.backgroundColor = new Color(0.38f, 0.58f, 0.92f);
        }

        if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Height(20f)))
        {
            _preferredCameraKind = kind;
            _lastClickedTrack = TrackId.Camera;
            AddMarkerAtPreview(kind);
        }

        GUI.backgroundColor = prevBg;
    }

    void DrawCameraTrackInspectorHint()
    {
        EditorGUILayout.LabelField("Camera 轨", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "在 Preview 时刻点 Shake / Push / Lock 创建标记；或双击 Camera 轨空白处（使用当前首选类型）。",
            MessageType.None);
        DrawCameraKindToolbar(null, readOnly: true);
    }

    void DrawMarkerInspector(SerializedProperty elem)
    {
        var pKind = elem.FindPropertyRelative(nameof(ActionTimelineMarker.Kind));
        var kind = ReadMarkerKind(elem);

        EditorGUILayout.LabelField($"Marker [{_selectedMarker}] · {kind}", EditorStyles.boldLabel);

        if (IsCameraMarkerKind(kind))
        {
            DrawCameraKindToolbar(elem, readOnly: false);
            DrawCameraMarkerFields(elem, kind);
            return;
        }

        if (IsTimeScaleMarkerKind(kind))
        {
            DrawTimeScaleKindToolbar(elem);
            DrawTimeScaleMarkerFields(elem, kind);
            return;
        }

        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(ActionTimelineMarker.NormalizedTime)));
        EditorGUILayout.PropertyField(pKind);
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(ActionTimelineMarker.Payload)));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(ActionTimelineMarker.Duration)));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(ActionTimelineMarker.Intensity)));
    }

    static bool IsTimeScaleMarkerKind(ActionTimelineMarkerKind kind) =>
        kind is ActionTimelineMarkerKind.TimeScaleHitStop
            or ActionTimelineMarkerKind.TimeScaleSlowMo
            or ActionTimelineMarkerKind.TimeScaleBulletTime;

    void DrawCameraKindToolbar(SerializedProperty elem, bool readOnly)
    {
        EditorGUILayout.LabelField("镜头类型", EditorStyles.miniBoldLabel);
        using (new EditorGUI.DisabledScope(readOnly))
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawCameraKindToggle(elem, "Shake", ActionTimelineMarkerKind.CameraShake, EditorStyles.miniButtonLeft);
            DrawCameraKindToggle(elem, "Push", ActionTimelineMarkerKind.CameraPush, EditorStyles.miniButtonMid);
            DrawCameraKindToggle(elem, "Lock", ActionTimelineMarkerKind.CameraLock, EditorStyles.miniButtonRight);
        }
    }

    void DrawCameraKindToggle(
        SerializedProperty elem,
        string label,
        ActionTimelineMarkerKind kind,
        GUIStyle style)
    {
        var current = elem != null
            ? ReadMarkerKind(elem)
            : _preferredCameraKind;
        var on = current == kind;
        var prevBg = GUI.backgroundColor;
        if (on)
        {
            GUI.backgroundColor = new Color(0.38f, 0.58f, 0.92f);
        }

        if (GUILayout.Button(label, style, GUILayout.Height(20f)))
        {
            _preferredCameraKind = kind;
            _lastClickedTrack = TrackId.Camera;
            if (elem != null && !on)
            {
                Undo.RecordObject(_action, "Set Camera Marker Kind");
                SetMarkerKind(elem, kind);
            }
        }

        GUI.backgroundColor = prevBg;
    }

    void ShowCameraTrackContextMenu()
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("首选：Shake"), _preferredCameraKind == ActionTimelineMarkerKind.CameraShake,
            () => _preferredCameraKind = ActionTimelineMarkerKind.CameraShake);
        menu.AddItem(new GUIContent("首选：Push"), _preferredCameraKind == ActionTimelineMarkerKind.CameraPush,
            () => _preferredCameraKind = ActionTimelineMarkerKind.CameraPush);
        menu.AddItem(new GUIContent("首选：Lock"), _preferredCameraKind == ActionTimelineMarkerKind.CameraLock,
            () => _preferredCameraKind = ActionTimelineMarkerKind.CameraLock);
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("在 Preview 创建 Shake"), false,
            () => AddMarkerAtPreview(ActionTimelineMarkerKind.CameraShake));
        menu.AddItem(new GUIContent("在 Preview 创建 Push"), false,
            () => AddMarkerAtPreview(ActionTimelineMarkerKind.CameraPush));
        menu.AddItem(new GUIContent("在 Preview 创建 Lock"), false,
            () => AddMarkerAtPreview(ActionTimelineMarkerKind.CameraLock));
        menu.ShowAsContext();
    }

    void DrawTimeScaleKindToolbar(SerializedProperty elem)
    {
        EditorGUILayout.LabelField("时间缩放类型", EditorStyles.miniBoldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawTimeScaleKindToggle(elem, "HitStop", ActionTimelineMarkerKind.TimeScaleHitStop, EditorStyles.miniButtonLeft);
            DrawTimeScaleKindToggle(elem, "SlowMo", ActionTimelineMarkerKind.TimeScaleSlowMo, EditorStyles.miniButtonMid);
            DrawTimeScaleKindToggle(elem, "Bullet", ActionTimelineMarkerKind.TimeScaleBulletTime, EditorStyles.miniButtonRight);
        }
    }

    void DrawTimeScaleKindToggle(
        SerializedProperty elem,
        string label,
        ActionTimelineMarkerKind kind,
        GUIStyle style)
    {
        var pKind = elem.FindPropertyRelative(nameof(ActionTimelineMarker.Kind));
        var current = ReadMarkerKind(elem);
        var on = current == kind;
        var prevBg = GUI.backgroundColor;
        if (on)
        {
            GUI.backgroundColor = new Color(0.92f, 0.62f, 0.28f);
        }

        if (GUILayout.Button(label, style, GUILayout.Height(20f)) && !on)
        {
            Undo.RecordObject(_action, "Set TimeScale Marker Kind");
            SetMarkerKind(elem, kind);
            _preferredTimeScaleKind = kind;
        }

        GUI.backgroundColor = prevBg;
    }

    void SetMarkerKind(SerializedProperty elem, ActionTimelineMarkerKind kind)
    {
        WriteMarkerKind(elem, kind);
        ApplyMarkerDefaults(elem, kind);
    }

    void DrawCameraMarkerFields(SerializedProperty elem, ActionTimelineMarkerKind kind)
    {
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(ActionTimelineMarker.NormalizedTime)));
        EditorGUILayout.PropertyField(
            elem.FindPropertyRelative(nameof(ActionTimelineMarker.Payload)),
            new GUIContent("Payload", "可选：镜头配置 id / 备注"));

        switch (kind)
        {
            case ActionTimelineMarkerKind.CameraShake:
                EditorGUILayout.PropertyField(
                    elem.FindPropertyRelative(nameof(ActionTimelineMarker.Duration)),
                    new GUIContent("时长 (秒)", "震动持续真实秒数"));
                EditorGUILayout.PropertyField(
                    elem.FindPropertyRelative(nameof(ActionTimelineMarker.Intensity)),
                    new GUIContent("幅度", "Shake 强度"));
                break;
            case ActionTimelineMarkerKind.CameraPush:
                EditorGUILayout.PropertyField(
                    elem.FindPropertyRelative(nameof(ActionTimelineMarker.Duration)),
                    new GUIContent("时长 (秒)", "Push 回弹真实秒数"));
                EditorGUILayout.PropertyField(
                    elem.FindPropertyRelative(nameof(ActionTimelineMarker.Intensity)),
                    new GUIContent("Pitch 推进 (°)", "俯仰推进量"));
                break;
            case ActionTimelineMarkerKind.CameraLock:
                EditorGUILayout.PropertyField(
                    elem.FindPropertyRelative(nameof(ActionTimelineMarker.Duration)),
                    new GUIContent("区间 (归一化)", "锁定视角的归一化长度"));
                EditorGUILayout.HelpBox("Lock 区间内禁止玩家视角输入。", MessageType.None);
                break;
        }
    }

    void DrawTimeScaleMarkerFields(SerializedProperty elem, ActionTimelineMarkerKind kind)
    {
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(ActionTimelineMarker.NormalizedTime)));
        switch (kind)
        {
            case ActionTimelineMarkerKind.TimeScaleHitStop:
                EditorGUILayout.PropertyField(
                    elem.FindPropertyRelative(nameof(ActionTimelineMarker.Duration)),
                    new GUIContent("停顿时长 (秒)", "HitStop 真实秒数"));
                break;
            default:
                EditorGUILayout.PropertyField(
                    elem.FindPropertyRelative(nameof(ActionTimelineMarker.Duration)),
                    new GUIContent("区间 (归一化)", "慢动作/子弹时间区间长度"));
                EditorGUILayout.PropertyField(
                    elem.FindPropertyRelative(nameof(ActionTimelineMarker.Intensity)),
                    new GUIContent("Time Scale", "0.35 ≈ 35% 速度"));
                break;
        }
    }

    void DrawMarkersOfKind(Rect barRect, ActionTimelineMarkerKind kind)
    {
        if (_markers == null)
        {
            return;
        }

        for (var i = 0; i < _markers.arraySize; i++)
        {
            var elem = _markers.GetArrayElementAtIndex(i);
            if (ReadMarkerKind(elem) != kind)
            {
                continue;
            }

            var t = elem.FindPropertyRelative(nameof(ActionTimelineMarker.NormalizedTime)).floatValue;
            var x = TimeToX(barRect, t);
            var r = new Rect(x - 4f, barRect.y + 4f, 8f, barRect.height - 8f);
            EditorGUI.DrawRect(r, _selectedMarker == i ? Color.white : GetPresentationMarkerColor(kind));
        }
    }
}
#endif

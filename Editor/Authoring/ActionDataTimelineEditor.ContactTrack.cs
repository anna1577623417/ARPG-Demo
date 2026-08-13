#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed partial class ActionDataTimelineEditor
{
    const float DefaultContactLength = 0.15f;

    SerializedProperty _contactEvents;
    string _selectedContactEventId;
    int _dragContactIndex = -1;
    bool _foldContactList = true;
    ContactAuthoringEditLayer _contactEditLayer = ContactAuthoringEditLayer.EventOverride;

    partial void RefreshContactTrackProperties()
    {
        _contactEvents = _so?.FindProperty(nameof(ActionDataSO.ContactEvents));
        EnsureContactIds();
        ResolveSelectedContactIndex();
    }

    void DrawContactTrack(Rect barRect)
    {
        if (_contactEvents == null) return;

        for (var i = 0; i < _contactEvents.arraySize; i++)
        {
            var elem = _contactEvents.GetArrayElementAtIndex(i);
            ReadContactRange(elem, out var start, out var end);
            var id = ReadContactId(elem);
            var selected = id == _selectedContactEventId;
            var active = _previewTime >= start && _previewTime <= end;
            var x0 = TimeToX(barRect, start);
            var x1 = TimeToX(barRect, end);
            var seg = new Rect(x0, barRect.y + 2f, Mathf.Max(2f, x1 - x0), barRect.height - 4f);
            EditorGUI.DrawRect(
                seg,
                selected
                    ? new Color(1f, 0.72f, 0.30f, 0.98f)
                    : active
                        ? ContactHitboxColor
                        : new Color(0.58f, 0.08f, 0.05f, 0.78f));

            if (selected)
            {
                EditorGUI.DrawRect(new Rect(seg.x, seg.y, 2f, seg.height), Color.white);
                EditorGUI.DrawRect(new Rect(seg.xMax - 2f, seg.y, 2f, seg.height), Color.white);
            }

            if (seg.width >= 32f)
            {
                var debugName = elem.FindPropertyRelative(nameof(ContactEvent.DebugName)).stringValue;
                GUI.Label(seg, string.IsNullOrEmpty(debugName) ? $"Hitbox {i}" : debugName, EditorStyles.miniBoldLabel);
            }
        }
    }

    partial void HandleContactTrackInput(TrackId track, float norm, Event e, Rect barRect)
    {
        if (track != TrackId.Contact || _contactEvents == null) return;

        if (TryBeginContactDrag(e.mousePosition, norm, barRect))
        {
            e.Use();
            Repaint();
            return;
        }

        if (e.clickCount >= 2)
        {
            AddContactAt(norm);
            e.Use();
            Repaint();
            return;
        }

        ClearContactSelection();
        e.Use();
        Repaint();
    }

    bool TryBeginContactDrag(Vector2 mousePosition, float norm, Rect barRect)
    {
        for (var i = _contactEvents.arraySize - 1; i >= 0; i--)
        {
            var elem = _contactEvents.GetArrayElementAtIndex(i);
            ReadContactRange(elem, out var start, out var end);
            var seg = new Rect(
                TimeToX(barRect, start),
                barRect.y,
                Mathf.Max(2f, TimeToX(barRect, end) - TimeToX(barRect, start)),
                barRect.height);
            if (!seg.Contains(mousePosition)) continue;

            ClearAllButContactSelection();
            _selectedContactEventId = ReadContactId(elem);
            _dragContactIndex = i;
            _dragAnchorNorm = norm;
            _dragOrigStart = start;
            _dragOrigEnd = end;
            _dragMode = mousePosition.x <= seg.xMin + HandleWidth
                ? DragMode.ResizeStart
                : mousePosition.x >= seg.xMax - HandleWidth
                    ? DragMode.ResizeEnd
                    : DragMode.MoveClip;
            Undo.RecordObject(_action, "Edit Contact Event");
            PublishContactSelection();
            return true;
        }

        return false;
    }

    void ApplyContactDrag(Event e)
    {
        if (_contactEvents == null || _dragContactIndex < 0 || _dragContactIndex >= _contactEvents.arraySize) return;

        var elem = _contactEvents.GetArrayElementAtIndex(_dragContactIndex);
        var startProp = elem.FindPropertyRelative(nameof(ContactEvent.ActiveStart));
        var endProp = elem.FindPropertyRelative(nameof(ContactEvent.ActiveEnd));
        var norm = Snap(XToTime(_laneRect, e.mousePosition.x));
        var delta = norm - _dragAnchorNorm;
        switch (_dragMode)
        {
            case DragMode.MoveClip:
            {
                var length = _dragOrigEnd - _dragOrigStart;
                var start = Snap(Mathf.Clamp(_dragOrigStart + delta, 0f, 1f - length));
                startProp.floatValue = start;
                endProp.floatValue = start + length;
                break;
            }
            case DragMode.ResizeStart:
                startProp.floatValue = Snap(Mathf.Clamp(norm, 0f, _dragOrigEnd - MinClipDuration));
                break;
            case DragMode.ResizeEnd:
                endProp.floatValue = Snap(Mathf.Clamp(norm, _dragOrigStart + MinClipDuration, 1f));
                break;
        }
    }

    void AddContactAt(float normalizedStart)
    {
        Undo.RecordObject(_action, "Add Contact Event");
        _contactEvents.arraySize++;
        var index = _contactEvents.arraySize - 1;
        var elem = _contactEvents.GetArrayElementAtIndex(index);
        var eventId = ContactEventId.NewId();
        elem.FindPropertyRelative(nameof(ContactEvent.EventId)).stringValue = eventId;
        elem.FindPropertyRelative(nameof(ContactEvent.DebugName)).stringValue = $"Hitbox{index}";
        elem.FindPropertyRelative(nameof(ContactEvent.ActiveStart)).floatValue = Snap(normalizedStart);
        elem.FindPropertyRelative(nameof(ContactEvent.ActiveEnd)).floatValue =
            Snap(Mathf.Min(1f, normalizedStart + DefaultContactLength));
        elem.FindPropertyRelative(nameof(ContactEvent.Definition)).objectReferenceValue = null;
        ResetContactOverride(elem.FindPropertyRelative(nameof(ContactEvent.Override)));
        ClearAllButContactSelection();
        _selectedContactEventId = eventId;
        PublishContactSelection();
    }

    bool TryDrawContactTrackInspector()
    {
        if (_lastClickedTrack != TrackId.Contact && !HasSelectedContact()) return false;

        DrawContactList();
        var index = ResolveSelectedContactIndex();
        if (index < 0)
        {
            EditorGUILayout.HelpBox(
                "双击 Hitbox 轨创建 Action Contact。时间窗口由 ContactEvent.ActiveStart/ActiveEnd 统一驱动。",
                MessageType.Info);
            return true;
        }

        var elem = _contactEvents.GetArrayElementAtIndex(index);
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField($"Hitbox · {ShortId(_selectedContactEventId)}", EditorStyles.boldLabel);

        var nextLayer = (ContactAuthoringEditLayer)EditorGUILayout.EnumPopup("Edit Layer", _contactEditLayer);
        if (nextLayer != _contactEditLayer)
        {
            _contactEditLayer = nextLayer;
            PublishContactSelection();
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(ContactEvent.EventId)));
        }

        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(ContactEvent.DebugName)));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(ContactEvent.ActiveStart)));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(ContactEvent.ActiveEnd)));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(ContactEvent.Definition)));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(ContactEvent.Override)), true);
        DrawContactEffectiveSummary(elem);
        return true;
    }

    void DrawContactList()
    {
        _foldContactList = ActionTimelineEditorUI.Foldout(
            _foldContactList,
            $"Hitbox Events（{(_contactEvents != null ? _contactEvents.arraySize : 0)}）");
        if (!_foldContactList || _contactEvents == null) return;

        using (new EditorGUI.IndentLevelScope())
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Hitbox", EditorStyles.miniButtonLeft)) AddContactAt(_previewTime);
                using (new EditorGUI.DisabledScope(!HasSelectedContact()))
                {
                    if (GUILayout.Button("复制", EditorStyles.miniButtonMid)) DuplicateContact();
                    if (GUILayout.Button("删除", EditorStyles.miniButtonRight)) TryDeleteSelectedContact();
                }
            }

            for (var i = 0; i < _contactEvents.arraySize; i++)
            {
                var elem = _contactEvents.GetArrayElementAtIndex(i);
                ReadContactRange(elem, out var start, out var end);
                var id = ReadContactId(elem);
                var label = elem.FindPropertyRelative(nameof(ContactEvent.DebugName)).stringValue;
                if (GUILayout.Button(
                        $"[{i}] {(string.IsNullOrEmpty(label) ? "Hitbox" : label)} {start:F2}~{end:F2} · {ShortId(id)}",
                        EditorStyles.miniButton))
                {
                    ClearAllButContactSelection();
                    _selectedContactEventId = id;
                    _lastClickedTrack = TrackId.Contact;
                    PublishContactSelection();
                }
            }
        }
    }

    void DuplicateContact()
    {
        var index = ResolveSelectedContactIndex();
        if (index < 0) return;

        Undo.RecordObject(_action, "Duplicate Contact Event");
        _contactEvents.InsertArrayElementAtIndex(index);
        var newIndex = Mathf.Min(index + 1, _contactEvents.arraySize - 1);
        var copy = _contactEvents.GetArrayElementAtIndex(newIndex);
        var newId = ContactEventId.NewId();
        copy.FindPropertyRelative(nameof(ContactEvent.EventId)).stringValue = newId;
        copy.FindPropertyRelative(nameof(ContactEvent.DebugName)).stringValue += "_copy";
        _selectedContactEventId = newId;
        PublishContactSelection();
    }

    bool TryDeleteSelectedContact()
    {
        var index = ResolveSelectedContactIndex();
        if (index < 0) return false;

        Undo.RecordObject(_action, "Delete Contact Event");
        _contactEvents.DeleteArrayElementAtIndex(index);
        ClearContactSelection();
        return true;
    }

    bool HasSelectedContact() => ResolveSelectedContactIndex() >= 0;

    int ResolveSelectedContactIndex()
    {
        if (_contactEvents == null || string.IsNullOrEmpty(_selectedContactEventId)) return -1;
        for (var i = 0; i < _contactEvents.arraySize; i++)
        {
            if (ReadContactId(_contactEvents.GetArrayElementAtIndex(i)) == _selectedContactEventId) return i;
        }

        _selectedContactEventId = null;
        return -1;
    }

    void EnsureContactIds()
    {
        if (_contactEvents == null || _action == null) return;

        var seen = new System.Collections.Generic.HashSet<string>();
        var changed = false;
        for (var i = 0; i < _contactEvents.arraySize; i++)
        {
            var idProp = _contactEvents.GetArrayElementAtIndex(i).FindPropertyRelative(nameof(ContactEvent.EventId));
            if (ContactEventId.IsValid(idProp.stringValue) && seen.Add(idProp.stringValue)) continue;
            idProp.stringValue = ContactEventId.NewId();
            seen.Add(idProp.stringValue);
            changed = true;
        }

        if (changed)
        {
            _so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(_action);
        }
    }

    void PublishContactSelection()
    {
        var index = ResolveSelectedContactIndex();
        if (index < 0)
        {
            ContactAuthoringSelectionContext.Clear();
            return;
        }

        var elem = _contactEvents.GetArrayElementAtIndex(index);
        var definition = elem.FindPropertyRelative(nameof(ContactEvent.Definition)).objectReferenceValue
            as CombatObjectDefinitionSO;
        ContactAuthoringSelectionContext.Publish(
            _action,
            _selectedContactEventId,
            definition,
            _contactEditLayer,
            _previewTime);
    }

    void ClearContactSelection()
    {
        _selectedContactEventId = null;
        _dragContactIndex = -1;
        ContactAuthoringSelectionContext.Clear();
    }

    void ClearAllButContactSelection()
    {
        _selectedWindow = -1;
        _selectedTeleport = -1;
        _selectedMarker = -1;
        _selectedCombatEvent = -1;
        ClearGuardSelection();
    }

    void DrawContactEffectiveSummary(SerializedProperty elem)
    {
        var definition = elem.FindPropertyRelative(nameof(ContactEvent.Definition)).objectReferenceValue
            as CombatObjectDefinitionSO;
        if (definition == null)
        {
            EditorGUILayout.HelpBox("需要 ActionContact Definition。", MessageType.Warning);
            return;
        }

        var validation = CombatObjectDefinitionValidator.Validate(
            definition,
            CombatDefinitionUseSite.ContactEvent);
        if (!validation.IsValid)
        {
            EditorGUILayout.HelpBox(validation.FirstErrorOrNull(), MessageType.Error);
            return;
        }

        EditorGUILayout.HelpBox(
            $"Effective · Preset={definition.ShapePreset.name}\n" +
            $"Geometry={definition.ShapePreset.ShapeMode} / Motion={definition.ShapePreset.DefaultMotion}\n" +
            "Window=[Event] · Shape/Query/Hit/Attack=[Definition/Preset]",
            MessageType.None);
    }

    static void ReadContactRange(SerializedProperty elem, out float start, out float end)
    {
        start = Mathf.Clamp01(elem.FindPropertyRelative(nameof(ContactEvent.ActiveStart)).floatValue);
        end = Mathf.Clamp01(elem.FindPropertyRelative(nameof(ContactEvent.ActiveEnd)).floatValue);
        if (end < start) (start, end) = (end, start);
    }

    static string ReadContactId(SerializedProperty elem) =>
        elem.FindPropertyRelative(nameof(ContactEvent.EventId)).stringValue;

    static string ShortId(string id) =>
        string.IsNullOrEmpty(id) ? "none" : id.Substring(0, Mathf.Min(8, id.Length));

    static void ResetContactOverride(SerializedProperty overrideProp)
    {
        if (overrideProp == null) return;
        overrideProp.FindPropertyRelative(nameof(ContactOverrideData.OverridePlacement)).boolValue = false;
        overrideProp.FindPropertyRelative(nameof(ContactOverrideData.Origin)).enumValueIndex = (int)SpawnSource.SelfRootBone;
        overrideProp.FindPropertyRelative(nameof(ContactOverrideData.LocalOffset)).vector3Value = Vector3.zero;
        overrideProp.FindPropertyRelative(nameof(ContactOverrideData.LocalEuler)).vector3Value = Vector3.zero;
        overrideProp.FindPropertyRelative(nameof(ContactOverrideData.OverrideMotion)).boolValue = false;
        overrideProp.FindPropertyRelative(nameof(ContactOverrideData.Motion)).enumValueIndex =
            (int)ContactMotionKind.SweepBetweenFrames;
    }
}
#endif

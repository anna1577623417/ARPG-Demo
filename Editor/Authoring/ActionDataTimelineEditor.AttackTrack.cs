#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 216.3 M1 L3 / M2 L2 — Attack(HitClip) 判定轨：
/// Active 拖拽/创建；多段 playhead 高亮当前、其余变灰；列表管理（选中/排序/复制/删除）。
/// </summary>
public sealed partial class ActionDataTimelineEditor
{
    const float DefaultAttackClipLength = 0.15f;

    /// <summary>playhead 落入 Active 的当前段。</summary>
    static readonly Color AttackClipActiveColor = new Color(0.98f, 0.35f, 0.28f, 0.95f);

    /// <summary>选中（Inspector 焦点）。</summary>
    static readonly Color AttackClipSelectedColor = new Color(1f, 0.72f, 0.35f, 0.98f);

    /// <summary>非当前段（playhead 外）——变灰，突出多段中的「当前」。</summary>
    static readonly Color AttackClipDimColor = new Color(0.45f, 0.42f, 0.40f, 0.55f);

    SerializedProperty _attackClips;
    int _selectedAttackClip = -1;
    int _dragAttackClipIndex = -1;
    bool _foldAttackClipList = true;

    GUIStyle _attackClipLabelStyle;
    GUIStyle _attackClipDimLabelStyle;

    partial void RefreshAttackTrackProperties()
    {
        _attackClips = _so?.FindProperty(nameof(ActionDataSO.AttackClips));
    }

    void DrawAttackTrack(Rect barRect)
    {
        if (_attackClips == null)
        {
            return;
        }

        EnsureAttackLabelStyles();

        // 先画非当前（灰），再画当前/选中，避免重叠时灰条盖住高亮。
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < _attackClips.arraySize; i++)
            {
                var elem = _attackClips.GetArrayElementAtIndex(i);
                ReadAttackActiveRange(elem, out var start, out var end);
                var playheadInside = IsPlayheadInsideAttack(start, end);
                var selected = _selectedAttackClip == i;
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
                EditorGUI.DrawRect(seg, ResolveAttackClipColor(selected, playheadInside));

                if (selected)
                {
                    EditorGUI.DrawRect(new Rect(seg.x, seg.y, 2f, seg.height), Color.white);
                    EditorGUI.DrawRect(new Rect(seg.xMax - 2f, seg.y, 2f, seg.height), Color.white);
                }

                if (playheadInside && !selected)
                {
                    // 当前段顶边细亮线，区分「时间轴当前」与「Inspector 选中」。
                    EditorGUI.DrawRect(new Rect(seg.x, seg.y, seg.width, 2f), new Color(1f, 0.9f, 0.5f, 0.95f));
                }

                if (seg.width >= 28f)
                {
                    var name = elem.FindPropertyRelative(nameof(HitClip.DebugName)).stringValue;
                    var label = string.IsNullOrEmpty(name) ? $"Atk{i}" : name;
                    if (playheadInside)
                    {
                        label = $"▶ {label}";
                    }

                    GUI.Label(seg, label, playheadInside || selected ? _attackClipLabelStyle : _attackClipDimLabelStyle);
                }
            }
        }
    }

    static Color ResolveAttackClipColor(bool selected, bool playheadInside)
    {
        if (selected)
        {
            return AttackClipSelectedColor;
        }

        return playheadInside ? AttackClipActiveColor : AttackClipDimColor;
    }

    bool IsPlayheadInsideAttack(float start, float end) =>
        _previewTime >= start && _previewTime <= end;

    static void ReadAttackActiveRange(SerializedProperty elem, out float start, out float end)
    {
        start = Mathf.Clamp01(elem.FindPropertyRelative(nameof(HitClip.ActiveStart)).floatValue);
        end = Mathf.Clamp01(elem.FindPropertyRelative(nameof(HitClip.ActiveEnd)).floatValue);
        if (end < start)
        {
            (start, end) = (end, start);
        }
    }

    void EnsureAttackLabelStyles()
    {
        if (_attackClipLabelStyle == null)
        {
            _attackClipLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 1f, 0.95f) },
                fontStyle = FontStyle.Bold,
            };
        }

        if (_attackClipDimLabelStyle == null)
        {
            _attackClipDimLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f, 0.55f) },
            };
        }
    }

    partial void HandleAttackTrackInput(TrackId track, float norm, Event e, Rect barRect)
    {
        if (track != TrackId.Attack || _attackClips == null)
        {
            return;
        }

        if (TryBeginAttackClipDrag(e.mousePosition, norm, barRect))
        {
            e.Use();
            Repaint();
            return;
        }

        if (e.clickCount >= 2)
        {
            AddAttackClipAt(norm);
            e.Use();
            Repaint();
            return;
        }

        _selectedAttackClip = -1;
        e.Use();
        Repaint();
    }

    bool TryBeginAttackClipDrag(Vector2 mp, float norm, Rect barRect)
    {
        if (_attackClips == null || !IsTrackEditable(TrackId.Attack))
        {
            return false;
        }

        // 从后往前：重叠时优先点到后添加的段。
        for (var i = _attackClips.arraySize - 1; i >= 0; i--)
        {
            var elem = _attackClips.GetArrayElementAtIndex(i);
            ReadAttackActiveRange(elem, out var start, out var end);

            if (norm < start - 0.01f / _zoom || norm > end + 0.01f / _zoom)
            {
                continue;
            }

            var x0 = TimeToX(barRect, start);
            var x1 = TimeToX(barRect, end);
            var seg = new Rect(x0, barRect.y, Mathf.Max(2f, x1 - x0), barRect.height);

            ClearNonAttackSelection();
            _selectedAttackClip = i;
            _dragAttackClipIndex = i;
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

            Undo.RecordObject(_action, "Edit HitClip Active");
            return true;
        }

        return false;
    }

    void ApplyAttackClipDrag(Event e)
    {
        if (_attackClips == null
            || _dragAttackClipIndex < 0
            || _dragAttackClipIndex >= _attackClips.arraySize)
        {
            return;
        }

        var elem = _attackClips.GetArrayElementAtIndex(_dragAttackClipIndex);
        var pStart = elem.FindPropertyRelative(nameof(HitClip.ActiveStart));
        var pEnd = elem.FindPropertyRelative(nameof(HitClip.ActiveEnd));
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

    void AddAttackClipAt(float normStart)
    {
        if (_attackClips == null)
        {
            return;
        }

        Undo.RecordObject(_action, "Add HitClip");
        _attackClips.arraySize++;
        var elem = _attackClips.GetArrayElementAtIndex(_attackClips.arraySize - 1);
        WriteDefaultAttackClip(elem, Snap(normStart), _attackClips.arraySize - 1);

        ClearNonAttackSelection();
        _selectedAttackClip = _attackClips.arraySize - 1;
    }

    void WriteDefaultAttackClip(SerializedProperty elem, float start, int index)
    {
        var end = Snap(Mathf.Min(1f, start + DefaultAttackClipLength));
        elem.FindPropertyRelative(nameof(HitClip.DebugName)).stringValue = $"Atk{index}";
        elem.FindPropertyRelative(nameof(HitClip.ActiveStart)).floatValue = start;
        elem.FindPropertyRelative(nameof(HitClip.ActiveEnd)).floatValue = end;
        elem.FindPropertyRelative(nameof(HitClip.ShapeMode)).enumValueIndex = (int)HitShapeMode.Volume;
        elem.FindPropertyRelative(nameof(HitClip.Shape)).objectReferenceValue = null;
        elem.FindPropertyRelative(nameof(HitClip.WeaponSockets)).objectReferenceValue = null;
        elem.FindPropertyRelative(nameof(HitClip.Origin)).enumValueIndex = (int)SpawnSource.SelfRootBone;
        elem.FindPropertyRelative(nameof(HitClip.OriginOffset)).vector3Value = Vector3.zero;
        elem.FindPropertyRelative(nameof(HitClip.OriginEuler)).vector3Value = Vector3.zero;
        elem.FindPropertyRelative(nameof(HitClip.Reach)).floatValue = 0f;

        var target = elem.FindPropertyRelative(nameof(HitClip.Target));
        if (target != null)
        {
            WriteTargetProfile(target, TargetProfile.HostileCombatantsOnly);
        }

        var policy = elem.FindPropertyRelative(nameof(HitClip.Policy));
        if (policy != null)
        {
            policy.FindPropertyRelative(nameof(HitPolicyParams.Kind)).enumValueIndex =
                (int)HitPolicyKind.PerTarget;
            policy.FindPropertyRelative(nameof(HitPolicyParams.IntervalSeconds)).floatValue = 0.2f;
            policy.FindPropertyRelative(nameof(HitPolicyParams.MaxHitsPerTarget)).intValue = 1;
            policy.FindPropertyRelative(nameof(HitPolicyParams.MaxTargets)).intValue = 999;
        }

        var reaction = elem.FindPropertyRelative(nameof(HitClip.Reaction));
        if (reaction != null)
        {
            reaction.FindPropertyRelative(nameof(HitReaction.BaseDamage)).floatValue = 10f;
            reaction.FindPropertyRelative(nameof(HitReaction.ImpulseLocalDir)).vector3Value = Vector3.forward;
            reaction.FindPropertyRelative(nameof(HitReaction.ImpulseForce)).floatValue = 0f;
            reaction.FindPropertyRelative(nameof(HitReaction.LaunchUpSpeed)).floatValue = 0f;
            reaction.FindPropertyRelative(nameof(HitReaction.HitStopSeconds)).floatValue = 0.06f;
            reaction.FindPropertyRelative(nameof(HitReaction.CameraShakeIntensity)).floatValue = 0f;
            reaction.FindPropertyRelative(nameof(HitReaction.CameraShakeDuration)).floatValue = 0.12f;
        }
    }

    /// <summary>216.3 M2 L2 — Attack 轨多段列表 + 选中属性。</summary>
    bool TryDrawAttackTrackInspector()
    {
        var show = _lastClickedTrack == TrackId.Attack || HasSelectedAttackClip();
        if (!show || _attackClips == null)
        {
            return false;
        }

        DrawAttackClipListManager();

        if (!HasSelectedAttackClip())
        {
            EditorGUILayout.HelpBox(
                "双击 Attack 轨或点「+ 段」添加 HitClip；拖 playhead 看当前段高亮（▶），其余变灰。",
                MessageType.Info);
            return true;
        }

        var elem = _attackClips.GetArrayElementAtIndex(_selectedAttackClip);
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField($"HitClip #{_selectedAttackClip}", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "ShapeMode=Volume 用 Shape + Reach；WeaponTrace 用 WeaponSockets（改 SocketSet 的 Bone/Offset/Radius，Reach 无效）。拖 Active 边界改开判区间。",
            MessageType.None);

        var shapeMode = (HitShapeMode)elem.FindPropertyRelative(nameof(HitClip.ShapeMode)).enumValueIndex;
        if (shapeMode == HitShapeMode.WeaponTrace)
        {
            EditorGUILayout.HelpBox(
                "WeaponTrace 预览：Timeline 工具栏 Show → 勾选 Attack HitClip / Attack Coverage / Attack Ghost；\n" +
                "Scene 需有 Preview 角色（Humanoid Animator）。调刃长：打开 WeaponSockets 资产，改 tip/mid/base 的 LocalOffset Z 与 Radius。",
                MessageType.Info);
        }

        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(HitClip.DebugName)));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(HitClip.ActiveStart)));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(HitClip.ActiveEnd)));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(HitClip.ShapeMode)));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(HitClip.Shape)));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(HitClip.WeaponSockets)));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(HitClip.Origin)));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(HitClip.OriginOffset)));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(HitClip.OriginEuler)));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(HitClip.Reach)));
        DrawTargetProfileInspector(elem.FindPropertyRelative(nameof(HitClip.Target)));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(HitClip.QueryLayerMask)));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(HitClip.Policy)), true);
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(HitClip.Reaction)), true);
        return true;
    }

    void DrawAttackClipListManager()
    {
        _foldAttackClipList = ActionTimelineEditorUI.Foldout(
            _foldAttackClipList,
            $"HitClips（{_attackClips.arraySize}）");
        if (!_foldAttackClipList)
        {
            return;
        }

        using (new EditorGUI.IndentLevelScope())
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ 段", EditorStyles.miniButtonLeft))
                {
                    AddAttackClipAt(_previewTime);
                }

                using (new EditorGUI.DisabledScope(!HasSelectedAttackClip()))
                {
                    if (GUILayout.Button("复制", EditorStyles.miniButtonMid))
                    {
                        DuplicateSelectedAttackClip();
                    }

                    if (GUILayout.Button("↑", EditorStyles.miniButtonMid))
                    {
                        MoveSelectedAttackClip(-1);
                    }

                    if (GUILayout.Button("↓", EditorStyles.miniButtonMid))
                    {
                        MoveSelectedAttackClip(+1);
                    }

                    if (GUILayout.Button("删", EditorStyles.miniButtonRight))
                    {
                        TryDeleteSelectedAttackClip();
                    }
                }
            }

            for (var i = 0; i < _attackClips.arraySize; i++)
            {
                var elem = _attackClips.GetArrayElementAtIndex(i);
                ReadAttackActiveRange(elem, out var start, out var end);
                var name = elem.FindPropertyRelative(nameof(HitClip.DebugName)).stringValue;
                if (string.IsNullOrEmpty(name))
                {
                    name = $"Atk{i}";
                }

                var playheadInside = IsPlayheadInsideAttack(start, end);
                var selected = _selectedAttackClip == i;
                var mark = playheadInside ? "▶" : " ";
                var label = $"{mark} [{i}] {name}  {start:F2}~{end:F2}";

                var prev = GUI.backgroundColor;
                if (selected)
                {
                    GUI.backgroundColor = new Color(1f, 0.85f, 0.45f, 1f);
                }
                else if (playheadInside)
                {
                    GUI.backgroundColor = new Color(1f, 0.55f, 0.4f, 1f);
                }

                if (GUILayout.Button(label, EditorStyles.miniButton))
                {
                    ClearNonAttackSelection();
                    _selectedAttackClip = i;
                    _lastClickedTrack = TrackId.Attack;
                }

                GUI.backgroundColor = prev;
            }
        }
    }

    void DuplicateSelectedAttackClip()
    {
        if (!HasSelectedAttackClip())
        {
            return;
        }

        Undo.RecordObject(_action, "Duplicate HitClip");
        var srcIndex = _selectedAttackClip;
        _attackClips.InsertArrayElementAtIndex(srcIndex);
        // Insert 复制了 src；新元素在 srcIndex+1（Unity 行为：插入后原位置保留副本）。
        var newIndex = srcIndex + 1;
        if (newIndex >= _attackClips.arraySize)
        {
            newIndex = _attackClips.arraySize - 1;
        }

        var copy = _attackClips.GetArrayElementAtIndex(newIndex);
        var nameProp = copy.FindPropertyRelative(nameof(HitClip.DebugName));
        var baseName = nameProp.stringValue;
        if (string.IsNullOrEmpty(baseName))
        {
            baseName = $"Atk{srcIndex}";
        }

        nameProp.stringValue = baseName + "_copy";
        _selectedAttackClip = newIndex;
    }

    void MoveSelectedAttackClip(int delta)
    {
        if (!HasSelectedAttackClip())
        {
            return;
        }

        var from = _selectedAttackClip;
        var to = from + delta;
        if (to < 0 || to >= _attackClips.arraySize)
        {
            return;
        }

        Undo.RecordObject(_action, "Reorder HitClip");
        _attackClips.MoveArrayElement(from, to);
        _selectedAttackClip = to;
    }

    bool HasSelectedAttackClip() =>
        _selectedAttackClip >= 0 && _attackClips != null && _selectedAttackClip < _attackClips.arraySize;

    bool TryDeleteSelectedAttackClip()
    {
        if (!HasSelectedAttackClip())
        {
            return false;
        }

        Undo.RecordObject(_action, "Delete HitClip");
        _attackClips.DeleteArrayElementAtIndex(_selectedAttackClip);
        _selectedAttackClip = _attackClips.arraySize > 0
            ? Mathf.Clamp(_selectedAttackClip, 0, _attackClips.arraySize - 1)
            : -1;
        return true;
    }

    void ClearNonAttackSelection()
    {
        _selectedWindow = -1;
        _selectedTeleport = -1;
        _selectedMarker = -1;
        _selectedCombatEvent = -1;
        ClearGuardSelection();
    }

    void ClearAttackSelection()
    {
        _selectedAttackClip = -1;
        _dragAttackClipIndex = -1;
    }

    void DrawTargetProfileInspector(SerializedProperty targetProp)
    {
        if (targetProp == null)
        {
            return;
        }

        EditorGUILayout.LabelField("Target Profile", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(targetProp.FindPropertyRelative(nameof(TargetProfile.Relations)));
        EditorGUILayout.PropertyField(targetProp.FindPropertyRelative(nameof(TargetProfile.UnitKinds)));
        EditorGUILayout.PropertyField(targetProp.FindPropertyRelative(nameof(TargetProfile.SelfHit)));
        EditorGUILayout.PropertyField(targetProp.FindPropertyRelative(nameof(TargetProfile.IncludeDead)));
        EditorGUILayout.PropertyField(targetProp.FindPropertyRelative(nameof(TargetProfile.RequireSelectable)));

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("预设：敌战斗单位", EditorStyles.miniButtonLeft))
            {
                WriteTargetProfile(targetProp, TargetProfile.DamageEnemyCombatants);
            }

            if (GUILayout.Button("预设：仅敌对", EditorStyles.miniButtonMid))
            {
                WriteTargetProfile(targetProp, TargetProfile.HostileCombatantsOnly);
            }

            if (GUILayout.Button("预设：治疗友方", EditorStyles.miniButtonRight))
            {
                WriteTargetProfile(targetProp, TargetProfile.HealAllies);
            }
        }

        if (GUILayout.Button("预设：仅敌方小兵（例 C）", EditorStyles.miniButtonLeft))
        {
            WriteTargetProfile(targetProp, TargetProfile.ClearMinionsOnly);
        }

        if (GUILayout.Button("预设：Owned 召唤（例 D）", EditorStyles.miniButtonRight))
        {
            WriteTargetProfile(targetProp, TargetProfile.DamageOwnedSummons);
        }
    }

    static void WriteTargetProfile(SerializedProperty targetProp, in TargetProfile profile)
    {
        if (targetProp == null)
        {
            return;
        }

        targetProp.FindPropertyRelative(nameof(TargetProfile.Relations)).enumValueFlag = (int)profile.Relations;
        targetProp.FindPropertyRelative(nameof(TargetProfile.UnitKinds)).enumValueFlag = (int)profile.UnitKinds;
        targetProp.FindPropertyRelative(nameof(TargetProfile.SelfHit)).enumValueIndex = (int)profile.SelfHit;
        targetProp.FindPropertyRelative(nameof(TargetProfile.IncludeDead)).boolValue = profile.IncludeDead;
        targetProp.FindPropertyRelative(nameof(TargetProfile.RequireSelectable)).boolValue = profile.RequireSelectable;
    }
}
#endif

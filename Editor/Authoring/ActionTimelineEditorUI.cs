#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Action 时间轴编辑器共享 UI（140.1 双栏 + 142.1 自适应布局）。
/// </summary>
internal static class ActionTimelineEditorUI
{
    public const float TimelineColumnMinWidth = 600f;
    public const float PropertiesColumnMinWidth = 320f;
    public const float SplitterWidth = 5f;
    public const float LabelWidthRatio = 0.4f;
    public const float StatusBarHeight = 22f;
    /// <summary>列内容左右留白，避免贴边裁切感。</summary>
    public const float ColumnContentPadding = 8f;
    /// <summary>轨道名与刻度条之间的空隙。</summary>
    public const float TimelineLabelLaneGap = 6f;

    const string PrefPropertyWidth = "ActionTimelineEditor.PropertyWidth";
    const string PrefZoom = "ActionTimelineEditor.Zoom";
    const string PrefTimelineScrollX = "ActionTimelineEditor.TimelineScrollX";
    const string PrefTimelineScrollY = "ActionTimelineEditor.TimelineScrollY";
    const string PrefPropertiesScrollY = "ActionTimelineEditor.PropertiesScrollY";
    const string PrefTrackVisibilityMask = "ActionTimelineEditor.TrackVisibilityMask";
    const string PrefGizmoAnchorGlobalId = "ActionTimelineEditor.GizmoAnchorGlobalId";

    public static void LightSectionSeparator(string title)
    {
        GUILayout.Space(2f);
        EditorGUILayout.LabelField($"─── {title} ───", EditorStyles.miniBoldLabel);
        GUILayout.Space(2f);
    }

    public static int GetResponsiveColumnCount(float width)
    {
        if (width >= 600f)
        {
            return 4;
        }

        if (width >= 400f)
        {
            return 2;
        }

        return 1;
    }

    public static void CompactRowFloat(SerializedProperty[] props, GUIContent[] labels, int columns)
    {
        if (props == null || labels == null || props.Length == 0)
        {
            return;
        }

        columns = Mathf.Max(1, columns);
        var index = 0;
        while (index < props.Length)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var rowCount = Mathf.Min(columns, props.Length - index);
                for (var i = 0; i < rowCount; i++)
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(72f)))
                    {
                        EditorGUILayout.LabelField(labels[index + i], EditorStyles.miniLabel);
                        EditorGUILayout.PropertyField(props[index + i], GUIContent.none);
                    }
                }
            }

            index += columns;
        }
    }

    public static PreviewVisibilityMask LoadPreviewVisibilityMask() =>
        ActionTimelinePreviewVisibility.Load();

    public static void SavePreviewVisibilityMask(PreviewVisibilityMask mask)
    {
        ActionTimelinePreviewVisibility.Save(mask);
    }

    public static void SaveGizmoAnchor(Transform anchor)
    {
        if (anchor == null)
        {
            EditorPrefs.DeleteKey(PrefGizmoAnchorGlobalId);
            return;
        }

        var gid = GlobalObjectId.GetGlobalObjectIdSlow(anchor);
        EditorPrefs.SetString(PrefGizmoAnchorGlobalId, gid.ToString());
    }

    public static Transform LoadGizmoAnchor(Transform serializedFallback = null)
    {
        var idStr = EditorPrefs.GetString(PrefGizmoAnchorGlobalId, string.Empty);
        if (!string.IsNullOrEmpty(idStr) && GlobalObjectId.TryParse(idStr, out var gid))
        {
            var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
            if (obj is Transform t)
            {
                return t;
            }

            if (obj is GameObject go)
            {
                return go.transform;
            }
        }

        return serializedFallback;
    }

    /// <summary>TL / Combat 预览锚点：显式 override → Hierarchy 选中 → 启发式 Player。</summary>
    public static Transform ResolvePreviewAnchor(Transform anchorOverride)
    {
        if (anchorOverride != null)
        {
            return anchorOverride;
        }

        var fromSelection = PickPreviewAnchorFromSelection();
        if (fromSelection != null)
        {
            return fromSelection;
        }

        return FindPreferredPlayerTransform();
    }

    public static Transform PickPreviewAnchorFromSelection()
    {
        if (Selection.activeTransform == null)
        {
            return null;
        }

        var player = Selection.activeTransform.GetComponentInParent<Player>();
        return player != null ? player.transform : Selection.activeTransform;
    }

    public static Transform FindPreferredPlayerTransform()
    {
        var players = UnityEngine.Object.FindObjectsByType<Player>(FindObjectsSortMode.None);
        if (players == null || players.Length == 0)
        {
            return null;
        }

        if (players.Length == 1)
        {
            return players[0].transform;
        }

        for (var i = 0; i < players.Length; i++)
        {
            var pc = players[i].GetComponent<PlayerController>();
            if (pc != null && pc.enabled)
            {
                return players[i].transform;
            }
        }

        for (var i = 0; i < players.Length; i++)
        {
            var name = players[i].name;
            if (name.IndexOf("Training", System.StringComparison.OrdinalIgnoreCase) < 0
                && name.IndexOf("Dummy", System.StringComparison.OrdinalIgnoreCase) < 0
                && name.IndexOf("Target", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                return players[i].transform;
            }
        }

        return players[0].transform;
    }

    /// <summary>属性栏绘制中为 true；PropertyDrawer 可省略 HelpBox、缩短行高。</summary>
    public static bool CompactPropertyContext { get; set; }

    public static float LoadPropertyWidth(float fallback = 340f)
    {
        return EditorPrefs.GetFloat(PrefPropertyWidth, fallback);
    }

    public static void SavePropertyWidth(float width)
    {
        EditorPrefs.SetFloat(PrefPropertyWidth, width);
    }

    public static float LoadZoom(float fallback = 1f)
    {
        return EditorPrefs.GetFloat(PrefZoom, fallback);
    }

    public static void SaveZoom(float zoom)
    {
        EditorPrefs.SetFloat(PrefZoom, zoom);
    }

    public static Vector2 LoadTimelineScroll()
    {
        return new Vector2(
            EditorPrefs.GetFloat(PrefTimelineScrollX, 0f),
            EditorPrefs.GetFloat(PrefTimelineScrollY, 0f));
    }

    public static void SaveTimelineScroll(Vector2 scroll)
    {
        EditorPrefs.SetFloat(PrefTimelineScrollX, scroll.x);
        EditorPrefs.SetFloat(PrefTimelineScrollY, scroll.y);
    }

    public static float LoadPropertiesScrollY(float fallback = 0f)
    {
        return EditorPrefs.GetFloat(PrefPropertiesScrollY, fallback);
    }

    public static void SavePropertiesScrollY(float y)
    {
        EditorPrefs.SetFloat(PrefPropertiesScrollY, y);
    }

    public static ActionTimelinePreviewTrackVisibility LoadTrackVisibility()
    {
        var mask = EditorPrefs.GetInt(PrefTrackVisibilityMask, ActionTimelinePreviewTrackVisibility.DefaultAllOn.ToPrefsMask());
        return ActionTimelinePreviewTrackVisibility.FromPrefsMask(mask);
    }

    public static void SaveTrackVisibility(in ActionTimelinePreviewTrackVisibility visibility)
    {
        EditorPrefs.SetInt(PrefTrackVisibilityMask, visibility.ToPrefsMask());
    }

    public static float ClampPropertyWidth(float desired, float totalWidth, float horizontalPadding = 8f)
    {
        var max = Mathf.Max(
            PropertiesColumnMinWidth,
            totalWidth - TimelineColumnMinWidth - SplitterWidth - horizontalPadding);
        return Mathf.Clamp(desired, PropertiesColumnMinWidth, max);
    }

    public static void SectionHeader(string title, string tooltip = null)
    {
        var content = string.IsNullOrEmpty(tooltip)
            ? new GUIContent(title)
            : new GUIContent(title, tooltip);
        EditorGUILayout.LabelField(content, EditorStyles.boldLabel);
    }

    public static void MiniHint(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        EditorGUILayout.LabelField(text, EditorStyles.miniLabel);
    }

    public static bool Foldout(bool state, string title)
    {
        return EditorGUILayout.Foldout(state, title, true, EditorStyles.foldoutHeader);
    }

    // ═══ 172.2 W0/W1：垂直字段布局 ═══
    // label 顶置 + 控件全宽 + DelayedFloat 输入；解决中文 label 截断问题。

    /// <summary>字段间距（块与块之间）。</summary>
    public const float VerticalFieldSpacing = 4f;
    /// <summary>label 与控件之间的间距。</summary>
    public const float LabelToControlSpacing = 1f;

    /// <summary>垂直布局：label 顶置 + 控件全宽。适合所有需要自适应宽度的字段。</summary>
    public static void VerticalField(SerializedProperty prop, GUIContent label)
    {
        if (prop == null) return;
        EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
        if (LabelToControlSpacing > 0f) GUILayout.Space(LabelToControlSpacing);
        EditorGUILayout.PropertyField(prop, GUIContent.none, true);
        GUILayout.Space(VerticalFieldSpacing);
    }

    /// <summary>垂直布局 + DelayedFloatField（Enter 提交，拖时不触发回调）。</summary>
    public static float VerticalDelayedFloatField(GUIContent label, float current)
    {
        EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
        if (LabelToControlSpacing > 0f) GUILayout.Space(LabelToControlSpacing);
        var next = EditorGUILayout.DelayedFloatField(GUIContent.none, current);
        GUILayout.Space(VerticalFieldSpacing);
        return next;
    }

    /// <summary>虚线分组分隔，替代 Foldout 的视觉重量；默认展开。</summary>
    public static void SectionSeparator(string title)
    {
        GUILayout.Space(2f);
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawHorizontalLine(1f);
            GUILayout.Space(4f);
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel, GUILayout.Width(GUI.skin.label.CalcSize(new GUIContent(title)).x + 8f));
            GUILayout.Space(4f);
            DrawHorizontalLine(1f);
        }
        GUILayout.Space(2f);
    }

    static void DrawHorizontalLine(float thickness)
    {
        var rect = EditorGUILayout.GetControlRect(false, thickness, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.35f, 0.35f, 0.35f, 1f));
    }

    /// <summary>属性栏 40% Label / 60% Control，禁止横向溢出。</summary>
    public readonly struct PropertyLayoutScope : IDisposable
    {
        readonly float _prevLabelWidth;

        public PropertyLayoutScope(float columnWidth)
        {
            _prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(columnWidth * LabelWidthRatio, 72f, 168f);
        }

        public void Dispose()
        {
            EditorGUIUtility.labelWidth = _prevLabelWidth;
        }
    }

    /// <summary>仅纵向滚动的 ScrollView（142.1：禁止横向滚动条）。</summary>
    public static Vector2 BeginVerticalScrollView(Vector2 scrollPosition, params GUILayoutOption[] options)
    {
        return EditorGUILayout.BeginScrollView(scrollPosition, false, true, options);
    }

    public static void EndScrollView()
    {
        EditorGUILayout.EndScrollView();
    }

    /// <summary>Timeline ←→ Property 可拖拽分隔线。</summary>
    public static void DrawVerticalSplitter(
        EditorWindow window,
        ref float propertiesWidth,
        float totalWidth,
        ref bool dragging,
        ref float dragStartMouseX,
        ref float dragStartPropertyWidth)
    {
        var rect = GUILayoutUtility.GetRect(SplitterWidth, 4f, GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(rect, new Color(0.22f, 0.22f, 0.22f, 1f));
        var line = new Rect(rect.x + 1f, rect.y, 1f, rect.height);
        EditorGUI.DrawRect(line, new Color(0.08f, 0.08f, 0.08f, 1f));

        EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);

        var e = Event.current;
        switch (e.type)
        {
            case EventType.MouseDown:
                if (e.button == 0 && rect.Contains(e.mousePosition))
                {
                    dragging = true;
                    dragStartMouseX = e.mousePosition.x;
                    dragStartPropertyWidth = propertiesWidth;
                    e.Use();
                }

                break;

            case EventType.MouseDrag:
                if (dragging)
                {
                    var delta = e.mousePosition.x - dragStartMouseX;
                    propertiesWidth = ClampPropertyWidth(
                        dragStartPropertyWidth - delta,
                        totalWidth);
                    window.Repaint();
                    e.Use();
                }

                break;

            case EventType.MouseUp:
                if (dragging)
                {
                    dragging = false;
                    SavePropertyWidth(propertiesWidth);
                    e.Use();
                }

                break;
        }
    }
}
#endif

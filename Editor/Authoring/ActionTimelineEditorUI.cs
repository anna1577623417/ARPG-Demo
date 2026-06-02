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

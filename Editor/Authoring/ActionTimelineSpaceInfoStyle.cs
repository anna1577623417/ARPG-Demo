#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>171.5 W6 — SpaceInfo 字段样式（EditorPrefs）。</summary>
internal enum ActionTimelineSpaceInfoField : byte
{
    Action = 0,
    Clip = 1,
    Seg = 2,
    AnimSpeed = 3,
    XyzLocal = 4,
    XyzWorld = 5,
    Origin = 6,
    Heading = 7,
}

internal static class ActionTimelineSpaceInfoStyle
{
    const string Prefix = "ATL.SpaceInfo.";

    public static bool IsVisible(ActionTimelineSpaceInfoField field) =>
        EditorPrefs.GetBool(Prefix + field + ".Visible", DefaultVisible(field));

    public static Color GetColor(ActionTimelineSpaceInfoField field) =>
        LoadColor(Prefix + field + ".Color", DefaultColor(field));

    public static int GetFontSize(ActionTimelineSpaceInfoField field) =>
        EditorPrefs.GetInt(Prefix + field + ".Size", DefaultFontSize(field));

    public static void SetVisible(ActionTimelineSpaceInfoField field, bool visible) =>
        EditorPrefs.SetBool(Prefix + field + ".Visible", visible);

    public static void SetColor(ActionTimelineSpaceInfoField field, Color color) =>
        EditorPrefs.SetString(Prefix + field + ".Color", ColorUtility.ToHtmlStringRGBA(color));

    public static void SetFontSize(ActionTimelineSpaceInfoField field, int size) =>
        EditorPrefs.SetInt(Prefix + field + ".Size", Mathf.Clamp(size, 8, 24));

    public static void ResetField(ActionTimelineSpaceInfoField field)
    {
        EditorPrefs.DeleteKey(Prefix + field + ".Visible");
        EditorPrefs.DeleteKey(Prefix + field + ".Color");
        EditorPrefs.DeleteKey(Prefix + field + ".Size");
    }

    public static void ResetAll()
    {
        foreach (ActionTimelineSpaceInfoField field in Enum.GetValues(typeof(ActionTimelineSpaceInfoField)))
        {
            ResetField(field);
        }
    }

    public static string GetLine(in ActionTimelineSpaceInfoModel model, ActionTimelineSpaceInfoField field) =>
        field switch
        {
            ActionTimelineSpaceInfoField.Action => model.ActionName,
            ActionTimelineSpaceInfoField.Clip => model.ClipLine,
            ActionTimelineSpaceInfoField.Seg => model.SegLine,
            ActionTimelineSpaceInfoField.AnimSpeed => model.AnimSpeedLine,
            ActionTimelineSpaceInfoField.XyzLocal => model.XyzLocalLine,
            ActionTimelineSpaceInfoField.XyzWorld => model.XyzWorldLine,
            ActionTimelineSpaceInfoField.Origin => model.OriginLine,
            ActionTimelineSpaceInfoField.Heading => model.HeadingLine,
            _ => string.Empty,
        };

    public static GUIStyle GetGuiStyle(ActionTimelineSpaceInfoField field)
    {
        var style = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = GetColor(field) },
            fontSize = GetFontSize(field),
        };
        return style;
    }

    public static GUIStyle GetSceneLabelStyle(ActionTimelineSpaceInfoField field)
    {
        var style = new GUIStyle(EditorStyles.whiteLabel)
        {
            normal = { textColor = GetColor(field) },
            fontSize = GetFontSize(field),
        };
        return style;
    }

    static bool DefaultVisible(ActionTimelineSpaceInfoField field) =>
        field != ActionTimelineSpaceInfoField.AnimSpeed
        && field != ActionTimelineSpaceInfoField.XyzWorld
        && field != ActionTimelineSpaceInfoField.Heading;

    static int DefaultFontSize(ActionTimelineSpaceInfoField field) =>
        field == ActionTimelineSpaceInfoField.Action ? 12 : 11;

    static Color DefaultColor(ActionTimelineSpaceInfoField field) =>
        field == ActionTimelineSpaceInfoField.Action
            ? new Color(0.92f, 0.92f, 0.92f)
            : new Color(0.82f, 0.82f, 0.82f);

    static Color LoadColor(string key, Color fallback)
    {
        var hex = EditorPrefs.GetString(key, string.Empty);
        if (string.IsNullOrEmpty(hex))
        {
            return fallback;
        }

        return ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : fallback;
    }
}

internal sealed class ActionTimelineSpaceInfoStyleWindow : EditorWindow
{
    ActionTimelineSpaceInfoField[] _fields;

    public static void Open()
    {
        var win = GetWindow<ActionTimelineSpaceInfoStyleWindow>(true, "SpaceInfo Style", true);
        win.minSize = new Vector2(420f, 320f);
        win.ShowUtility();
    }

    void OnEnable()
    {
        _fields = (ActionTimelineSpaceInfoField[])Enum.GetValues(typeof(ActionTimelineSpaceInfoField));
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("SpaceInfo Style（171.5 W6）", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("字段", EditorStyles.miniBoldLabel, GUILayout.Width(72f));
            EditorGUILayout.LabelField("颜色", EditorStyles.miniBoldLabel, GUILayout.Width(120f));
            EditorGUILayout.LabelField("字号", EditorStyles.miniBoldLabel, GUILayout.Width(40f));
            EditorGUILayout.LabelField("可见", EditorStyles.miniBoldLabel, GUILayout.Width(36f));
        }

        foreach (var field in _fields)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(field.ToString(), GUILayout.Width(72f));
                var color = ActionTimelineSpaceInfoStyle.GetColor(field);
                var nextColor = EditorGUILayout.ColorField(color, GUILayout.Width(120f));
                if (nextColor != color)
                {
                    ActionTimelineSpaceInfoStyle.SetColor(field, nextColor);
                }

                var size = ActionTimelineSpaceInfoStyle.GetFontSize(field);
                var nextSize = EditorGUILayout.IntField(size, GUILayout.Width(40f));
                if (nextSize != size)
                {
                    ActionTimelineSpaceInfoStyle.SetFontSize(field, nextSize);
                }

                var visible = ActionTimelineSpaceInfoStyle.IsVisible(field);
                var nextVisible = EditorGUILayout.Toggle(visible, GUILayout.Width(36f));
                if (nextVisible != visible)
                {
                    ActionTimelineSpaceInfoStyle.SetVisible(field, nextVisible);
                }
            }
        }

        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Reset All"))
            {
                ActionTimelineSpaceInfoStyle.ResetAll();
            }

            if (GUILayout.Button("Apply"))
            {
                SceneView.RepaintAll();
                Repaint();
            }
        }
    }
}
#endif

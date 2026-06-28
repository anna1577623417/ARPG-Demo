#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 171.5 W1 — ActionTimeline Scene Gizmo 颜色集中配置（EditorPrefs）。
/// 入口：Preferences → ActionTimeline → Gizmo Colors。
/// </summary>
internal static class ActionTimelineGizmoColors
{
    const string KComposite   = "ATL.Gizmo.Composite";
    const string KXAxis       = "ATL.Gizmo.X";
    const string KYAxis       = "ATL.Gizmo.Y";
    const string KZAxis       = "ATL.Gizmo.Z";
    const string KFutureMark  = "ATL.Gizmo.Future";
    const string KRootMotion  = "ATL.Gizmo.RootMotion";

    static readonly Color DefComposite  = new(1f, 0.65f, 0.10f, 0.95f);
    static readonly Color DefX          = new(1f, 0.30f, 0.30f, 0.90f);
    static readonly Color DefY          = new(0.45f, 1f, 0.45f, 0.90f);
    static readonly Color DefZ          = new(0.40f, 0.65f, 1f, 0.90f);
    static readonly Color DefFuture     = new(1f, 0.95f, 0.50f, 0.95f);
    static readonly Color DefRootMotion = new(0.55f, 0.55f, 0.55f, 0.85f);

    public static Color Composite  => Load(KComposite, DefComposite);
    public static Color XAxis      => Load(KXAxis, DefX);
    public static Color YAxis      => Load(KYAxis, DefY);
    public static Color ZAxis      => Load(KZAxis, DefZ);
    public static Color FutureMark => Load(KFutureMark, DefFuture);
    public static Color RootMotion => Load(KRootMotion, DefRootMotion);

    static Color Load(string key, Color fallback)
    {
        var hex = EditorPrefs.GetString(key, string.Empty);
        if (string.IsNullOrEmpty(hex)) return fallback;
        return ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : fallback;
    }

    static void Save(string key, Color c)
    {
        EditorPrefs.SetString(key, ColorUtility.ToHtmlStringRGBA(c));
    }

    static void Reset()
    {
        EditorPrefs.DeleteKey(KComposite);
        EditorPrefs.DeleteKey(KXAxis);
        EditorPrefs.DeleteKey(KYAxis);
        EditorPrefs.DeleteKey(KZAxis);
        EditorPrefs.DeleteKey(KFutureMark);
        EditorPrefs.DeleteKey(KRootMotion);
    }

    [SettingsProvider]
    public static SettingsProvider CreateProvider()
    {
        return new SettingsProvider("Preferences/ActionTimeline/Gizmo Colors", SettingsScope.User)
        {
            label = "Gizmo Colors",
            guiHandler = _ =>
            {
                EditorGUILayout.LabelField("171.5 W1 — Scene Gizmo 颜色配置", EditorStyles.boldLabel);
                EditorGUILayout.Space(4f);

                ColorRow("合成轨迹 (Composite)", KComposite, DefComposite);
                ColorRow("X 轴分量 (X-Axis)", KXAxis, DefX);
                ColorRow("Y 轴分量 (Y-Axis)", KYAxis, DefY);
                ColorRow("Z 轴分量 (Z-Axis)", KZAxis, DefZ);
                ColorRow("Future Marker（圈圈）", KFutureMark, DefFuture);
                ColorRow("RootMotion 对比", KRootMotion, DefRootMotion);

                EditorGUILayout.Space(8f);
                if (GUILayout.Button("Reset All to Default", GUILayout.Width(200f)))
                {
                    Reset();
                    SceneView.RepaintAll();
                }
            },
            keywords = new System.Collections.Generic.HashSet<string> { "ActionTimeline", "Gizmo", "Color", "Trajectory" },
        };
    }

    static void ColorRow(string label, string key, Color def)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(200f));
            var cur = Load(key, def);
            var next = EditorGUILayout.ColorField(cur, GUILayout.Width(180f));
            if (next != cur)
            {
                Save(key, next);
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("Default", EditorStyles.miniButton, GUILayout.Width(72f)))
            {
                EditorPrefs.DeleteKey(key);
                SceneView.RepaintAll();
            }
        }
    }
}
#endif

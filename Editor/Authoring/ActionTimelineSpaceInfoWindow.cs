#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>171.5 — SpaceInfo 独立子窗口（与 Action Timeline 解耦，可单独停靠）。</summary>
internal sealed class ActionTimelineSpaceInfoWindow : EditorWindow
{
    const float MinHeight = 160f;

    [MenuItem("Window/Action Timeline/SpaceInfo")]
    static void OpenFromMenu()
    {
        OpenOrFocus();
    }

    public static void OpenOrFocus()
    {
        var win = GetWindow<ActionTimelineSpaceInfoWindow>(false, "Action SpaceInfo", true);
        win.minSize = new Vector2(360f, MinHeight);
        win.Show();
    }

    void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    void OnEditorUpdate()
    {
        if (ActionDataTimelineEditor.ActiveInstance != null)
        {
            Repaint();
        }
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("SpaceInfo（171.5）", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Style ⚙", EditorStyles.miniButton, GUILayout.Width(72f)))
            {
                ActionTimelineSpaceInfoStyleWindow.Open();
            }

            if (GUILayout.Button("刷新", EditorStyles.miniButton, GUILayout.Width(48f)))
            {
                Repaint();
                SceneView.RepaintAll();
            }
        }

        EditorGUILayout.Space(4f);

        var editor = ActionDataTimelineEditor.ActiveInstance;
        if (editor == null || !editor.HasBoundAction)
        {
            EditorGUILayout.HelpBox(
                "请先打开 Action Timeline 并选中 ActionDataSO。\n本窗口可独立停靠，数据随 Timeline 预览时刻同步。",
                MessageType.Info);
            return;
        }

        var ctx = editor.BuildCurrentPreviewContext();
        var height = Mathf.Max(MinHeight - 48f, 120f);
        var rect = GUILayoutUtility.GetRect(position.width - 16f, height);
        EditorGUI.DrawRect(rect, new Color(0.14f, 0.14f, 0.14f, 1f));
        ActionTimelinePreviewFramework.DrawTimelineSpaceInfo(in ctx, rect);
    }
}
#endif

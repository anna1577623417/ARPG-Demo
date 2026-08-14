#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 234.6.3 — 点按尾段复用 172.1 Segment 的 MinMaxSlider。
/// 左柄 = 起播 Clip nt，右柄 = 尾端；跨度写入 TapTailSeconds。
/// 不写 Action.SegmentStart/End（那是 Time Authority / Loop 的 Clip 映射）。
/// </summary>
static class StopTapTailSegmentEditor
{
    const float MinSpan = 0.001f;

    public static void Draw(SerializedObject serializedAction, SerializedProperty inheritProp, ActionDataSO action)
    {
        if (serializedAction == null || inheritProp == null || action == null)
        {
            return;
        }

        var startProp = inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.TapTailStartNormalized));
        var secondsProp = inheritProp.FindPropertyRelative(nameof(InheritPhysicsSettings.TapTailSeconds));
        if (startProp == null || secondsProp == null)
        {
            return;
        }

        EditorGUILayout.LabelField("点按尾段 · 起播→尾端", EditorStyles.miniLabel);

        if (action.MainClip == null)
        {
            EditorGUILayout.LabelField("(无 MainClip)", EditorStyles.miniLabel);
            return;
        }

        var clipLen = Mathf.Max(MinSpan, action.MainClip.length);
        var isAuthor = StopTierResolver.IsAuthorTapTail(action.InheritPhysics);
        ResolveDisplayRange(action, clipLen, isAuthor, out var left, out var right);

        var mode = isAuthor ? "Author" : "Auto";
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.MinMaxSlider(
            new GUIContent(
                "Range",
                "左=起播 Clip nt，右=尾端。Auto（字段 0/0）预览最后 T_tap 秒；拖动后写入 Author。只写 InheritPhysics，不改 Time Authority Segment。"),
            ref left,
            ref right,
            0f,
            1f);
        var sliderChanged = EditorGUI.EndChangeCheck();

        var typedChanged = false;
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginChangeCheck();
            var typedStart = EditorGUILayout.DelayedFloatField(left, GUILayout.Width(52f));
            EditorGUILayout.LabelField("→", EditorStyles.miniLabel, GUILayout.Width(14f));
            var typedEnd = EditorGUILayout.DelayedFloatField(right, GUILayout.Width(52f));
            typedChanged = EditorGUI.EndChangeCheck();
            if (typedChanged)
            {
                if (!Mathf.Approximately(typedStart, left))
                {
                    left = Mathf.Clamp01(typedStart);
                }

                if (!Mathf.Approximately(typedEnd, right))
                {
                    right = Mathf.Clamp01(typedEnd);
                }
            }

            EditorGUILayout.LabelField(
                $"{mode}  Δ={(right - left) * clipLen:F3}s",
                EditorStyles.miniLabel,
                GUILayout.MinWidth(96f));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(
                    new GUIContent("Snap", "吸附起播/尾端到 30fps 帧边界"),
                    EditorStyles.miniButtonLeft,
                    GUILayout.Width(52f)))
            {
                SnapToFrame(ref left, ref right, clipLen, 30f);
                ApplyAuthorRange(serializedAction, action, startProp, secondsProp, left, right, clipLen);
                return;
            }

            if (GUILayout.Button(
                    new GUIContent("Auto", "清零字段，运行时回到最后 T_tap 秒"),
                    EditorStyles.miniButtonRight,
                    GUILayout.Width(44f)))
            {
                Undo.RecordObject(action, "Reset Tap Tail Auto");
                startProp.floatValue = 0f;
                secondsProp.floatValue = 0f;
                serializedAction.ApplyModifiedProperties();
                EditorUtility.SetDirty(action);
                return;
            }
        }

        if (sliderChanged || typedChanged)
        {
            ApplyAuthorRange(serializedAction, action, startProp, secondsProp, left, right, clipLen);
        }
    }

    static void ResolveDisplayRange(
        ActionDataSO action,
        float clipLen,
        bool isAuthor,
        out float left,
        out float right)
    {
        var s = action.InheritPhysics;
        if (isAuthor)
        {
            left = Mathf.Clamp01(s.TapTailStartNormalized);
            var spanNt = s.TapTailSeconds > 0.0001f
                ? s.TapTailSeconds / clipLen
                : StopTierResolver.ResolveTapPresentation(in s) / clipLen;
            right = Mathf.Clamp01(left + Mathf.Max(MinSpan, spanNt));
            return;
        }

        var autoSpan = Mathf.Clamp(
            StopTierResolver.ResolveTapPresentation(in s) / clipLen,
            MinSpan,
            1f);
        right = 1f;
        left = Mathf.Clamp01(1f - autoSpan);
    }

    static void SnapToFrame(ref float left, ref float right, float clipLen, float fps)
    {
        var frameStep = 1f / Mathf.Max(1f, fps);
        left = Mathf.Round(left * clipLen / frameStep) * frameStep / clipLen;
        right = Mathf.Round(right * clipLen / frameStep) * frameStep / clipLen;
        left = Mathf.Clamp01(left);
        right = Mathf.Clamp01(Mathf.Max(right, left + MinSpan));
    }

    static void ApplyAuthorRange(
        SerializedObject serializedAction,
        ActionDataSO action,
        SerializedProperty startProp,
        SerializedProperty secondsProp,
        float left,
        float right,
        float clipLen)
    {
        left = Mathf.Clamp01(left);
        right = Mathf.Clamp01(Mathf.Max(right, left + MinSpan));
        Undo.RecordObject(action, "Edit Tap Tail Segment");
        startProp.floatValue = left;
        secondsProp.floatValue = Mathf.Max(MinSpan, (right - left) * clipLen);
        serializedAction.ApplyModifiedProperties();
        EditorUtility.SetDirty(action);
    }
}
#endif

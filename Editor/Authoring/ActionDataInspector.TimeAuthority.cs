#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed partial class ActionDataInspector
{
    static float s_editorDurationScalePreview = 1f;

    void DrawTimeAuthoritySection(ActionDataSO action)
    {
        EditorGUILayout.Space(6f);
        s_timeFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            s_timeFoldout,
            "Time Authority（145.1 · Duration + End Ratio）");
        if (!s_timeFoldout)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(ActionDataSO.Duration)));

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(action.MainClip == null))
            {
                if (GUILayout.Button("Clip 时长 → Duration", GUILayout.Height(20f)))
                {
                    Undo.RecordObject(action, "Clip Length To Duration");
                    action.Duration = action.MainClip.length;
                    EditorUtility.SetDirty(action);
                    serializedObject.Update();
                }
            }

            if (action.MainClip != null)
            {
                EditorGUILayout.LabelField(
                    $"Clip={action.MainClip.length:F3}s",
                    GUILayout.Width(100f));
            }
        }

        EditorGUILayout.PropertyField(
            serializedObject.FindProperty(nameof(ActionDataSO.AnimSpeed)),
            new GUIContent("Anim Speed", "Clip 速率；「按 Duration 匹配 Clip 速率」写入 (Clip×Ratio)÷Duration"));

        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(ActionDataSO.AnimationEndRatio)));
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(ActionDataSO.DurationStatScaling)));
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(ActionDataSO.PrincipalAxis)));

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Full (1.0)", EditorStyles.miniButtonLeft))
            {
                SetRatio(action, 1f);
            }

            if (GUILayout.Button("Trim 0.8", EditorStyles.miniButtonMid))
            {
                SetRatio(action, 0.8f);
            }

            if (GUILayout.Button("Extend 1.2", EditorStyles.miniButtonRight))
            {
                SetRatio(action, 1.2f);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(action.MainClip == null))
            {
                if (GUILayout.Button("Ratio → Duration", EditorStyles.miniButtonLeft))
                {
                    Undo.RecordObject(action, "Ratio To Duration");
                    action.Duration = ActionTimeAuthority.ComputeSuggestedDurationFromRatio(action);
                    EditorUtility.SetDirty(action);
                    serializedObject.Update();
                }

                if (GUILayout.Button("Duration → Ratio", EditorStyles.miniButtonMid))
                {
                    Undo.RecordObject(action, "Duration To Ratio");
                    action.AnimationEndRatio = ActionTimeAuthority.ComputeSuggestedRatioFromDuration(action);
                    EditorUtility.SetDirty(action);
                    serializedObject.Update();
                }

                if (GUILayout.Button("Convert From AnimSpeed", EditorStyles.miniButtonRight))
                {
                    Undo.RecordObject(action, "Convert From AnimSpeed");
                    action.AnimationEndRatio = ActionTimeAuthority.InferAnimationEndRatioFromAnimSpeed(action);
                    EditorUtility.SetDirty(action);
                    serializedObject.Update();
                }
            }
        }

        if (GUILayout.Button("按 Duration 匹配 Clip 速率", GUILayout.Height(24f)))
        {
            Undo.RecordObject(action, "Match Clip Speed To Duration");
            action.AnimSpeed = ActionTimeAuthority.ComputeAnimSpeed(action);
            EditorUtility.SetDirty(action);
            serializedObject.Update();
            LogMatchClipSpeed(action);
        }

        using (new EditorGUI.DisabledScope(action.MotionProfile == null))
        {
            if (GUILayout.Button("motionProfile获取主轴位移", GUILayout.Height(22f)))
            {
                RefreshPrincipalDisplacementFromMotionProfile(action);
                Repaint();
            }
        }

        serializedObject.ApplyModifiedProperties();

        var authored = ActionTimeAuthority.ResolveAuthoredLogicDurationSeconds(action);
        var previewScale = action.DurationStatScaling == MotionScaleType.None
            ? 1f
            : s_editorDurationScalePreview;
        var effective = ActionTimeAuthority.ResolveLogicDurationSeconds(action, null, previewScale);
        var motionDist = ActionTimeAuthority.MeasurePrincipalAxisDisplacementMeters(action);
        var computedAnimSpeed = ActionTimeAuthority.ComputeAnimSpeed(action);
        var suggestedDuration = ActionTimeAuthority.ComputeSuggestedDurationFromRatio(action);
        var suggestedRatio = ActionTimeAuthority.ComputeSuggestedRatioFromDuration(action);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("预览", EditorStyles.boldLabel);
        s_editorDurationScalePreview = EditorGUILayout.Slider(
            "Editor 属性倍率预览",
            s_editorDurationScalePreview,
            0.25f,
            3f);

        EditorGUILayout.LabelField("Authored Logic Duration", $"{authored:F4}s");
        EditorGUILayout.LabelField("Effective Logic Duration", $"{effective:F4}s");
        EditorGUILayout.LabelField("Clip End @ Action End", $"{action.AnimationEndRatio:P0}");
        EditorGUILayout.LabelField(
            "Clip Progress @ nt=1",
            $"{ActionTimeAuthority.MapNormalizedTimeToClipProgress(1f, action):P0}");
        EditorGUILayout.LabelField("Computed AnimSpeed", $"{computedAnimSpeed:F3}");

        using (new EditorGUI.DisabledScope(action.MotionProfile == null))
        {
            EditorGUILayout.LabelField(
                $"Motion 主轴 {action.PrincipalAxis} 位移 (nt=1, 100%)",
                action.MotionProfile != null ? $"{motionDist:F3} m" : "—");
        }

        if (action.MainClip != null)
        {
            EditorGUILayout.LabelField("Clip Length", $"{action.MainClip.length:F4}s");
            EditorGUILayout.LabelField("Suggested Duration (Ratio→Duration)", $"{suggestedDuration:F4}s");
            EditorGUILayout.LabelField("Suggested Ratio (Duration→Ratio)", $"{suggestedRatio:F3}");
        }

        if (action.MotionProfile != null)
        {
            var motionDur = MotionDurationResolver.Resolve(action);
            EditorGUILayout.LabelField("Runtime Motion Duration", $"{motionDur:F4}s");
        }

        EditorGUILayout.HelpBox(
            "三条时间轴：Action nt | Motion t=nt（100%位移）| Clip progress=nt×Ratio。\n" +
            "Anim Speed 可手调；「按 Duration 匹配 Clip 速率」= (Clip×Ratio)÷Duration 写入上方字段。",
            MessageType.None);

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    static void SetRatio(ActionDataSO action, float ratio)
    {
        Undo.RecordObject(action, "Set Animation End Ratio");
        action.AnimationEndRatio = ratio;
        EditorUtility.SetDirty(action);
    }

    static void LogMatchClipSpeed(ActionDataSO action)
    {
        if (action == null)
        {
            return;
        }

        var dur = ActionTimeAuthority.ResolveAuthoredLogicDurationSeconds(action);
        var ratio = action.AnimationEndRatio;
        var clipProgress = ActionTimeAuthority.MapNormalizedTimeToClipProgress(1f, action);
        var motionEnd = ActionTimeAuthority.MeasurePrincipalAxisDisplacementAtActionEnd(action);

        Debug.Log(
            $"[ActionTime][ClipMatch] '{action.name}' Duration={dur:F3}s Ratio={ratio:F2} " +
            $"AnimSpeed={action.AnimSpeed:F3} → Clip={clipProgress:P0} | Motion {action.PrincipalAxis}={motionEnd:F2}m");
    }

    static void RefreshPrincipalDisplacementFromMotionProfile(ActionDataSO action)
    {
        var profile = action.MotionProfile;
        if (profile == null)
        {
            return;
        }

        var dist = ActionTimeAuthority.MeasurePrincipalAxisDisplacementAtActionEnd(action);
        EditorGUIUtility.PingObject(profile);
        Debug.Log(
            $"[ActionTime] '{action.name}' ← '{profile.name}' " +
            $"axis={action.PrincipalAxis} displacement={dist:F3}m duration={action.Duration:F3}s ratio={action.AnimationEndRatio:F3}");
    }
}
#endif

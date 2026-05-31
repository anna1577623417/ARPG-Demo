#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ActionDataSO))]
public sealed class ActionDataInspector : Editor
{
    static bool s_timeFoldout;
    static bool s_extractFoldout;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var action = (ActionDataSO)target;
        if (action == null)
        {
            return;
        }

        DrawTimeAuthoritySection(action);
        DrawMotionPassSection(action);
    }

    void DrawTimeAuthoritySection(ActionDataSO action)
    {
        EditorGUILayout.Space(6f);
        s_timeFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            s_timeFoldout,
            "Time Authority（可选）");
        if (!s_timeFoldout)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        EditorGUILayout.LabelField("Logic Duration", $"{action.Duration:F4}s");
        if (action.MainClip != null)
        {
            EditorGUILayout.LabelField("Clip Length", $"{action.MainClip.length:F4}s");
            EditorGUILayout.LabelField(
                "Clip Wall @ AnimSpeed",
                $"{MotionDurationResolver.ResolveClipWallClockSeconds(action):F4}s");
        }

        using (new EditorGUI.DisabledScope(action.MainClip == null))
        {
            if (GUILayout.Button("Import Clip Length → Logic Duration"))
            {
                Undo.RecordObject(action, "Import Clip Length");
                action.Duration = action.MainClip.length;
                if (action.MotionProfile != null)
                {
                    Undo.RecordObject(action.MotionProfile, "Sync Reference Duration");
                    action.MotionProfile.Duration_AuthoringReference = action.Duration;
                    EditorUtility.SetDirty(action.MotionProfile);
                }

                EditorUtility.SetDirty(action);
            }
        }

        EditorGUILayout.HelpBox(
            "Motion Runtime 以 Action.LogicDuration 为唯一时间源；MotionProfile.UseActionDuration 默认 ON。",
            MessageType.None);
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    void DrawMotionPassSection(ActionDataSO action)
    {
        EditorGUILayout.Space(8f);
        s_extractFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            s_extractFoldout,
            "Motion Extract → MotionProfile（可选）");
        if (!s_extractFoldout)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        var canPass = action.MainClip != null && action.MotionProfile != null;
        using (new EditorGUI.DisabledScope(!canPass))
        {
            if (GUILayout.Button("传递 MainClip → MotionProfile"))
            {
                PassMainClipToMotionProfile(action);
            }
        }

        if (action.MainClip == null)
        {
            EditorGUILayout.HelpBox("需要 MainClip。", MessageType.Info);
        }

        if (action.MotionProfile == null)
        {
            EditorGUILayout.HelpBox("需要 MotionProfile 引用。", MessageType.Warning);
        }
        else if (canPass)
        {
            var bound = action.MotionProfile.SourceClip == action.MainClip;
            EditorGUILayout.HelpBox(
                bound
                    ? $"已绑定：{action.MotionProfile.name}.SourceClip = {action.MainClip.name}。在 MotionProfile 中展开【Clip → XYZ】并选中 Rig 后提取位移。"
                    : "点击按钮将 MainClip 写入 MotionProfile.SourceClip；XYZ 采样在 MotionProfile Inspector 完成。",
                MessageType.None);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    static void PassMainClipToMotionProfile(ActionDataSO action)
    {
        var profile = action.MotionProfile;
        if (profile == null || action.MainClip == null)
        {
            return;
        }

        Undo.RecordObject(profile, "Assign Source Clip From Action");
        profile.SourceClip = action.MainClip;
        EditorUtility.SetDirty(profile);
        EditorGUIUtility.PingObject(profile);

        Debug.Log(
            $"[MotionXYZ] Action '{action.name}' → MotionProfile '{profile.name}'.SourceClip = '{action.MainClip.name}'");
    }
}
#endif

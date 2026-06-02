#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ActionDataSO))]
public sealed partial class ActionDataInspector : Editor
{
    static bool s_timeFoldout;
    static bool s_extractFoldout;
    static bool s_timelineFoldout = true;

    static readonly string[] HiddenInDefaultInspector =
    {
        "m_Script",
        nameof(ActionDataSO.Duration),
        nameof(ActionDataSO.AnimationEndRatio),
        nameof(ActionDataSO.AnimSpeed),
        nameof(ActionDataSO.DurationStatScaling),
        nameof(ActionDataSO.PrincipalAxis),
        nameof(ActionDataSO.Windows),
        nameof(ActionDataSO.TeleportTriggers),
        nameof(ActionDataSO.TimelineMarkers),
    };

    public override void OnInspectorGUI()
    {
        var action = (ActionDataSO)target;
        if (action == null)
        {
            return;
        }

        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, HiddenInDefaultInspector);
        DrawTimelineSection(action);
        serializedObject.ApplyModifiedProperties();

        DrawTimeAuthoritySection(action);
        DrawMotionPassSection(action);
    }

    void DrawTimelineSection(ActionDataSO action)
    {
        EditorGUILayout.Space(8f);
        s_timelineFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            s_timelineFoldout,
            "Timeline（139.2 · Interrupt + Combat）");

        if (!s_timelineFoldout)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        var winCount = action.Windows != null ? action.Windows.Count : 0;
        var tpCount = action.TeleportTriggers != null ? action.TeleportTriggers.Count : 0;
        var mkCount = action.TimelineMarkers != null ? action.TimelineMarkers.Count : 0;
        EditorGUILayout.LabelField("Windows", winCount.ToString());
        EditorGUILayout.LabelField("Teleport Triggers", tpCount.ToString());
        EditorGUILayout.LabelField("Timeline Markers", mkCount.ToString());

        EditorGUILayout.HelpBox(
            "推荐用「打开 Action 时间轴编辑器」：左右分栏，属性不再与时间轴纵向堆叠。",
            MessageType.None);

        if (GUILayout.Button("打开 Action 时间轴编辑器", GUILayout.Height(26f)))
        {
            ActionDataTimelineEditor.Open(action);
        }

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

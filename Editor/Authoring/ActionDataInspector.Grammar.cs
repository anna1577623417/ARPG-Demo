#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed partial class ActionDataInspector
{
    static bool s_grammarFoldout = true;

    static readonly string[] HiddenGrammarInDefaultInspector =
    {
        nameof(ActionDataSO.IsLocomotionRecovery),
        nameof(ActionDataSO.TransitionType),
        nameof(ActionDataSO.OverrideGrammar),
        nameof(ActionDataSO.GrammarOverride),
    };

    void DrawGrammarSection(ActionDataSO action)
    {
        EditorGUILayout.Space(8f);
        s_grammarFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            s_grammarFoldout,
            "Motion Grammar (184.4)");

        if (!s_grammarFoldout)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        DrawProperty(nameof(ActionDataSO.TransitionType));
        DrawProperty(nameof(ActionDataSO.IsLocomotionRecovery));

        var inherited = MotionGrammar.ResolveGrammar(action);
        var hasTransition = action.TransitionType != TransitionType.None;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Grammar (inherited)", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Toggle("Own Presentation", inherited.OwnsPresentation);
            EditorGUILayout.Toggle("Consume Direction Change", inherited.ConsumesDirectionChange);
            EditorGUILayout.Toggle("Consume Momentum Change", inherited.ConsumesMomentumChange);
            EditorGUILayout.Toggle("Block Other Transitions", inherited.BlocksOtherTransitions);
        }

        DrawProperty(nameof(ActionDataSO.OverrideGrammar));
        if (action.OverrideGrammar)
        {
            DrawProperty(nameof(ActionDataSO.GrammarOverride));
        }

        if (hasTransition)
        {
            EditorGUILayout.HelpBox(MotionGrammar.GetDocumentation(action.TransitionType), MessageType.Info);
        }
        else if (action.IsLocomotionRecovery)
        {
            EditorGUILayout.HelpBox(
                "IsLocomotionRecovery 已勾选但 TransitionType=None。请设为 Start 或 End，或取消 Recovery 标记。",
                MessageType.Warning);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }
}
#endif

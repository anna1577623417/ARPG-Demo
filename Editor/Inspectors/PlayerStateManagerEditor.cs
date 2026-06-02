#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerStateManager))]
internal sealed class PlayerStateManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(
            "连续状态打断（137.1）\n" +
            "· Locomotion / Airborne 的 Allowed Categories：地面/空中 pillars 能接哪些「动作类别」的意图。\n" +
            "· 与 Action 内 ActionWindow.InterruptibleByCategories 是两层：先过 pillar 类别，再过动作窗口。\n" +
            "· 不再使用 dodge_Interrupt / jump_Interrupt 等 StateTag 位配置连续状态。",
            MessageType.Info);
        EditorGUILayout.HelpBox(
            "AbilityMap（Loadout）\n" +
            "· CombatGraph 流转边准入，不经 Action 打断窗。\n" +
            "· Route 起手：abilityGateRules；动作内打断：ActionWindow 类别掩码。",
            MessageType.None);
    }
}

#endif

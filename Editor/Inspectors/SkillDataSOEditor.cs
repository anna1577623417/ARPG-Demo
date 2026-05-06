#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkillDataSO))]
public sealed class SkillDataSOEditor : Editor
{
    static readonly string ComboVsStagesHelp =
        "连招链 comboChain：每次输入切换到「另一张 SkillDataSO」资产；每一段可有自带 stages。\n\n"
        + "同一 Sheet 多 stages：不切换 Skill 资产，只在本 Skill 内线性地选 Stage（含 Charge 长按落档、末尾多段收尾）。\n\n"
        + "二段窗口 SecondStageAvailable：任务调用 Player.SkillNotifySecondStageAvailable 后，在同一 Segment 内把「下一发」视作进阶段位（Prepare 时用虚拟段位解析，Consume 成功后才 AdvanceStage）；"
        + "不会自动沿着 comboChain 走下一段。\n\n"
        + "不建议：根 Skill 既配长 comboChain 又在同一根上叠多 Stage + 二段窗——除非你愿意逐条推敲 CDPolicy / OnLastCast / CanCast。";

    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(ComboVsStagesHelp, MessageType.Info);
        EditorGUILayout.Space(4f);
        DrawDefaultInspector();
    }
}

#endif

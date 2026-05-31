#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Charge Route Inspector — SingleClip 模式下在 Stages 区上方提示编排约定。
/// </summary>
[CustomEditor(typeof(ChargeRouteDefinition))]
public sealed class ChargeRouteDefinitionEditor : UnityEditor.Editor
{
    const string SingleClipStagesHelp =
        "【SingleClip · Stages 编排】\n" +
        "· 仅 Stages[0] 作为整条蓄力的动作时间线（前摇 → HoldAnchor 凝滞 → 松手收尾均在同一 Clip 上）。\n" +
        "· 凝滞点与动画压速由下方「Single-Clip Animation Lock-in」配置，不由 Stages[1] 承担。\n" +
        "· 若 Stages 多于 1 段：Stages[1] 及之后应在本段 Stage 资产配置 Transition → Auto（openTime≈1）衔接下一段，" +
        "常用于收招/后摇；勿让后续 Stage 单独充当蓄力时间轴。";

    public override void OnInspectorGUI()
    {
        var charge = (ChargeRouteDefinition)target;
        if (charge.PresentationMode == ChargePresentationMode.SingleClip)
        {
            EditorGUILayout.HelpBox(SingleClipStagesHelp, MessageType.Info);
        }

        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "ownerGroup",
            "overrideCooldown",
            "overrideIcon",
            "overrideCost");
        SkillRouteGroupMembershipDrawer.Draw(serializedObject);
        serializedObject.ApplyModifiedProperties();
    }
}
#endif

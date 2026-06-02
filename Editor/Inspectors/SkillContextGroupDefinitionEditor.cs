#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkillContextGroupDefinition))]
public sealed class SkillContextGroupDefinitionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var def = (SkillContextGroupDefinition)target;
        if (def == null)
        {
            return;
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("匹配摘要", EditorStyles.boldLabel);

        var slotNote = def.RequiredSlot == SkillEntrySlot.Any ? "任意槽位" : def.RequiredSlot.ToString();
        var dirNote = def.RequireDirectional ? "Directional" : "任意语义";
        var ctxNote = def.RequireGrounded ? "接地" : def.RequireAirborne ? "滞空" : "地面/滞空不限";
        var moveNote = def.RequiredMoveDirection == MoveDirection8.None
            ? "方向不限"
            : def.RequiredMoveDirection.ToString();

        EditorGUILayout.HelpBox(
            $"当 slot={slotNote} 且 {dirNote} 且 {ctxNote} 且 {moveNote} → Group={def.TargetGroup?.name ?? "(未配置)"}",
            def.TargetGroup != null ? MessageType.None : MessageType.Warning);
    }
}
#endif

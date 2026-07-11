#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 188.3 W2 — HitShapeSO 通用 Inspector：在默认 Inspector 下方追加 "Preview in Scene" 按钮。
/// </summary>
[CustomEditor(typeof(HitShapeSO), editorForChildClasses: true)]
public sealed class HitShapeSOInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Scene Preview (188.3)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "选中 CombatObjectDefinitionSO 可在 Scene 用 Move/Rotate/Scale 编辑攻击盒。\n" +
            "或：选择 Transform → Preview → 只读查看 Shape。",
            MessageType.Info);

        HitShapeGizmoPreview.FollowSelection = EditorGUILayout.Toggle(
            "Follow Selection", HitShapeGizmoPreview.FollowSelection);
        HitShapeGizmoPreview.Color = EditorGUILayout.ColorField("Gizmo Color", HitShapeGizmoPreview.Color);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Preview in Scene", GUILayout.Height(24)))
            {
                var t = Selection.activeTransform;
                HitShapeGizmoPreview.SetTarget((HitShapeSO)target, t);
                if (t == null)
                {
                    EditorUtility.DisplayDialog("Preview",
                        "未选中任何 Transform；Gizmo 将绘制在原点。", "OK");
                }
            }
            if (GUILayout.Button("Clear Preview", GUILayout.Height(24)))
            {
                HitShapeGizmoPreview.Clear();
            }
        }
    }
}
#endif

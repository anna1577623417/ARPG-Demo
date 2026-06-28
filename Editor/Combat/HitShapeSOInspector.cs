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
            "选择场景中一个 Transform → 点击 Preview → 在 Scene 中查看 Shape Gizmo。\n" +
            "Follow Selection 勾选后，切换 Selection 时 Gizmo 跟随移动。",
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

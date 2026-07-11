#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 214.3 / 214.5 — Scene 中编辑 CombatObject 攻击盒（Offset / Rotation / Shape 尺寸）。
/// 遵循 Unity 工具栏 W/E/R，同一时刻只显示一种 Handle。
/// </summary>
[InitializeOnLoad]
public static class CombatHitVolumeSceneEditor
{
    static CombatObjectDefinitionSO s_activeDef;
    static Transform s_anchor;

    static CombatHitVolumeSceneEditor()
    {
        Selection.selectionChanged += OnSelectionChanged;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    static void OnSelectionChanged()
    {
        if (Selection.activeObject is CombatObjectDefinitionSO def)
        {
            var anchor = ActionDataTimelineEditor.ActiveInstance?.GizmoAnchorOverride
                         ?? Selection.activeTransform;
            SetActive(def, anchor);
        }
    }

    public static void SetActive(CombatObjectDefinitionSO def, Transform anchor)
    {
        s_activeDef = def;
        s_anchor = anchor;
        CombatHitVolumeEditProbe.LogActivate(def, anchor);
        SceneView.RepaintAll();
    }

    static void OnSceneGUI(SceneView view)
    {
        if (s_activeDef == null)
        {
            return;
        }

        var anchor = ResolveAnchor();
        if (anchor == null)
        {
            Handles.BeginGUI();
            GUI.Box(new Rect(10f, 10f, 360f, 40f), "选中场景角色 Transform 作为攻击盒锚点");
            Handles.EndGUI();
            return;
        }

        var spawnSource = s_activeDef.SpawnSource;
        var basis = GetBasisTransform(anchor, spawnSource);
        CombatHitPreviewRig.TryResolveSpawn(
            anchor, spawnSource, s_activeDef.LocalOffset, s_activeDef.LocalEulerOffset,
            out var worldPos, out var worldRot);

        if (s_activeDef.Shape != null)
        {
            HitShapeGizmoPreview.DrawShapeHandles(
                s_activeDef.Shape, worldPos, worldRot, new Color(1f, 0.45f, 0.2f, 0.55f));
        }

        var tool = Tools.current;
        var changed = false;
        var newPos = worldPos;
        var newRot = worldRot;

        switch (tool)
        {
            case Tool.Rotate:
                EditorGUI.BeginChangeCheck();
                newRot = Handles.RotationHandle(worldRot, worldPos);
                changed = EditorGUI.EndChangeCheck();
                break;

            case Tool.Scale:
                if (s_activeDef.Shape != null)
                {
                    changed = DrawShapeScaleHandle(worldPos, worldRot, s_activeDef.Shape);
                }

                break;

            default:
                EditorGUI.BeginChangeCheck();
                newPos = Handles.PositionHandle(worldPos, worldRot);
                changed = EditorGUI.EndChangeCheck();
                break;
        }

        if (changed && tool != Tool.Scale)
        {
            Undo.RecordObject(s_activeDef, "Edit Combat Hit Volume");
            var invRot = Quaternion.Inverse(basis.rotation);
            s_activeDef.LocalOffset = invRot * (newPos - basis.position);
            var localRot = Quaternion.Inverse(basis.rotation) * newRot;
            s_activeDef.LocalEulerOffset = localRot.eulerAngles;
            EditorUtility.SetDirty(s_activeDef);
            CombatHitVolumeEditProbe.LogTransform(
                tool == Tool.Rotate ? "ROTATE" : "MOVE",
                s_activeDef,
                newPos,
                newRot,
                s_activeDef.LocalOffset,
                s_activeDef.LocalEulerOffset);
        }

        DrawSceneHud(anchor, spawnSource, tool);
    }

    static void DrawSceneHud(Transform anchor, SpawnSource spawnSource, Tool tool)
    {
        Handles.BeginGUI();
        var toolHint = tool switch
        {
            Tool.Rotate => "旋转 (E)",
            Tool.Scale => "缩放 (R) — 改 Shape 尺寸",
            _ => "位移 (W)",
        };
        GUI.Label(new Rect(10f, 10f, 640f, 22f),
            $"[CombatHitEdit] {s_activeDef.name} · 锚点={anchor.name} · Spawn={spawnSource} · {toolHint}");
        GUI.Label(new Rect(10f, 32f, 640f, 18f),
            "W=位移  E=旋转  R=缩放 Shape；Log：GameMain → Debug → Combat Hit");
        Handles.EndGUI();
    }

    static Transform ResolveAnchor()
    {
        if (ActionDataTimelineEditor.ActiveInstance?.GizmoAnchorOverride != null)
        {
            return ActionDataTimelineEditor.ActiveInstance.GizmoAnchorOverride;
        }

        if (s_anchor != null)
        {
            return s_anchor;
        }

        return ActionTimelineEditorUI.ResolvePreviewAnchor(null);
    }

    static Transform GetBasisTransform(Transform anchor, SpawnSource source)
    {
        if (CombatSpawnBoneResolver.TryGetBoneTransform(anchor, source, out var boneTf))
        {
            return boneTf;
        }

        return anchor;
    }

    static bool DrawShapeScaleHandle(Vector3 worldPos, Quaternion worldRot, HitShapeSO shape)
    {
        EditorGUI.BeginChangeCheck();
        var handleSize = HandleUtility.GetHandleSize(worldPos);
        var scale = Handles.ScaleHandle(Vector3.one, worldPos, worldRot, handleSize);
        if (!EditorGUI.EndChangeCheck())
        {
            return false;
        }

        Undo.RecordObject(shape, "Scale Hit Shape");
        var factor = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
        var detail = $"factor={factor:F3}";
        switch (shape)
        {
            case SphereShapeSO sphere:
                sphere.radius = Mathf.Max(0.01f, sphere.radius * factor);
                detail += $" radius={sphere.radius:F3}";
                break;
            case CapsuleShapeSO cap:
                cap.radius = Mathf.Max(0.01f, cap.radius * factor);
                cap.height = Mathf.Max(0.01f, cap.height * factor);
                detail += $" r={cap.radius:F3} h={cap.height:F3}";
                break;
            case BoxShapeSO box:
                box.halfExtents = Vector3.Max(box.halfExtents * factor, Vector3.one * 0.01f);
                detail += $" half={box.halfExtents}";
                break;
        }

        EditorUtility.SetDirty(shape);
        CombatHitVolumeEditProbe.LogScale(shape, detail);
        return true;
    }
}
#endif

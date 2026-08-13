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
            // 224.1 L5：Selection 不再自动进入 Scene Edit；Timeline/Contact 优先。
            if (!CombatSceneEditCoordinator.AllowsLegacyHitVolumeOwner())
            {
                s_activeDef = null;
                s_anchor = null;
                return;
            }

            var anchor = ActionDataTimelineEditor.ActiveInstance?.GizmoAnchorOverride
                         ?? Selection.activeTransform;
            SetActive(def, anchor);
        }
    }

    public static void SetActive(CombatObjectDefinitionSO def, Transform anchor)
    {
        if (def != null && !CombatSceneEditCoordinator.AllowsLegacyHitVolumeOwner())
        {
            Debug.LogWarning(
                "[CombatHitEdit] Legacy HitVolume editor blocked while Timeline/Contact owns Scene. " +
                "Use explicit Edit CO In Scene (Coordinator) instead.");
            return;
        }

        s_activeDef = def;
        s_anchor = anchor;
        if (def != null)
        {
            CombatSceneEditCoordinator.RequestCombatObjectAssetEdit(def);
        }

        CombatHitVolumeEditProbe.LogActivate(def, anchor);
        SceneView.RepaintAll();
    }

    static void OnSceneGUI(SceneView view)
    {
        if (s_activeDef == null)
        {
            return;
        }

        if (!CombatSceneEditCoordinator.AllowsLegacyHitVolumeOwner()
            && CombatSceneEditCoordinator.Session.Mode != CombatSceneEditMode.CombatObjectAssetEdit)
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
            CombatSceneDrawSourceProbe.RegisterPrimaryDraw(
                CombatSceneDrawSourceProbe.SourceLegacyHitVolume,
                worldPos,
                $"def={s_activeDef.name} shape={s_activeDef.Shape.name} spawn={spawnSource}");
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
        CombatSceneDrawSourceProbe.DrawHudBanner(
            CombatSceneDrawSourceProbe.SourceLegacyHitVolume,
            $"def={s_activeDef.name} (Selection auto-active)");
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
        // 224.1 L6：与 ContactSceneEditor 对齐，禁止 max-scale 乘法。
        switch (shape)
        {
            case SphereShapeSO sphere:
            {
                EditorGUI.BeginChangeCheck();
                var next = Handles.RadiusHandle(worldRot, worldPos, sphere.radius);
                if (!EditorGUI.EndChangeCheck()) return false;
                Undo.RecordObject(sphere, "Scale Hit Shape");
                sphere.radius = Mathf.Max(0.01f, next);
                EditorUtility.SetDirty(sphere);
                CombatHitVolumeEditProbe.LogScale(shape, $"radius={sphere.radius:F3}");
                return true;
            }
            case CapsuleShapeSO cap:
            {
                var axis = worldRot * Vector3.up;
                var half = Mathf.Max(0f, cap.height * 0.5f - cap.radius);
                EditorGUI.BeginChangeCheck();
                var nextRadius = Handles.RadiusHandle(worldRot, worldPos, cap.radius);
                var nextTop = Handles.Slider(worldPos + axis * half, axis);
                if (!EditorGUI.EndChangeCheck()) return false;
                Undo.RecordObject(cap, "Scale Hit Shape");
                cap.radius = Mathf.Max(0.01f, nextRadius);
                var newHalf = Vector3.Dot(nextTop - worldPos, axis);
                cap.height = Mathf.Max(cap.radius * 2f, Mathf.Abs(newHalf) * 2f + cap.radius * 2f);
                EditorUtility.SetDirty(cap);
                CombatHitVolumeEditProbe.LogScale(shape, $"r={cap.radius:F3} h={cap.height:F3}");
                return true;
            }
            case BoxShapeSO box:
            {
                var center = worldPos + worldRot * box.offset;
                var right = worldRot * Vector3.right;
                var up = worldRot * Vector3.up;
                var fwd = worldRot * Vector3.forward;
                var he = box.halfExtents;
                EditorGUI.BeginChangeCheck();
                var px = Handles.Slider(center + right * he.x, right);
                var py = Handles.Slider(center + up * he.y, up);
                var pz = Handles.Slider(center + fwd * he.z, fwd);
                if (!EditorGUI.EndChangeCheck()) return false;
                Undo.RecordObject(box, "Scale Hit Shape");
                box.halfExtents = new Vector3(
                    Mathf.Max(0.01f, Vector3.Dot(px - center, right)),
                    Mathf.Max(0.01f, Vector3.Dot(py - center, up)),
                    Mathf.Max(0.01f, Vector3.Dot(pz - center, fwd)));
                EditorUtility.SetDirty(box);
                CombatHitVolumeEditProbe.LogScale(shape, $"half={box.halfExtents}");
                return true;
            }
            default:
                return false;
        }
    }
}
#endif

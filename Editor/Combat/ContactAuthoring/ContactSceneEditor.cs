#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class ContactSceneEditor
{
    static ContactSceneEditor()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    static void OnSceneGUI(SceneView view)
    {
        if (!ContactAuthoringSelectionContext.TryGet(out _)) return;

        var anchor = ActionDataTimelineEditor.ActiveInstance?.GizmoAnchorOverride
                     ?? ActionTimelineEditorUI.ResolvePreviewAnchor(null);
        if (!ContactPreviewResolver.TryResolve(anchor, out var preview, out var failure))
        {
            DrawHud($"Hitbox Preview：{failure}");
            return;
        }

        CombatSceneDrawSourceProbe.BeginSceneGuiFrame();
        var color = preview.IsActiveAtPreviewTime
            ? new Color(1f, 0.25f, 0.2f, 1f)
            : new Color(1f, 0.55f, 0.2f, 0.58f);
        if (preview.Spec.ShapeMode == HitShapeMode.Volume && preview.Spec.Geometry != null)
        {
            HitShapeGizmoPreview.DrawShapeHandles(
                preview.Spec.Geometry,
                preview.WorldPosition,
                preview.WorldRotation,
                color);
            CombatSceneDrawSourceProbe.RegisterPrimaryDraw(
                CombatSceneDrawSourceProbe.SourceContactScene,
                preview.WorldPosition,
                $"event={preview.Event.DebugName} edit={preview.Selection.EditLayer} " +
                $"geo={preview.Spec.Geometry.name}");
        }
        else if (preview.Spec.ShapeMode == HitShapeMode.WeaponTrace)
        {
            DrawWeaponLayout(preview);
            CombatSceneDrawSourceProbe.RegisterPrimaryDraw(
                CombatSceneDrawSourceProbe.SourceContactScene,
                preview.WorldPosition,
                $"event={preview.Event.DebugName} mode=WeaponTrace");
        }

        ContactPoseGeometryBaselineEditorProbe.TryCapturePreviewFromScene(in preview);
        DrawTransformHandle(preview);
        DrawHud(
            $"Hitbox · {preview.Event.DebugName} · {(preview.IsActiveAtPreviewTime ? "ACTIVE" : "INACTIVE PREVIEW")} · " +
            $"Edit={preview.Selection.EditLayer}");
        CombatSceneDrawSourceProbe.DrawHudBanner(
            CombatSceneDrawSourceProbe.SourceContactScene,
            $"eventId={preview.Selection.EventId} defRev={preview.Spec.DefinitionRevision}");

        CombatSceneEditCoordinator.RequestTimelineContactEdit(
            preview.Selection.Action != null ? preview.Selection.Action.name : null,
            preview.Event.WindowId,
            preview.Selection.EventId,
            preview.Spec.Definition != null ? preview.Spec.Definition.name : null,
            preview.Spec.DefinitionRevision);
    }

    static void DrawTransformHandle(in ContactPreviewState preview)
    {
        var tool = Tools.current;
        if (tool == Tool.Scale)
        {
            if (preview.Spec.Geometry != null)
            {
                DrawGeometryScaleHandle(preview.Spec.Geometry, preview.WorldPosition, preview.WorldRotation);
            }

            return;
        }

        EditorGUI.BeginChangeCheck();
        var nextPosition = preview.WorldPosition;
        var nextRotation = preview.WorldRotation;
        if (tool == Tool.Rotate)
        {
            nextRotation = Handles.RotationHandle(preview.WorldRotation, preview.WorldPosition);
        }
        else
        {
            nextPosition = Handles.PositionHandle(preview.WorldPosition, preview.WorldRotation);
        }

        if (!EditorGUI.EndChangeCheck()) return;
        WriteTransform(preview, nextPosition, nextRotation);
    }

    static void WriteTransform(
        in ContactPreviewState preview,
        Vector3 worldPosition,
        Quaternion worldRotation)
    {
        var definition = preview.Spec.Definition;
        if (definition != null
            && definition.ActionContactAuthoring.UseExplicitData)
        {
            if (!CombatObjectAuthoringService.TryChangeLocalPoseFromWorldHandle(
                    definition,
                    preview.Basis.position,
                    preview.Basis.rotation,
                    worldPosition,
                    worldRotation,
                    out var failure))
            {
                Debug.LogWarning($"[ContactAuthoring] {failure}");
            }

            return;
        }

        ContactPlacementMath.ResolveLocal(
            preview.Basis.position,
            preview.Basis.rotation,
            worldPosition,
            worldRotation,
            out var localOffset,
            out var localRotation);

        switch (preview.Selection.EditLayer)
        {
            case ContactAuthoringEditLayer.EventOverride:
            {
                var action = preview.Selection.Action;
                var index = ContactPreviewResolver.FindEventIndex(action, preview.Selection.EventId);
                if (index < 0) return;
                Undo.RecordObject(action, "Edit Contact Event Override");
                var contactEvent = action.ContactEvents[index];
                contactEvent.Override.OverridePlacement = true;
                contactEvent.Override.Origin = preview.Spec.Origin;
                contactEvent.Override.LocalOffset = localOffset;
                contactEvent.Override.LocalEuler = localRotation.eulerAngles;
                action.ContactEvents[index] = contactEvent;
                EditorUtility.SetDirty(action);
                break;
            }

            case ContactAuthoringEditLayer.SharedPreset:
            {
                var preset = preview.Spec.ShapePreset;
                if (preset == null) return;
                Undo.RecordObject(preset, "Edit Shared Contact Preset");
                preset.DefaultOrigin = preview.Spec.Origin;
                preset.DefaultLocalOffset = localOffset;
                preset.DefaultLocalEuler = localRotation.eulerAngles;
                EditorUtility.SetDirty(preset);
                break;
            }

            case ContactAuthoringEditLayer.SharedDefinition:
                Debug.LogWarning(
                    "[ContactAuthoring] Definition owns combat semantics, not placement. " +
                    "Enable CO Single Source V1 or choose EventOverride/SharedPreset.");
                break;
        }
    }

    static void DrawGeometryScaleHandle(
        HitShapeSO geometry,
        Vector3 worldPosition,
        Quaternion worldRotation)
    {
        // 224.1 L6：禁止 ScaleHandle(Vector3.one)+max(xyz)。按类型写独立尺寸。
        switch (geometry)
        {
            case SphereShapeSO sphere:
            {
                EditorGUI.BeginChangeCheck();
                var next = Handles.RadiusHandle(worldRotation, worldPosition, sphere.radius);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(sphere, "Scale Sphere Radius");
                    sphere.radius = Mathf.Max(0.01f, next);
                    EditorUtility.SetDirty(sphere);
                }

                break;
            }
            case CapsuleShapeSO capsule:
            {
                var axis = worldRotation * Vector3.up;
                var half = Mathf.Max(0f, capsule.height * 0.5f - capsule.radius);
                var top = worldPosition + axis * half;
                EditorGUI.BeginChangeCheck();
                var nextRadius = Handles.RadiusHandle(worldRotation, worldPosition, capsule.radius);
                var nextTop = Handles.Slider(top, axis);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(capsule, "Scale Capsule");
                    capsule.radius = Mathf.Max(0.01f, nextRadius);
                    var newHalf = Vector3.Dot(nextTop - worldPosition, axis);
                    capsule.height = Mathf.Max(capsule.radius * 2f, Mathf.Abs(newHalf) * 2f + capsule.radius * 2f);
                    EditorUtility.SetDirty(capsule);
                }

                break;
            }
            case BoxShapeSO box:
            {
                var center = worldPosition + worldRotation * box.offset;
                var right = worldRotation * Vector3.right;
                var up = worldRotation * Vector3.up;
                var fwd = worldRotation * Vector3.forward;
                var he = box.halfExtents;
                EditorGUI.BeginChangeCheck();
                var px = Handles.Slider(center + right * he.x, right);
                var py = Handles.Slider(center + up * he.y, up);
                var pz = Handles.Slider(center + fwd * he.z, fwd);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(box, "Scale Box HalfExtents");
                    box.halfExtents = new Vector3(
                        Mathf.Max(0.01f, Vector3.Dot(px - center, right)),
                        Mathf.Max(0.01f, Vector3.Dot(py - center, up)),
                        Mathf.Max(0.01f, Vector3.Dot(pz - center, fwd)));
                    EditorUtility.SetDirty(box);
                }

                break;
            }
            default:
                Debug.LogWarning($"[ContactAuthoring] Geometry {geometry.GetType().Name} has no dedicated scale handle yet.");
                break;
        }
    }

    static void DrawWeaponLayout(in ContactPreviewState preview)
    {
        var layout = preview.Spec.ShapePreset != null ? preview.Spec.ShapePreset.WeaponSocketLayout : null;
        if (layout == null || layout.Bindings == null || layout.Bindings.Length == 0)
        {
            Handles.Label(preview.WorldPosition + Vector3.up * 0.2f, "Missing WeaponSocketLayout");
            return;
        }

        var previous = default(Vector3);
        var hasPrevious = false;
        for (var i = 0; i < layout.Bindings.Length; i++)
        {
            var binding = layout.Bindings[i];
            var point = preview.WorldPosition + preview.WorldRotation * binding.RootLocalPosition;
            Handles.DrawWireDisc(point, preview.WorldRotation * Vector3.up, Mathf.Max(0.005f, binding.Radius));
            if (hasPrevious) Handles.DrawLine(previous, point);
            previous = point;
            hasPrevious = true;
        }
    }

    static void DrawHud(string text)
    {
        Handles.BeginGUI();
        GUI.Box(new Rect(12f, 12f, 720f, 24f), text);
        Handles.EndGUI();
    }
}
#endif

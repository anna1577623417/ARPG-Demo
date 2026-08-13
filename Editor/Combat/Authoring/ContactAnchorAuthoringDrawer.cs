#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>224.1 L2 — Binding → Origin → LocalPose 依赖式 UI。</summary>
public static class ContactAnchorAuthoringDrawer
{
    public static void Draw(CombatObjectDefinitionSO definition)
    {
        if (definition == null) return;

        var data = definition.ActionContactAuthoring;
        EditorGUILayout.LabelField("Binding / Origin / Local Pose", EditorStyles.boldLabel);

        if (!data.UseExplicitData)
        {
            EditorGUILayout.HelpBox(
                "Data source: Legacy Preset/Override（只读迁移期）。点击下方启用 CO Single Source V1。",
                MessageType.Warning);
            if (GUILayout.Button("Enable CombatObject Single Source V1"))
            {
                if (!CombatObjectAuthoringService.TryEnsureExplicitAuthoring(definition, out var failure))
                {
                    Debug.LogWarning($"[CombatAuthoring] {failure}");
                }
            }

            return;
        }

        EditorGUILayout.HelpBox(
            "Data source: CombatObject Single Source V1",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        var binding = (ContactAnchorBindingMode)EditorGUILayout.EnumPopup("Binding Mode", data.BindingMode);
        if (EditorGUI.EndChangeCheck())
        {
            if (!CombatObjectAuthoringService.TryChangeBinding(
                    definition,
                    binding,
                    ContactOriginChangeMode.KeepLocalPose,
                    context: null,
                    out var failure))
            {
                Debug.LogWarning($"[CombatAuthoring] {failure}");
            }

            return;
        }

        using (new EditorGUI.DisabledScope(data.BindingMode == ContactAnchorBindingMode.StaticAtWindowStart))
        {
            EditorGUI.BeginChangeCheck();
            var sweep = (ContactSweepPolicy)EditorGUILayout.EnumPopup("Sweep Policy", data.SweepPolicy);
            if (EditorGUI.EndChangeCheck())
            {
                if (!CombatObjectAuthoringService.TryChangeSweepPolicy(definition, sweep, out var failure))
                {
                    Debug.LogWarning($"[CombatAuthoring] {failure}");
                }

                return;
            }
        }

        if (data.BindingMode == ContactAnchorBindingMode.StaticAtWindowStart)
        {
            EditorGUILayout.HelpBox("Static binding forces Sweep=None.", MessageType.None);
        }

        EditorGUI.BeginChangeCheck();
        var originPolicy = (ContactOriginPolicy)EditorGUILayout.EnumPopup("Origin Policy", data.OriginPolicy);
        var originSource = (SpawnSource)EditorGUILayout.EnumPopup("Origin", data.Origin.Source);
        if (EditorGUI.EndChangeCheck())
        {
            var next = definition.ActionContactAuthoring;
            if (originPolicy == ContactOriginPolicy.Auto)
            {
                BeginAutoOrigin(definition, next);
            }
            else
            {
                CombatObjectAuthoringService.TryChangeOrigin(
                    definition,
                    ContactAnchorReference.FromSpawnSource(originSource),
                    ContactOriginChangeMode.KeepLocalPose,
                    null,
                    out var failure);
                if (!string.IsNullOrEmpty(failure))
                {
                    Debug.LogWarning($"[CombatAuthoring] {failure}");
                }
            }

            return;
        }

        EditorGUI.BeginChangeCheck();
        var localPos = EditorGUILayout.Vector3Field("Local Position", data.LocalPosition);
        var localEuler = EditorGUILayout.Vector3Field("Local Euler", data.LocalEuler);
        if (EditorGUI.EndChangeCheck())
        {
            if (!CombatObjectAuthoringService.TryChangeLocalPose(definition, localPos, localEuler, out var failure))
            {
                Debug.LogWarning($"[CombatAuthoring] {failure}");
            }
        }
    }

    static void BeginAutoOrigin(CombatObjectDefinitionSO definition, ActionContactAuthoringData data)
    {
        Undo.RecordObject(definition, "Set Origin Policy Auto");
        data.OriginPolicy = ContactOriginPolicy.Auto;
        data.Origin = data.BindingMode == ContactAnchorBindingMode.FollowAnchor
            ? ContactAnchorReference.DefaultFollow
            : ContactAnchorReference.DefaultStatic;
        definition.ActionContactAuthoring = data;
        definition.DefinitionRevision = Mathf.Max(0, definition.DefinitionRevision) + 1;
        EditorUtility.SetDirty(definition);
        CombatAuthoringChangeBus.PublishContactConfig(definition, CombatAuthoringChangeKind.ContactConfig);
    }
}
#endif

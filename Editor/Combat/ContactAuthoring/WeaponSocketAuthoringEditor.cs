#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponSocketAuthoring))]
public sealed class WeaponSocketAuthoringEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var authoring = (WeaponSocketAuthoring)target;
        EditorGUILayout.Space(4f);
        using (new EditorGUI.DisabledScope(authoring.BakeTarget == null))
        {
            if (GUILayout.Button("Bake Socket Layout", GUILayout.Height(26f)))
            {
                Bake(authoring);
            }
        }
    }

    static void Bake(WeaponSocketAuthoring authoring)
    {
        if (!TryBuildBindings(authoring, out var bindings, out var failure))
        {
            EditorUtility.DisplayDialog("Weapon Socket Bake", failure, "OK");
            return;
        }

        var target = authoring.BakeTarget;
        Undo.RecordObject(target, "Bake Weapon Socket Layout");
        target.SourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(authoring.gameObject)
                              ?? authoring.gameObject;
        target.Bindings = bindings;
        target.BakeVersion++;
        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssetIfDirty(target);
        EditorGUIUtility.PingObject(target);
    }

    internal static bool TryBuildBindings(
        WeaponSocketAuthoring authoring,
        out WeaponSocketBinding[] bindings,
        out string failure)
    {
        bindings = Array.Empty<WeaponSocketBinding>();
        failure = null;
        if (authoring == null || authoring.Points == null || authoring.Points.Length == 0)
        {
            failure = "No authoring points.";
            return false;
        }

        var seen = new HashSet<string>();
        bindings = new WeaponSocketBinding[authoring.Points.Length];
        for (var i = 0; i < authoring.Points.Length; i++)
        {
            var point = authoring.Points[i];
            if (point.Transform == null || !point.Transform.IsChildOf(authoring.transform))
            {
                failure = $"Point #{i} must reference a Transform under the weapon authoring root.";
                bindings = Array.Empty<WeaponSocketBinding>();
                return false;
            }

            var key = string.IsNullOrWhiteSpace(point.Key) ? point.Slot.ToString() : point.Key.Trim();
            if (!seen.Add(key))
            {
                failure = $"Duplicate socket key: {key}.";
                bindings = Array.Empty<WeaponSocketBinding>();
                return false;
            }

            bindings[i] = new WeaponSocketBinding
            {
                Slot = point.Slot,
                Key = key,
                TransformPath = AnimationUtility.CalculateTransformPath(point.Transform, authoring.transform),
                RootLocalPosition = authoring.transform.InverseTransformPoint(point.Transform.position),
                RootLocalRotation = Quaternion.Inverse(authoring.transform.rotation) * point.Transform.rotation,
                Radius = Mathf.Max(0.001f, point.Radius),
            };
        }

        return true;
    }
}
#endif

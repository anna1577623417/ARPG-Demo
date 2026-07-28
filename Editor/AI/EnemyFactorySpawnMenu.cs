#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class EnemyFactorySpawnMenu
{
    [MenuItem("GameMain/Tools/Enemy/Spawn From Definition", false, 20)]
    static void SpawnFromDefinition()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[EnemyFactory] Spawn requires Play Mode.");
            return;
        }

        var definition = Selection.activeObject as EnemyDefinitionSO;
        if (definition == null)
        {
            Debug.LogWarning("[EnemyFactory] Select an EnemyDefinitionSO asset first.");
            return;
        }

        var view = SceneView.lastActiveSceneView;
        var camera = view != null ? view.camera : null;
        var position = camera != null
            ? camera.transform.position + camera.transform.forward * 5f
            : Vector3.zero;
        var rotation = camera != null
            ? Quaternion.LookRotation(Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up), Vector3.up)
            : Quaternion.identity;

        EnemyFactory.Spawn(definition, position, rotation);
    }

    [MenuItem("GameMain/Tools/Enemy/Spawn From Definition", true)]
    static bool ValidateSpawnFromDefinition()
    {
        return EditorApplication.isPlaying
               && Selection.activeObject is EnemyDefinitionSO;
    }
}
#endif

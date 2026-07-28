using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class EnemyManager : MonoBehaviour
{
    [Header("Spawn Definition")]
    [SerializeField] EnemyDefinitionSO definition;
    [SerializeField] Transform spawnPoint;
    [SerializeField, Min(1)] int initialCount = 1;
    [SerializeField, Min(0f)] float spacing = 2f;
    [SerializeField] bool spawnOnStart = true;
    [SerializeField] bool parentSpawnedEnemies = true;

    readonly List<Enemy> _spawnedEnemies = new List<Enemy>();

    public EnemyDefinitionSO Definition => definition;
    public IReadOnlyList<Enemy> SpawnedEnemies => _spawnedEnemies;

    void Start()
    {
        if (spawnOnStart)
        {
            SpawnInitialEnemies();
        }
    }

    [ContextMenu("Spawn Initial Enemies")]
    public void SpawnInitialEnemies()
    {
        if (definition == null)
        {
            Debug.LogError(
                $"[EnemyManager] Spawn blocked reason=missing-definition manager={name}",
                this);
            return;
        }

        var origin = spawnPoint != null ? spawnPoint : transform;
        var count = Mathf.Max(1, initialCount);
        for (var index = 0; index < count; index++)
        {
            var offset = origin.right * (spacing * index);
            var enemy = EnemyFactory.Spawn(
                definition,
                origin.position + offset,
                origin.rotation,
                parentSpawnedEnemies ? transform : null);

            if (enemy != null)
            {
                _spawnedEnemies.Add(enemy);
            }
        }

        Debug.Log(
            $"[EnemyManager] SpawnInitial result=count={_spawnedEnemies.Count} " +
            $"definition={definition.Id} log=220.8",
            this);
    }

    [ContextMenu("Clear Spawned Enemies")]
    public void ClearSpawnedEnemies()
    {
        for (var index = _spawnedEnemies.Count - 1; index >= 0; index--)
        {
            var enemy = _spawnedEnemies[index];
            if (enemy != null)
            {
                Destroy(enemy.gameObject);
            }
        }

        _spawnedEnemies.Clear();
    }
}

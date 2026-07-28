using UnityEngine;

public static class EnemyFactory
{
    public static Enemy Spawn(
        EnemyDefinitionSO definition,
        Vector3 position,
        Quaternion rotation,
        Transform parent = null)
    {
        if (definition == null)
        {
            Debug.LogError("[EnemyFactory] Spawn blocked reason=missing-definition");
            return null;
        }

        if (!definition.TryValidate(out var reason))
        {
            Debug.LogError(
                $"[EnemyFactory] Spawn blocked definition={definition.name} reason={reason}",
                definition);
            return null;
        }

        var enemy = Object.Instantiate(
            definition.RuntimePrefab,
            position,
            rotation,
            parent);

        enemy.ApplyDefinition(definition);
        var aiController = enemy.GetComponent<AIController>();
        if (aiController == null)
        {
            aiController = enemy.gameObject.AddComponent<AIController>();
        }

        aiController.ApplyDefinition(definition);

        var perception = enemy.GetComponent<EnemyPerception>();
        if (perception == null)
        {
            perception = enemy.gameObject.AddComponent<EnemyPerception>();
        }

        perception.ApplyDefinition(definition.PerceptionConfig);

        if (GameMainDebugSettings.EnemyPerception2208Log
            || GameMainDebugSettings.AIBrain2207Log)
        {
            Debug.Log(
                $"[EnemyFactory] Spawn result=Accepted definition={definition.Id} " +
                $"entity={enemy.name} position={position} log=220.8",
                enemy);
        }

        return enemy;
    }
}

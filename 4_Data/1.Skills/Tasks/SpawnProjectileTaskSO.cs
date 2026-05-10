using UnityEngine;

[CreateAssetMenu(menuName = "Skill/Task/Spawn Projectile")]
public sealed class SpawnProjectileTaskSO : AbilityTaskSO
{
    public GameObject projectilePrefab;
    public float speed = 18f;
    public float lifetime = 3f;
    public Vector3 spawnOffset;
    public float collisionRadius = 0.15f;

    public override AbilityTask CreateInstance() => new SpawnProjectileTask(this);
}

using UnityEngine;

public struct DamageInfo {
    public float Amount;
    public Vector3 HitPoint;
    public Vector3 Force;
    public GameObject Source; // …À∫¶¿¥‘¥
}

public interface IDamageable {
    void TakeDamage(DamageInfo info);
}
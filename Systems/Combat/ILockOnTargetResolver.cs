using UnityEngine;

/// <summary>
/// 战斗系统实现：从场上单位中选出当前应对齐锁定的 Transform（如敌人胸口骨骼）。
/// </summary>
public interface ILockOnTargetResolver
{
    bool TryGetLockOnTarget(out Transform target);
}

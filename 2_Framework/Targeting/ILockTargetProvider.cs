using UnityEngine;

/// <summary>
/// Targeting Runtime 向既有 Player/Motion 暴露的最小锁定读口。
/// 完整候选、Session、HUD、Camera 仍归 Targeting Runtime；Motion 只需要本帧稳定的平面方向。
/// </summary>
public interface ILockTargetProvider
{
    /// <summary>当前是否存在可供 Gameplay 消费的有效锁定目标。</summary>
    bool HasValidLock { get; }

    /// <summary>
    /// 取得从请求者指向锁定 AimPoint 的世界平面单位方向。
    /// 返回 false 表示本帧不应使用锁定方向，调用方必须走自身已有回退逻辑。
    /// </summary>
    bool TryGetPlanarDirection(out Vector3 direction);
}

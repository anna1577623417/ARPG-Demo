using UnityEngine;

/// <summary>
/// 实体状态基类，继承框架层 State&lt;T&gt;。
/// 在通用状态生命周期基础上，增加实体特有的碰撞回调。
///
/// 生命周期：
///   OnEnter    → 状态进入，做准备工作（播动画、订阅事件等）
///   OnLogicUpdate   → 逻辑帧轮询（输入响应、状态切换判断、计时器）
///   OnPhysicsUpdate → 物理帧轮询（刚体力、射线检测、物理移动）
///   OnExit     → 状态退出，清零中间变量、取消订阅
///   OnContact  → 碰撞回调（可选覆写）
/// </summary>
public abstract class EntityState<T> : State<T> where T : Entity<T>
{
    /// <summary>碰撞回调，由 EntityStateManager 转发。</summary>
    public virtual void OnContact(T entity, Collider other) { }
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 非泛型基类，用于 Inspector 引用和 GetComponent。
/// </summary>
public abstract class EntityStateManager : MonoBehaviour { }

/// <summary>
/// 实体状态机管理器。
/// 继承 MonoBehaviour 驱动 Update / FixedUpdate，内部委托框架层 StateMachine 管理状态。
///
/// 职责：
/// 1. 构建状态列表（由子类 BuildStateList 提供）
/// 2. 驱动状态的 LogicUpdate / PhysicsUpdate / OnContact
/// 3. 状态切换时通过 Entity.EventBus 发布事件（纯数据驱动）
/// </summary>
public abstract class EntityStateManager<T> : EntityStateManager where T : Entity<T>
{
    private readonly StateMachine<T> _machine = new StateMachine<T>();

    public T Entity { get; private set; }
    public EntityState<T> Current => _machine.Current as EntityState<T>;
    public EntityState<T> Previous => _machine.Previous as EntityState<T>;

    protected abstract List<EntityState<T>> BuildStateList();

    // ─── 初始化 ───

    protected virtual void Start()
    {
        Entity = GetComponent<T>();

        var entityStates = BuildStateList();
        var states = new List<State<T>>(entityStates.Count);
        foreach (var s in entityStates) states.Add(s);

        _machine.Initialize(Entity, states);
        _machine.Start();

        // 发布初始状态进入事件
        if (Current != null)
        {
            PublishStateEnter(Current);
            PublishStateChange(null, Current);
        }
    }

    // ─── 驱动（将 Unity 的 deltaTime 传入纯 C# 框架层） ───

    protected virtual void Update()
    {
        OnPreLogicUpdate(Time.deltaTime);
        _machine.LogicUpdate(Time.deltaTime);
    }

    /// <summary>
    /// 在 LogicUpdate 之前执行：供子类实现意图缓冲、帧上下文构建等。
    /// </summary>
    protected virtual void OnPreLogicUpdate(float deltaTime)
    {
    }

    protected virtual void FixedUpdate()
    {
        _machine.PhysicsUpdate(Time.fixedDeltaTime);
    }

    // ─── 状态切换入口 ───

    public void Change(int index)
    {
        var prev = Current;
        _machine.Change(index);
        OnStateChanged(prev, Current);
    }

    public void Change<TState>() where TState : EntityState<T>
    {
        var prev = Current;
        _machine.Change<TState>();
        OnStateChanged(prev, Current);
    }

    public void Change(EntityState<T> to)
    {
        var prev = Current;
        _machine.Change(to);
        OnStateChanged(prev, Current);
    }

    /// <summary>
    /// 强制重入指定状态（允许 from == to）。
    /// Why: 某些状态依赖 OnEnter 刷新上下文（例如 Action 中断后切换同类状态实例）。
    /// </summary>
    public void ForceChange<TState>() where TState : EntityState<T>
    {
        var prev = Current;
        _machine.ForceChange<TState>();
        OnStateChanged(prev, Current);
    }

    // ─── 碰撞转发 ───

    public virtual void OnContact(Collider other)
    {
        Current?.OnContact(Entity, other);
    }

    // ─── 查询 ───

    public bool IsCurrentOfType<TState>() where TState : EntityState<T>
    {
        return _machine.IsCurrentOfType<TState>();
    }

    public TState GetState<TState>() where TState : EntityState<T>
    {
        return _machine.GetState<TState>();
    }

    // ─── 事件发布（纯数据驱动，通过 Entity.EventBus） ───

    private void OnStateChanged(EntityState<T> from, EntityState<T> to)
    {
        if (ReferenceEquals(from, to)) return;

        if (from != null) PublishStateExit(from);
        if (to != null) PublishStateEnter(to);
        PublishStateChange(from, to);
    }

    private void PublishStateEnter(EntityState<T> state)
    {
        if (Entity == null) return;
        Entity.PublishEvent(new EntityStateEnterEvent(
            Entity.GetInstanceID(), Entity.name, state.StateId));
    }

    private void PublishStateExit(EntityState<T> state)
    {
        if (Entity == null) return;
        Entity.PublishEvent(new EntityStateExitEvent(
            Entity.GetInstanceID(), Entity.name, state.StateId));
    }

    private void PublishStateChange(EntityState<T> from, EntityState<T> to)
    {
        if (Entity == null) return;
        Entity.PublishEvent(new EntityStateChangeEvent(
            Entity.GetInstanceID(), Entity.name,
            from != null ? from.StateId : "NULL",
            to != null ? to.StateId : "NULL"));
    }
}

using System;
using System.Collections.Generic;

/// <summary>
/// 框架层通用状态机（纯 C#，不依赖 UnityEngine，可迁移到任意 Unity 项目）。
/// 由外部驱动 LogicUpdate / PhysicsUpdate，传入 deltaTime。
///
/// ═══ Change 重载设计说明 ═══
///
/// 三个 Change 方法形成"漏斗"调用链，所有入口最终汇聚到同一个核心方法：
///
///   Change(int index)      ──→ Change(State)   按索引切换，适合顺序状态列表
///   Change&lt;TState&gt;()       ──→ Change(State)   按类型切换，最常用，编译期类型安全
///   Change(State to)                            核心实现，统一出口
///
/// 这是 Facade 模式：多入口、单出口、一处维护。
/// 不管从哪个入口进来，Exit → 记录 Previous → Enter 的流程只写一次。
/// </summary>
public class StateMachine<TOwner> where TOwner : class {
    private readonly List<State<TOwner>> _states = new List<State<TOwner>>();
    private readonly Dictionary<Type, State<TOwner>> _stateMap = new Dictionary<Type, State<TOwner>>();

    private TOwner _owner;
    private bool _isStarted;

    public State<TOwner> Current { get; private set; }
    public State<TOwner> Previous { get; private set; }
    public int StateCount => _states.Count;

    /// <summary>
    /// 初始化状态机：注入宿主和状态列表。
    /// 列表中第一个状态将作为默认初始状态。
    /// </summary>
    public void Initialize(TOwner owner, List<State<TOwner>> states) {
        _owner = owner;
        _isStarted = false;
        _states.Clear();
        _stateMap.Clear();

        if (states == null) return;

        foreach (var state in states) {
            if (state == null) continue;
            _states.Add(state);
            var type = state.GetType();
            if (!_stateMap.ContainsKey(type)) {
                _stateMap.Add(type, state);
            }
        }
    }

    /// <summary>启动状态机，进入第一个状态。</summary>
    public void Start() {
        if (_isStarted || _states.Count == 0) return;
        _isStarted = true;
        Current = _states[0];
        Current.Enter(_owner);
    }

    // ─── Change 入口 ───

    /// <summary>按列表索引切换状态。</summary>
    public void Change(int index) {
        if (index >= 0 && index < _states.Count) {
            Change(_states[index]);
        }
    }

    /// <summary>按泛型类型切换状态（最常用，编译期类型安全）。</summary>
    public void Change<TState>() where TState : State<TOwner> {
        if (_stateMap.TryGetValue(typeof(TState), out var state)) {
            Change(state);
        }
    }

    /// <summary>
    /// 核心切换逻辑（所有重载最终汇聚于此）。
    /// 流程：当前状态 Exit → 记录 Previous → 新状态 Enter。
    /// 同状态重复切换会被忽略（防止无意义的 Exit/Enter 循环）。
    /// </summary>
    public void Change(State<TOwner> to) {
        if (to == null || ReferenceEquals(Current, to)) return;

        Current?.Exit(_owner);
        Previous = Current;
        Current = to;
        Current.Enter(_owner);
    }

    // ─── 轮询驱动（deltaTime 由外部传入） ───

    /// <summary>逻辑帧更新（由外部 Update 调用，传入 Time.deltaTime）。</summary>
    public void LogicUpdate(float deltaTime) {
        Current?.LogicUpdate(_owner, deltaTime);
    }

    /// <summary>物理帧更新（由外部 FixedUpdate 调用，传入 Time.fixedDeltaTime）。</summary>
    public void PhysicsUpdate(float fixedDeltaTime) {
        Current?.PhysicsUpdate(_owner, fixedDeltaTime);
    }

    // ─── 查询 ───

    public bool IsCurrentOfType<TState>() where TState : State<TOwner> {
        return Current != null && Current.GetType() == typeof(TState);
    }

    public TState GetState<TState>() where TState : State<TOwner> {
        return _stateMap.TryGetValue(typeof(TState), out var state) ? (TState)state : null;
    }

    public bool ContainsState<TState>() where TState : State<TOwner> {
        return _stateMap.ContainsKey(typeof(TState));
    }
}

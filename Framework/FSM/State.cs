/// <summary>
/// 框架层通用状态基类（纯 C#，不依赖 UnityEngine，可迁移到任意 Unity 项目）。
/// TOwner: 持有此状态的宿主类型（Entity、UI面板、AI等均可）。
///
/// 分离 LogicUpdate 与 PhysicsUpdate：
/// - LogicUpdate(deltaTime)  → 对应 Update，处理输入响应、状态切换判断、计时器。
/// - PhysicsUpdate(fixedDeltaTime) → 对应 FixedUpdate，处理刚体力、射线检测。
///
/// deltaTime 由外部传入（而非直接读取 Time.deltaTime），
/// 这样框架层完全不引用 UnityEngine，可以迁移到任意项目。
///
/// 状态生命周期：Enter → (LogicUpdate / PhysicsUpdate 交替) → Exit
/// </summary>
public abstract class State<TOwner> where TOwner : class {
    /// <summary>进入当前状态后经过的时间（秒），在 LogicUpdate 中累加。</summary>
    public float TimeSinceEntered { get; private set; }

    /// <summary>状态标识，默认返回类名，子类可覆写。</summary>
    public virtual string StateId => GetType().Name;

    // ─── 生命周期驱动（由 StateMachine 调用） ───

    public void Enter(TOwner owner) {
        TimeSinceEntered = 0f;
        OnEnter(owner);
    }

    public void Exit(TOwner owner) {
        OnExit(owner);
    }

    public void LogicUpdate(TOwner owner, float deltaTime) {
        OnLogicUpdate(owner);
        TimeSinceEntered += deltaTime;
    }

    public void PhysicsUpdate(TOwner owner, float fixedDeltaTime) {
        OnPhysicsUpdate(owner);
    }

    // ─── 子类实现 ───

    protected abstract void OnEnter(TOwner owner);
    protected abstract void OnExit(TOwner owner);
    protected abstract void OnLogicUpdate(TOwner owner);

    /// <summary>物理帧更新，默认空实现（不是所有状态都需要物理逻辑）。</summary>
    protected virtual void OnPhysicsUpdate(TOwner owner) { }
}

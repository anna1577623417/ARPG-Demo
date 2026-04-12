using UnityEngine;

/// <summary>
/// 行为执行器（挂载在 Entity 上的 MonoBehaviour）。
///
/// ═══ 核心职责 ═══
///
/// 唯一的行为执行入口。所有行为必须通过 PlayAction() 发起：
///   State（决策层）→ ActionRunner.PlayAction(data) → ActionRuntime（时间轴）→ Entity（物理执行）
///
/// 不允许：
///   - State 直接操作位移/无敌/动画
///   - 绕过 ActionRunner 直接创建 ActionRuntime
///
/// ═══ 每帧职责 ═══
///
/// 1. 推进 ActionRuntime 时间轴
/// 2. 读取窗口边缘事件 → 发布到 EventBus（动画/VFX/Hit 系统监听）
/// 3. 根据 ActionData 配置执行：位移/无敌/旋转锁定
/// 4. 行为完成时发布 ActionEndEvent
///
/// ═══ 打断机制 ═══
///
/// PlayAction 时检查当前行为：
///   - 无当前行为 → 直接执行
///   - 当前行为可被打断 && 新行为优先级 ≥ 当前 → 打断当前，执行新行为
///   - 否则 → 拒绝（返回 false）
/// </summary>
public class ActionRunner : MonoBehaviour
{
    private Entity _entity;
    private ActionRuntime _current;

    // ─── 公开查询 ───

    /// <summary>是否正在执行行为。</summary>
    public bool IsPlaying => _current != null && !_current.IsFinished;

    /// <summary>当前行为数据（可能为 null）。</summary>
    public ActionData CurrentData => _current?.Data;

    /// <summary>当前行为归一化进度。</summary>
    public float NormalizedTime => _current?.NormalizedTime ?? 0f;

    /// <summary>当前行为是否允许输入移动。</summary>
    public bool AllowsInputMove => _current != null && _current.Data.allowInputMove;

    /// <summary>当前行为的输入移动衰减系数。</summary>
    public float InputMoveMultiplier => _current?.Data.inputMoveMultiplier ?? 1f;

    /// <summary>当前行为是否锁定旋转。</summary>
    public bool IsRotationLocked => _current != null && _current.Data.lockRotation;

    // ─── 初始化 ───

    private void Awake()
    {
        _entity = GetComponent<Entity>();
    }

    // ─── 执行入口 ───

    /// <summary>
    /// 尝试执行一个行为。
    /// </summary>
    /// <param name="data">行为数据（ScriptableObject 资产）</param>
    /// <param name="forwardDirection">行为方向（ForwardLocked 模式下锁定此方向）</param>
    /// <returns>true = 成功发起行为，false = 被拒绝（当前行为不可打断或优先级不足）</returns>
    public bool PlayAction(ActionData data, Vector3 forwardDirection)
    {
        if (data == null) return false;

        // 打断检查
        if (IsPlaying)
        {
            if (!_current.CanBeInterrupted) return false;
            if (data.priority < _current.Data.priority) return false;

            // 打断当前行为
            EndCurrent(interrupted: true);
        }

        // 创建新的运行时实例
        _current = new ActionRuntime(data, forwardDirection);

        // 进入时朝向
        if (data.snapRotationOnEnter && _entity != null)
        {
            _entity.LookAtDirection(forwardDirection, immediate: true);
        }

        // 发布开始事件
        PublishToEntity(new ActionStartEvent(data.actionId, GetEntityInstanceId()));

        // 动画驱动（通过 EntityStateEnterEvent，复用现有动画系统）
        if (data.clip != null && _entity != null)
        {
            _entity.PublishEvent(new EntityStateEnterEvent(
                GetEntityInstanceId(), GetEntityName(), data.actionId));
        }

        return true;
    }

    /// <summary>
    /// 强制停止当前行为。
    /// </summary>
    public void StopAction()
    {
        if (IsPlaying)
        {
            EndCurrent(interrupted: true);
        }
    }

    // ─── 每帧驱动（由外部调用，如 State 的 OnLogicUpdate）───

    /// <summary>
    /// 推进当前行为的时间轴。State 在 OnLogicUpdate 中调用。
    /// 返回本帧的 TickResult，State 可据此做额外决策。
    /// </summary>
    public TickResult Tick(float deltaTime)
    {
        if (_current == null || _current.IsFinished)
            return default;

        var result = _current.Tick(deltaTime);

        // 处理窗口边缘事件
        int id = GetEntityInstanceId();

        if (result.IFrameEntered && _entity != null)
        {
            var player = _entity as Player;
            player?.SetInvincible(true);
        }

        if (result.IFrameExited && _entity != null)
        {
            var player = _entity as Player;
            player?.SetInvincible(false);
        }

        if (result.HitFrameEntered)
        {
            PublishToEntity(new ActionHitFrameStartEvent(_current.Data.actionId, id));
        }

        if (result.HitFrameExited)
        {
            PublishToEntity(new ActionHitFrameEndEvent(_current.Data.actionId, id));
        }

        if (result.Finished)
        {
            EndCurrent(interrupted: false);
        }

        return result;
    }

    /// <summary>
    /// 获取当前行为的位移向量（供 State 调用 ApplyMotor 时使用）。
    /// </summary>
    public Vector3 GetMoveVelocity()
    {
        if (_current == null || !_current.Data.HasMovement)
            return Vector3.zero;

        var dir = _current.Data.moveMode switch
        {
            ActionMoveMode.ForwardLocked => _current.LockedDirection,
            ActionMoveMode.FollowInput => _entity != null ? _entity.MoveDirection : Vector3.forward,
            _ => Vector3.zero
        };

        return dir * (_current.Data.moveSpeed * _current.GetMoveSpeedFactor());
    }

    // ─── 内部 ───

    private void EndCurrent(bool interrupted)
    {
        if (_current == null) return;

        var data = _current.Data;
        var id = GetEntityInstanceId();

        // 确保无敌关闭
        if (data.HasIFrame && _entity != null)
        {
            var player = _entity as Player;
            player?.SetInvincible(false);
        }

        PublishToEntity(new ActionEndEvent(data.actionId, id, interrupted));
        _current = null;
    }

    private void PublishToEntity(IGameEvent evt)
    {
        _entity?.PublishEvent(evt);
    }

    private int GetEntityInstanceId() => _entity != null ? _entity.GetInstanceID() : 0;
    private string GetEntityName() => _entity != null ? _entity.name : "";
}

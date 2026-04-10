using UnityEngine;

/// <summary>
/// 实体控制器基类。
///
/// Controller 层的职责：将"原始输入/AI决策"翻译为"语义化意图"，写入 Entity。
/// 具体子类（PlayerController、AIController）各自实现意图生成逻辑。
///
/// ═══ 分层关系 ═══
///
/// EntityController（基类）
///   ├── PlayerController — 读取 InputReader + 相机，生成玩家意图
///   ├── AIController — 读取行为树/FSM，生成 AI 意图（预留）
///   └── ReplayController — 读取录像数据，回放意图（预留）
///
/// Controller 不直接操作状态机。意图写入 Entity 后，由 StateMachine 消费。
/// </summary>
public abstract class EntityController : MonoBehaviour
{
    /// <summary>控制器是否激活（可被外部系统临时禁用，如过场动画）。</summary>
    public bool IsActive { get; set; } = true;
}

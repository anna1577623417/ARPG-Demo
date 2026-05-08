/// <summary>
/// 玩家状态 HUD 数据快照。v4.3 升级：
/// 旧字段（HealthCurrent / HealthMax）保留为兼容；新字段 <see cref="Player"/> 用于
/// HUD 内部 Presenter 绑定（事件订阅模式，推荐）。
///
/// 业务侧两种用法（向后兼容）：
///   ① 旧用法：上层每帧重算 HealthCurrent / HealthMax 推 props（轮询）
///   ② 新用法：直接传 Player 引用，HUD 内 Presenter 订阅 OnCurrentChanged 事件（推荐）
/// </summary>
public struct PlayerStatusHudProps
{
    /// <summary>v4.3 推荐：传入 Player，HUD 内 Presenter 订阅 Resources/Stats 事件。</summary>
    public Player Player;

    // ── 兼容字段（旧 HUD 调用方仍可使用文本快照）──
    public float HealthCurrent;
    public float HealthMax;
}

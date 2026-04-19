/// <summary>
/// 离散输入经“语义化”后的意图种类（不要用字符串状态名做分支）。
/// </summary>
public enum GameplayIntentKind : byte
{
    None = 0,
    Jump = 1,
    LightAttack = 2,
    HeavyAttack = 3,
    Dodge = 4,
    /// <summary>直线爆发位移（原 Shift「冲刺」语义，走 ActionDataSO，非连续 Run）。</summary>
    SwordDash = 5,

    /// <summary>
    /// 蓄力型攻击（独立 ActionDataSO，走 <see cref="WeaponMovesetSO.ChargedAttacks"/>；
    /// 与 <see cref="LightAttack"/> 同源主攻击键，由 <see cref="PrimaryAttackPressTracker"/> 长按阈值派发）。
    /// </summary>
    ChargedAttack = 6,
}

/// <summary>
/// 一条带时间戳的语义意图。
/// - <see cref="TimeStamp"/>：入队时间，用于调试与优先级扩展。
/// - <see cref="ExpireTime"/>：过期时间；过期后由缓冲队列丢弃，实现输入缓冲窗口。
/// - 标签字段：由工厂在入队时填好，供 <see cref="TransitionResolver"/> 做“只查标签”的过滤。
/// </summary>
public struct GameplayIntent
{
    public GameplayIntentKind Kind;

    /// <summary>入队时的 Time.time。</summary>
    public float TimeStamp;

    /// <summary>超过该 Time.time 后意图作废。</summary>
    public float ExpireTime;

    /// <summary>上下文中必须全部具备的标签（ulong 位）。</summary>
    public ulong RequiredAllTags;

    /// <summary>若非 0：上下文中至少命中其中一位。</summary>
    public ulong RequiredAnyTags;

    /// <summary>若上下文中命中任意禁止位，则意图非法。</summary>
    public ulong ForbiddenTags;

    /// <summary>可选：注入到 Action 支柱的动作资产（可为 null，走代码内默认动作）。</summary>
    public ActionDataSO Action;

    public static GameplayIntent Create(
        GameplayIntentKind kind,
        float time,
        float bufferSeconds,
        ulong requiredAll,
        ulong requiredAny,
        ulong forbidden,
        ActionDataSO action = null)
    {
        return new GameplayIntent
        {
            Kind = kind,
            TimeStamp = time,
            ExpireTime = time + bufferSeconds,
            RequiredAllTags = requiredAll,
            RequiredAnyTags = requiredAny,
            ForbiddenTags = forbidden,
            Action = action,
        };
    }
}

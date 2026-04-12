using UnityEngine;

/// <summary>
/// 行为数据定义（ScriptableObject 资产）。
///
/// Action = 一个被时间驱动的、可复用的行为描述单元。
/// 所有行为参数由数据定义，设计师可在 Inspector 中调整，无需改代码。
///
/// ═══ 设计原则 ═══
///
/// Action 只回答「这个行为长什么样」：
///   - 持续多久？
///   - 位移曲线？
///   - 什么时候无敌？
///   - 什么时候出伤害判定？
///   - 什么时候可以被打断？
///   - 动画播什么？
///
/// Action 不关心「能不能做」—— 那是 State 的决策。
/// Action 不关心「怎么执行」—— 那是 ActionRunner 的职责。
///
/// ═══ 时间轴模型 ═══
///
/// 所有逻辑基于归一化时间 t ∈ [0, 1]：
///   t = currentTime / duration
///
///   0                                                    1
///   ├── moveStart ────── moveEnd ─────────────────────────┤  位移窗口
///   ├────── iFrameStart ──── iFrameEnd ──────────────────┤  无敌窗口
///   ├──────────── hitFrameStart ─ hitFrameEnd ───────────┤  伤害窗口
///   ├── interruptLockEnd ────────────────────────────────┤  打断锁定
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Action/Action Data")]
public class ActionData : ScriptableObject
{
    [Header("Basic")]
    [Tooltip("行为唯一标识（用于事件发布和动画匹配）")]
    public string actionId;

    [Tooltip("行为总时长（秒）")]
    public float duration = 0.5f;

    [Tooltip("行为优先级。高优先级行为可打断低优先级行为。\n参考值：Hit=100, Dodge=80, Attack=50, Skill=60")]
    public int priority = 50;

    // ─── 位移 ───

    [Header("Movement")]
    [Tooltip("位移速度（单位/秒）。0 = 无位移（如站桩攻击）")]
    public float moveSpeed;

    [Tooltip("位移速度曲线。横轴=归一化时间，纵轴=速度系数。")]
    public AnimationCurve moveSpeedCurve = AnimationCurve.Constant(0f, 1f, 1f);

    [Tooltip("位移方向模式")]
    public ActionMoveMode moveMode = ActionMoveMode.ForwardLocked;

    [Tooltip("行为期间是否允许输入控制移动（如攻击中微移）")]
    public bool allowInputMove;

    [Tooltip("允许输入移动时的速度衰减系数 (0~1)")]
    [Range(0f, 1f)]
    public float inputMoveMultiplier = 0.25f;

    // ─── 无敌帧 ───

    [Header("Invincibility")]
    [Tooltip("无敌帧开始（归一化 0~1）")]
    [Range(0f, 1f)]
    public float iFrameStart;

    [Tooltip("无敌帧结束（归一化 0~1）")]
    [Range(0f, 1f)]
    public float iFrameEnd;

    // ─── 伤害判定 ───

    [Header("Hit Detection")]
    [Tooltip("伤害判定开始（归一化 0~1）")]
    [Range(0f, 1f)]
    public float hitFrameStart;

    [Tooltip("伤害判定结束（归一化 0~1）")]
    [Range(0f, 1f)]
    public float hitFrameEnd;

    // ─── 打断控制 ───

    [Header("Interrupt")]
    [Tooltip("是否可被更高优先级行为打断")]
    public bool canBeInterrupted = true;

    [Tooltip("打断锁定结束时刻（归一化 0~1）。\n在此之前即使 canBeInterrupted=true 也不可被打断。\n用于保护前摇/关键帧。")]
    [Range(0f, 1f)]
    public float interruptLockEnd;

    // ─── 旋转 ───

    [Header("Rotation")]
    [Tooltip("进入时是否立即朝向移动方向")]
    public bool snapRotationOnEnter = true;

    [Tooltip("行为期间是否锁定朝向（不跟随输入旋转）")]
    public bool lockRotation = true;

    // ─── 动画 ───

    [Header("Animation")]
    [Tooltip("动画片段（可选，为空则不驱动动画）")]
    public AnimationClip clip;

    [Tooltip("动画过渡时长（秒）")]
    [Range(0f, 0.5f)]
    public float animTransition = 0.05f;

    [Tooltip("动画播放速度")]
    [Range(0.1f, 5f)]
    public float animSpeed = 1f;

    // ─── 便捷查询 ───

    public bool HasIFrame => iFrameEnd > iFrameStart;
    public bool HasHitFrame => hitFrameEnd > hitFrameStart;
    public bool HasMovement => moveSpeed > 0f;
}

/// <summary>
/// 位移方向模式。
/// </summary>
public enum ActionMoveMode
{
    /// <summary>进入时锁定方向，执行中不变（闪避/冲刺）</summary>
    ForwardLocked,
    /// <summary>持续跟随输入方向（空中移动）</summary>
    FollowInput,
    /// <summary>无自身位移（站桩攻击），但可通过 allowInputMove 微移</summary>
    None
}

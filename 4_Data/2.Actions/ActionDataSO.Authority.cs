using UnityEngine;

/// <summary>
/// 187.3 W4 — ActionDataSO 的 Motion Authority 扩展 partial。
/// <para>策划编辑：每个 Action 声明持有 Move / Rotate 通道的权威 + 锁定行为 + 可选 Priority 覆盖。</para>
/// </summary>
public partial class ActionDataSO
{
    [Header("Motion Authority (187.3)")]

    [Tooltip("此 Action 在 Move 通道上希望持有的权威。\n" +
             "  · Action（默认）= 战斗 Action 持有，玩家移动输入按 MoveInputWeight 曲线\n" +
             "  · Recovery = WalkEnd/RunEnd 等过渡，任何 Intent 都能抢占\n" +
             "  · Cinematic = 剧情 / QTE 接管")]
    public AuthorityHolder MoveAuthority = AuthorityHolder.Action;

    [Tooltip("此 Action 在 Rotate 通道上希望持有的权威。\n" +
             "  · Action（默认）= 战斗 Action 持有，玩家朝向输入按 FacingInputWeight 曲线\n" +
             "  · Recovery = WalkEnd/Turn 等过渡，玩家朝向可立即响应")]
    public AuthorityHolder RotateAuthority = AuthorityHolder.Action;

    [Tooltip("动作期间禁玩家移动输入。\n" +
             "  · true（默认）= 魂系手感，攻击中不可平移\n" +
             "  · false = DMC/ZZZ 手感，攻击中允许 WASD 影响位移（与 174.2 MoveInputWeight 曲线协同）")]
    public bool LockMoveDuringPlay = true;

    [Tooltip("动作期间锁朝向。\n" +
             "  · true（默认）= 魂系手感，攻击中不可转向\n" +
             "  · false = ACT 手感，可微转（与 174.2 FacingInputWeight 曲线协同）")]
    public bool LockRotateDuringPlay = true;

    [Tooltip("覆盖默认 Priority。-1=按 AuthorityPriorityTable.ForAction 推算。\n" +
             "Cinematic=0 / ActionLocked=10 / ActionUnlocked=20 / StanceTransition=30 / Recovery=40 / Locomotion=100")]
    public int AuthorityPriorityOverride = -1;
}

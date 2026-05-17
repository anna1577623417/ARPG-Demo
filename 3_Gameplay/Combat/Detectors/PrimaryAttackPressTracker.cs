using UnityEngine;

/// <summary>
/// 主攻击键边沿（Ver4.3.7+ Semantic Resolver 接入）：
///
/// ═══ 与 Ver4.3.6 的差异 ═══
///   旧实现自己计算 hold 并直接 enqueue raw Intent；
///   新实现将"按下/松开/持续按住"三类原始边沿全部转发给 <see cref="InputSemanticResolver"/>，
///   由 Resolver 单点决定派发什么语义（Charge 在按下 ≥ TapThreshold 瞬间派发；
///   Tap / Combo 在松手时根据窗口分流）。
///
/// 本类**不再决策**输入语义；只是 raw edge 的搬运工。
/// </summary>
public sealed class PrimaryAttackPressTracker
{
    const SkillEntrySlot Slot = SkillEntrySlot.LM;

    bool _lastHeld;

    public void SyncInitialHeldState(bool attackHeld)
    {
        _lastHeld = attackHeld;
    }

    public void Tick(float time, bool attackHeld, Player player)
    {
        if (player?.InputSemantic == null) return;

        var rose = attackHeld && !_lastHeld;
        var fell = !attackHeld && _lastHeld;
        _lastHeld = attackHeld;

        if (rose)
        {
            player.InputSemantic.OnPressEdge(Slot, time);
        }

        // 持续按住期间每帧 Tick，让 Resolver 检测是否越过 TapThreshold 即派发 Charge intent。
        if (attackHeld)
        {
            player.InputSemantic.OnHoldTick(Slot, time);
        }

        if (fell)
        {
            player.InputSemantic.OnReleaseEdge(Slot, time);
        }
    }
}

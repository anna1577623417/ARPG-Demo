using UnityEngine;

/// <summary>
/// 220.6.1 C5：命中 VFX/SFX 的 PresentChannel。
/// <para>Combat 只发布载荷键和因果 EventId；具体资源播放器由表现层订阅事件实现。</para>
/// </summary>
public static class ReactionPresentChannel
{
    public static void Present(
        Entity target,
        in HitReaction reaction,
        ulong eventId)
    {
        if (target == null)
        {
            return;
        }

        var hasVfx = !string.IsNullOrWhiteSpace(reaction.VfxPayload);
        var hasSfx = !string.IsNullOrWhiteSpace(reaction.SfxPayload);
        if (!hasVfx && !hasSfx)
        {
            return;
        }

        target.PublishEvent(new EntityReactionPresentationEvent(
            target.GetInstanceID(),
            eventId,
            reaction.VfxPayload,
            reaction.SfxPayload));

        if (GameMainDebugSettings.CombatHit
            || GameMainDebugSettings.ReactionDirection2206Log)
        {
            Debug.Log(
                $"[Feedback] Present eventId={eventId} target={target.name} " +
                $"vfx={reaction.VfxPayload ?? "-"} sfx={reaction.SfxPayload ?? "-"} log=220.6",
                target);
        }
    }
}

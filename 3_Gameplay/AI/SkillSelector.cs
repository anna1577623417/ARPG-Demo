using UnityEngine;

/// <summary>
/// 220.7 D4：AI 只通过入口槽位选择可释放的 GameplayIntent。
/// <para>不解析 Animator、不持有 ActionData，也不直接提交 IntentHost。</para>
/// </summary>
public sealed class SkillSelector
{
    public bool TryPick(
        ISkillHost host,
        IBlackboardReader blackboard,
        SkillEntrySlot entrySlot,
        float meleeRange,
        bool aggressive,
        float now,
        out GameplayIntent intent,
        out string reason)
    {
        intent = default;
        reason = null;
        if (host == null || host.Entity == null)
        {
            reason = "missing-host";
            return false;
        }

        if (!aggressive)
        {
            reason = "passive";
            return false;
        }

        if (!blackboard.TryGet(AiBlackboardKeys.CurrentTarget, out Entity target)
            || target == null)
        {
            reason = "no-target";
            return false;
        }

        var distance = Vector3.ProjectOnPlane(
            target.Position - host.Entity.Position,
            Vector3.up).magnitude;
        if (distance > Mathf.Max(0f, meleeRange))
        {
            reason = "range";
            return false;
        }

        var entry = host.SkillEntryLoadout?.Resolve(entrySlot);
        if (entry == null)
        {
            reason = "empty";
            return false;
        }

        var route = entry.NormalRoute ?? entry.PrimaryRoute;
        if (route == null)
        {
            reason = "empty-route";
            return false;
        }

        if (host.Entity is Enemy enemy
            && (enemy.SkillEntries == null
                || !enemy.SkillEntries.TryGetRuntime(route, out var runtime)
                || runtime == null))
        {
            reason = "runtime-missing";
            return false;
        }

        if (host.Entity is Enemy enemyWithRuntime
            && enemyWithRuntime.SkillEntries.TryGetRuntime(route, out var routeRuntime)
            && routeRuntime.CdRemainingSeconds > 0.0001f)
        {
            reason = "cd";
            return false;
        }

        intent = GameplayIntent.ForEntry(
            entrySlot,
            now,
            bufferSeconds: 0.25f,
            requiredAll: 0UL,
            requiredAny: 0UL,
            forbidden: 0UL);
        intent.Semantic = InputSemanticType.Tap;
        return true;
    }
}

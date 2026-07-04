using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SkillGroup 共享 CD 注册表 — 从 SkillEntryService 拆出（208.3 L3）。
/// OnLastSubRouteEnd / Route OnExit 经 <see cref="SkillRouteRuntime"/> 调用 TryApply；Tick 驱动成员 Route CD 镜像。
/// </summary>
internal sealed class GroupCooldownRegistry
{
    readonly IGroupCooldownHost _host;
    readonly Dictionary<SkillGroupDefinition, GroupCooldownState> _states
        = new Dictionary<SkillGroupDefinition, GroupCooldownState>(8);

    internal GroupCooldownRegistry(IGroupCooldownHost host) => _host = host;

    internal void Tick(float dt)
    {
        if (_states.Count == 0)
        {
            return;
        }

        var keys = new List<SkillGroupDefinition>(_states.Keys);
        for (var i = 0; i < keys.Count; i++)
        {
            var group = keys[i];
            if (!_states.TryGetValue(group, out var state))
            {
                continue;
            }

            state.RemainingSeconds = Mathf.Max(0f, state.RemainingSeconds - dt);
            _states[group] = state;
            if (state.RemainingSeconds <= 0.0001f)
            {
                _states.Remove(group);
            }
            else
            {
                SyncMemberCooldowns(group, state.RemainingSeconds, state.TotalSeconds);
            }
        }
    }

    internal bool TryGetState(
        SkillGroupDefinition group,
        out float remainingSeconds,
        out float totalSeconds)
    {
        if (group != null && _states.TryGetValue(group, out var state))
        {
            remainingSeconds = state.RemainingSeconds;
            totalSeconds = state.TotalSeconds;
            return true;
        }

        remainingSeconds = 0f;
        totalSeconds = 0f;
        return false;
    }

    internal bool IsRouteBlocked(SkillRouteDefinition route)
    {
        if (route == null || route.OwnerGroup == null || route.OverrideGroupCooldown)
        {
            return false;
        }

        return _states.TryGetValue(route.OwnerGroup, out var state)
               && state.RemainingSeconds > 0.0001f;
    }

    internal bool TryApply(SkillRouteDefinition route, in SkillRouteContext ctx)
    {
        var group = route?.OwnerGroup;
        if (group == null || route.OverrideGroupCooldown)
        {
            return false;
        }

        var cd = group.CooldownSeconds;
        var stats = ctx.Stats;
        if (stats != null)
        {
            var cdr = Mathf.Clamp(stats.Get(StatType.CooldownReduction), 0f, 0.4f);
            cd = Mathf.Max(0f, cd * (1f - cdr));
        }

        _states[group] = new GroupCooldownState(cd, cd);
        SyncMemberCooldowns(group, cd);
        SkillRouteDebug.Log(
            _host.Owner,
            SkillRouteDebug.CatUnit,
            $"Group CD start group={group.name} cd={cd:F2}s via route={route.name}");
        return true;
    }

    void SyncMemberCooldowns(SkillGroupDefinition group, float remaining, float total = -1f)
    {
        if (group?.Routes == null)
        {
            return;
        }

        for (var i = 0; i < group.Routes.Count; i++)
        {
            var member = group.Routes[i];
            if (member == null || !_host.TryGetRouteRuntime(member, out var rt))
            {
                continue;
            }

            rt.CdRemainingSeconds = remaining;
            if (total >= 0f)
            {
                rt.CdScaledTotalSeconds = total;
            }
        }

        if (group.FallbackRoute != null
            && _host.TryGetRouteRuntime(group.FallbackRoute, out var fb))
        {
            fb.CdRemainingSeconds = remaining;
            if (total >= 0f)
            {
                fb.CdScaledTotalSeconds = total;
            }
        }
    }

    struct GroupCooldownState
    {
        public float RemainingSeconds;
        public float TotalSeconds;

        public GroupCooldownState(float remaining, float total)
        {
            RemainingSeconds = remaining;
            TotalSeconds = total;
        }
    }
}

/// <summary>GroupCooldownRegistry 向 SkillEntryService 索取的宿主能力。</summary>
internal interface IGroupCooldownHost
{
    Player Owner { get; }
    bool TryGetRouteRuntime(SkillRouteDefinition def, out SkillRouteRuntime rt);
}

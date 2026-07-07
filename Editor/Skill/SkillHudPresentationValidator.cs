#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 211.3 / 212-2 — Loadout HUD 展示配置校验（仅 Warning，不写 .asset）。
/// </summary>
public static class SkillHudPresentationValidator
{
    public sealed class ValidationResult
    {
        public readonly List<string> PresentationWarnings = new List<string>(8);
        public readonly List<string> HudLayoutWarnings = new List<string>(8);
        public readonly List<string> RouteGroupWarnings = new List<string>(8);

        public int TotalWarnings =>
            PresentationWarnings.Count + HudLayoutWarnings.Count + RouteGroupWarnings.Count;
    }

    public static ValidationResult Validate(SkillEntryLoadoutSO loadout, int maxWidgetsHint = 16)
    {
        var result = new ValidationResult();
        if (loadout == null)
        {
            result.PresentationWarnings.Add("Loadout is null.");
            return result;
        }

        var presentationIds = new HashSet<string>();
        var groupHandlesPerSlot = new Dictionary<SkillEntrySlot, int>();
        var hudHandleCount = 0;

        var bindings = loadout.Bindings;
        if (bindings != null)
        {
            for (var i = 0; i < bindings.Length; i++)
            {
                var entry = bindings[i].Entry;
                if (entry == null)
                {
                    continue;
                }

                ValidateEntry(entry, bindings[i].Slot, bindings[i].HudKeyLabel, loadout, result, presentationIds, ref hudHandleCount);
            }
        }

        var ctxGroups = loadout.ContextGroups;
        if (ctxGroups != null)
        {
            for (var i = 0; i < ctxGroups.Length; i++)
            {
                var group = ctxGroups[i]?.TargetGroup;
                if (group == null)
                {
                    continue;
                }

                var slot = ctxGroups[i].RequiredSlot;
                if (group.IsHudVisible())
                {
                    hudHandleCount++;
                    if (slot != SkillEntrySlot.Any)
                    {
                        IncrementSlotCount(groupHandlesPerSlot, slot);
                    }
                }

                ValidateGroup(group, result, presentationIds);
            }
        }

        foreach (var kv in groupHandlesPerSlot)
        {
            if (kv.Value > 1)
            {
                result.HudLayoutWarnings.Add(
                    $"同 EntrySlot 多个 Group HUD：slot={kv.Key} count={kv.Value}（例：Shift 双 Group）");
            }
        }

        if (hudHandleCount > maxWidgetsHint)
        {
            result.HudLayoutWarnings.Add(
                $"HudHandles 数 {hudHandleCount} > Presenter.maxWidgets 提示值 {maxWidgetsHint}");
        }

        return result;
    }

    static void ValidateEntry(
        SkillEntryDefinition entry,
        SkillEntrySlot slot,
        string keyLabel,
        SkillEntryLoadoutSO loadout,
        ValidationResult result,
        HashSet<string> presentationIds,
        ref int hudHandleCount)
    {
        if (entry.ChargeRoute != null)
        {
            ValidateRoute(entry.ChargeRoute, entry, slot, result, presentationIds, ref hudHandleCount);
        }

        if (entry.ComboRoute != null)
        {
            ValidateRoute(entry.ComboRoute, entry, slot, result, presentationIds, ref hudHandleCount);
        }

        if (entry.ExtendedComboRoute != null)
        {
            ValidateRoute(entry.ExtendedComboRoute, entry, slot, result, presentationIds, ref hudHandleCount);
        }

        if (entry.AirComboRoute != null)
        {
            ValidateRoute(entry.AirComboRoute, entry, slot, result, presentationIds, ref hudHandleCount);
        }

        if (entry.MultiStageRoute != null)
        {
            ValidateRoute(entry.MultiStageRoute, entry, slot, result, presentationIds, ref hudHandleCount);
        }

        if (entry.NormalRoute != null)
        {
            ValidateRoute(entry.NormalRoute, entry, slot, result, presentationIds, ref hudHandleCount);
        }

        var primaryGroup = entry.PrimaryGroup;
        if (primaryGroup != null)
        {
            if (primaryGroup.IsHudVisible())
            {
                hudHandleCount++;
            }

            ValidateGroup(primaryGroup, result, presentationIds);
        }
        else if (entry.PrimaryRoute != null)
        {
            ValidateRoute(entry.PrimaryRoute, entry, slot, result, presentationIds, ref hudHandleCount);
        }
    }

    static void ValidateRoute(
        SkillRouteDefinition route,
        SkillEntryDefinition entry,
        SkillEntrySlot slot,
        ValidationResult result,
        HashSet<string> presentationIds,
        ref int hudHandleCount)
    {
        if (route.OwnerGroup != null)
        {
            if (route.IsHudVisible())
            {
                result.RouteGroupWarnings.Add(
                    $"Group 成员 showOnHud=true：{AssetDatabase.GetAssetPath(route)}（应由 Group 控制）");
            }

            if (route.OverrideGroupIcon && route.Icon != null)
            {
                result.RouteGroupWarnings.Add(
                    $"Group 成员单独填 icon：{AssetDatabase.GetAssetPath(route)}（应只用 Group.icon）");
            }

            return;
        }

        if (!route.IsHudVisible())
        {
            return;
        }

        hudHandleCount++;
        WarnPresentationIdentity(route, entry, result, presentationIds);

        if (route.GetEffectiveIcon() == null && entry.FallbackIcon == null)
        {
            result.PresentationWarnings.Add(
                $"HUD 可见 Route icon 空且无 entry.fallback：{AssetDatabase.GetAssetPath(route)} slot={slot}");
        }

        if (string.IsNullOrEmpty(route.GetEffectiveDisplayName()))
        {
            result.PresentationWarnings.Add(
                $"displayName 空：{AssetDatabase.GetAssetPath(route)} slot={slot}");
        }
    }

    static void ValidateGroup(
        SkillGroupDefinition group,
        ValidationResult result,
        HashSet<string> presentationIds)
    {
        WarnPresentationIdentity(group, null, result, presentationIds);

        if (!group.IsHudVisible())
        {
            return;
        }

        if (group.Icon == null)
        {
            result.PresentationWarnings.Add(
                $"HUD 可见 Group icon 空：{AssetDatabase.GetAssetPath(group)}");
        }

        if (string.IsNullOrEmpty(group.GetEffectiveDisplayName()))
        {
            result.PresentationWarnings.Add(
                $"displayName 空：{AssetDatabase.GetAssetPath(group)}");
        }

        var routes = group.Routes;
        if (routes == null)
        {
            return;
        }

        for (var i = 0; i < routes.Count; i++)
        {
            var r = routes[i];
            if (r == null)
            {
                continue;
            }

            if (r.IsHudVisible())
            {
                result.RouteGroupWarnings.Add(
                    $"Group 成员 showOnHud=true：{AssetDatabase.GetAssetPath(r)}");
            }

            if (r.OverrideGroupIcon && r.Icon != null)
            {
                result.RouteGroupWarnings.Add(
                    $"Group 成员单独填 icon：{AssetDatabase.GetAssetPath(r)}");
            }
        }
    }

    static void WarnPresentationIdentity(
        SkillRouteDefinition route,
        SkillEntryDefinition entry,
        ValidationResult result,
        HashSet<string> presentationIds)
    {
        var id = route.PresentationId;
        if (string.IsNullOrEmpty(id))
        {
            result.PresentationWarnings.Add(
                $"presentationId 空：{AssetDatabase.GetAssetPath(route)}");
            return;
        }

        if (!presentationIds.Add(id))
        {
            result.PresentationWarnings.Add($"presentationId 重复：{id} @ {AssetDatabase.GetAssetPath(route)}");
        }
    }

    static void WarnPresentationIdentity(
        SkillGroupDefinition group,
        SkillEntryDefinition entry,
        ValidationResult result,
        HashSet<string> presentationIds)
    {
        var id = group.PresentationId;
        if (string.IsNullOrEmpty(id))
        {
            result.PresentationWarnings.Add(
                $"presentationId 空：{AssetDatabase.GetAssetPath(group)}");
            return;
        }

        if (!presentationIds.Add(id))
        {
            result.PresentationWarnings.Add($"presentationId 重复：{id} @ {AssetDatabase.GetAssetPath(group)}");
        }
    }

    static void IncrementSlotCount(Dictionary<SkillEntrySlot, int> map, SkillEntrySlot slot)
    {
        if (!map.TryGetValue(slot, out var count))
        {
            map[slot] = 1;
            return;
        }

        map[slot] = count + 1;
    }

    public static void LogToConsole(ValidationResult result)
    {
        if (result == null)
        {
            return;
        }

        var sb = new StringBuilder(256);
        sb.AppendLine($"[HUD] Validate HUD: {result.TotalWarnings} warnings");

        AppendSection(sb, "PresentationWarnings", result.PresentationWarnings);
        AppendSection(sb, "HudLayoutWarnings", result.HudLayoutWarnings);
        AppendSection(sb, "RouteGroupWarnings", result.RouteGroupWarnings);

        Debug.LogWarning(sb.ToString());
    }

    static void AppendSection(StringBuilder sb, string title, List<string> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        sb.AppendLine($"— {title} —");
        for (var i = 0; i < lines.Count; i++)
        {
            sb.AppendLine($"  · {lines[i]}");
        }
    }
}
#endif

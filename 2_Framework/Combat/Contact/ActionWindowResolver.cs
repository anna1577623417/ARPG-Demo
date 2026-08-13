using UnityEngine;

public readonly struct ResolvedActionWindow
{
    public readonly string WindowId;
    public readonly string DisplayName;
    public readonly float NormalizedStart;
    public readonly float NormalizedEnd;
    public readonly bool UsesLegacyRange;
    public readonly string SourcePath;

    public ResolvedActionWindow(
        string windowId,
        string displayName,
        float normalizedStart,
        float normalizedEnd,
        bool usesLegacyRange,
        string sourcePath)
    {
        WindowId = windowId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        NormalizedStart = normalizedStart;
        NormalizedEnd = normalizedEnd;
        UsesLegacyRange = usesLegacyRange;
        SourcePath = sourcePath ?? string.Empty;
    }
}

public readonly struct ContactWindowResolutionInfo
{
    public readonly bool UsesLegacyRange;
    public readonly bool NeedsMigration;
    public readonly string Message;

    public ContactWindowResolutionInfo(bool usesLegacyRange, bool needsMigration, string message)
    {
        UsesLegacyRange = usesLegacyRange;
        NeedsMigration = needsMigration;
        Message = message ?? string.Empty;
    }
}

/// <summary>224.1 L4 — Contact 时间唯一解析入口。</summary>
public static class ActionWindowResolver
{
    public static bool TryResolveContactWindow(
        ActionDataSO action,
        in ContactEvent contact,
        out ResolvedActionWindow window,
        out ContactWindowResolutionInfo info)
    {
        window = default;
        info = default;
        if (action == null)
        {
            info = new ContactWindowResolutionInfo(false, false, "Action is null.");
            return false;
        }

        var useWindowId =
            action.WindowAuthoringVersion == ActionWindowAuthoringVersion.WindowIdBoundContactV1
            && ActionWindowIdentity.IsValid(contact.WindowId);

        if (useWindowId)
        {
            if (!ActionWindowIdentity.TryGet(action, contact.WindowId, out var found))
            {
                info = new ContactWindowResolutionInfo(
                    false,
                    true,
                    $"WindowId '{contact.WindowId}' is dangling; Contact blocked.");
                return false;
            }

            if (found.NormalizedStart >= found.NormalizedEnd)
            {
                info = new ContactWindowResolutionInfo(
                    false,
                    false,
                    $"Window '{contact.WindowId}' has Start>=End.");
                return false;
            }

            window = new ResolvedActionWindow(
                found.WindowId,
                found.DisplayName,
                found.NormalizedStart,
                found.NormalizedEnd,
                usesLegacyRange: false,
                sourcePath: "ActionWindow.WindowId");
            info = new ContactWindowResolutionInfo(false, false, string.Empty);
            return true;
        }

        var start = Mathf.Min(contact.ActiveStart, contact.ActiveEnd);
        var end = Mathf.Max(contact.ActiveStart, contact.ActiveEnd);
        if (end <= start)
        {
            info = new ContactWindowResolutionInfo(
                true,
                true,
                "Legacy Contact Active range invalid.");
            return false;
        }

        window = new ResolvedActionWindow(
            contact.WindowId,
            contact.DebugName,
            start,
            end,
            usesLegacyRange: true,
            sourcePath: "ContactEvent.ActiveStart/ActiveEnd");
        info = new ContactWindowResolutionInfo(
            true,
            true,
            "Contact still uses Legacy ActiveStart/ActiveEnd; migrate to WindowId.");
        return true;
    }
}

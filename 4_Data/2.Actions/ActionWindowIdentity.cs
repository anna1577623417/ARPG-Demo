using System;

/// <summary>224.1 L4 — ActionWindow / Contact 时间关系版本。</summary>
public enum ActionWindowAuthoringVersion : byte
{
    LegacyRangeOnContact = 0,
    WindowIdBoundContactV1 = 1,
}

/// <summary>WindowId 生成与查找；禁止每加载随机补 ID。</summary>
public static class ActionWindowIdentity
{
    public static string NewId() => Guid.NewGuid().ToString("N");

    public static bool IsValid(string value) =>
        !string.IsNullOrWhiteSpace(value) && Guid.TryParseExact(value, "N", out _);

    public static int FindIndex(ActionDataSO action, string windowId)
    {
        if (action?.Windows == null || !IsValid(windowId)) return -1;
        for (var i = 0; i < action.Windows.Count; i++)
        {
            if (action.Windows[i].WindowId == windowId) return i;
        }

        return -1;
    }

    public static bool TryGet(ActionDataSO action, string windowId, out ActionWindow window)
    {
        window = default;
        var index = FindIndex(action, windowId);
        if (index < 0) return false;
        window = action.Windows[index];
        return true;
    }
}

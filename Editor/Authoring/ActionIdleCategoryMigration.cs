#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>171.7 B.W3 — 未迁移 Idle 资产 Editor 警告（不改运行时）。</summary>
static class ActionIdleCategoryMigration
{
    const string PrefsKeyPrefix = "ActionIdleCategoryMigration.Warned.";

    public static void WarnIfUnmigratedIdle(ActionDataSO action)
    {
        if (action == null
            || action.Category == ActionCategory.IdleFallback
            || !LooksLikeIdleFallbackAsset(action))
        {
            return;
        }

        var key = PrefsKeyPrefix + action.GetInstanceID();
        if (SessionState.GetBool(key, false))
        {
            return;
        }

        SessionState.SetBool(key, true);
        Debug.LogWarning(
            $"[ActionData][171.7] '{action.name}' 疑似 Idle 兜底资产但 Category={action.Category}；" +
            "建议改为 IdleFallback（FSM 兜底不走 ActionWindow 打断）。",
            action);
    }

    static bool LooksLikeIdleFallbackAsset(ActionDataSO action)
    {
        var name = action.name;
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        if (name.IndexOf("Idle", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return name.EndsWith("_Loop", System.StringComparison.OrdinalIgnoreCase)
               && action.IsContinuousLocomotion;
    }
}
#endif

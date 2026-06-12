#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>152.1 — Combat Flow Graph 指针/IMGUI 事件链调试（EditorPrefs 开关）。</summary>
public static class CombatFlowGraphInputDebug
{
    public const string PrefKey = "CombatFlowGraph.DebugInput";

    public static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, false);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    public static void Log(string message)
    {
        if (!Enabled)
        {
            return;
        }

        Debug.Log($"[CombatFlowGraph][Input] {message}");
    }

    public static string Describe(VisualElement element)
    {
        if (element == null)
        {
            return "(null)";
        }

        var name = string.IsNullOrEmpty(element.name) ? element.GetType().Name : $"{element.GetType().Name}#{element.name}";
        var pick = element.pickingMode;
        var pos = element.resolvedStyle.position;
        return $"{name} pick={pick} pos={pos}";
    }

    public static string DescribeTarget(Event evt)
    {
        if (evt == null)
        {
            return "(null)";
        }

        return $"{evt.type} btn={evt.button} mouse={evt.mousePosition}";
    }
}
#endif

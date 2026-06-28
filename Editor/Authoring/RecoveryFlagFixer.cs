#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 187.3 W0 — 资产层批量止血脚本：把所有命名匹配的"过渡 Action"补齐
/// IsLocomotionRecovery / TransitionType(End/Turn/Pivot) + Windows 全开 5 类。
/// <para>
/// 目的：BUG ① "WalkEnd 中 D+Space 完全不响应" 在 5 分钟内通过资产层修复，
/// 不依赖任何新框架。
/// </para>
/// </summary>
public static class RecoveryFlagFixer
{
    static readonly string[] s_EndPatterns =
    {
        "_Walk_End_", "_Run_End_", "_Land_", "_JumpLand_", "_RunEnd", "_WalkEnd",
    };

    static readonly string[] s_StartPatterns =
    {
        "_Walk_Start_", "_Run_Start_", "_RunStart", "_WalkStart",
    };

    static readonly string[] s_TurnPivotPatterns =
    {
        "_Turn", "_Pivot_", "_Pivot",
    };

    [MenuItem("GameMain/Action/Fix Recovery Flags on Transition Actions")]
    public static void FixAll()
    {
        var report = RunFix(dryRun: false);
        Debug.Log(report);
    }

    [MenuItem("GameMain/Action/Fix Recovery Flags (Dry Run)")]
    public static void DryRun()
    {
        var report = RunFix(dryRun: true);
        Debug.Log(report);
    }

    static string RunFix(bool dryRun)
    {
        var guids = AssetDatabase.FindAssets("t:ActionDataSO");
        var changed = 0;
        var scanned = 0;
        var log = new System.Text.StringBuilder();
        log.AppendLine($"[Recovery Flag Fixer] dryRun={dryRun}");

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            var kind = ClassifyByName(name);
            if (kind == TransitionKind.None) continue;
            scanned++;

            var a = AssetDatabase.LoadAssetAtPath<ActionDataSO>(path);
            if (a == null) continue;

            var dirty = false;
            var notes = new List<string>();

            // 1. TransitionType
            switch (kind)
            {
                case TransitionKind.End:
                    if (a.TransitionType != TransitionType.End)
                    {
                        if (!dryRun) a.TransitionType = TransitionType.End;
                        dirty = true; notes.Add("TransitionType→End");
                    }
                    break;
                case TransitionKind.Start:
                    if (a.TransitionType != TransitionType.Start)
                    {
                        if (!dryRun) a.TransitionType = TransitionType.Start;
                        dirty = true; notes.Add("TransitionType→Start");
                    }
                    break;
                case TransitionKind.Turn:
                    if (a.TransitionType != TransitionType.Turn)
                    {
                        if (!dryRun) a.TransitionType = TransitionType.Turn;
                        dirty = true; notes.Add("TransitionType→Turn");
                    }
                    break;
                case TransitionKind.Pivot:
                    if (a.TransitionType != TransitionType.Pivot)
                    {
                        if (!dryRun) a.TransitionType = TransitionType.Pivot;
                        dirty = true; notes.Add("TransitionType→Pivot");
                    }
                    break;
            }

            // 2. 补 Windows：全开 5 类（IdleFallback 不放行）
            if (a.Windows == null || a.Windows.Count == 0)
            {
                if (!dryRun)
                {
                    a.Windows = new List<ActionWindow>
                    {
                        new ActionWindow
                        {
                            NormalizedStart = 0f,
                            NormalizedEnd = 1f,
                            InterruptibleByCategories =
                                ActionCategory.Defensive |
                                ActionCategory.Offense |
                                ActionCategory.Movement |
                                ActionCategory.Utility |
                                ActionCategory.Locomotion,
                        },
                    };
                }
                dirty = true; notes.Add("加 Windows[0]=全开");
            }
            else
            {
                // 已有 Window：补 5 类掩码（如缺少 Defensive 等）
                for (var i = 0; i < a.Windows.Count; i++)
                {
                    var w = a.Windows[i];
                    var need = ActionCategory.Defensive | ActionCategory.Offense
                             | ActionCategory.Movement | ActionCategory.Utility | ActionCategory.Locomotion;
                    if ((w.InterruptibleByCategories & need) != need)
                    {
                        if (!dryRun)
                        {
                            w.InterruptibleByCategories |= need;
                            a.Windows[i] = w;
                        }
                        dirty = true; notes.Add($"Windows[{i}].Interruptible+=5类");
                    }
                }
            }

            if (dirty)
            {
                if (!dryRun) EditorUtility.SetDirty(a);
                changed++;
                log.AppendLine($"  ✓ {name}: {string.Join(", ", notes)}");
            }
        }

        if (!dryRun) AssetDatabase.SaveAssets();
        log.AppendLine();
        log.AppendLine($"Scanned: {scanned}   Changed: {changed}");
        return log.ToString();
    }

    enum TransitionKind { None, End, Start, Turn, Pivot }

    static TransitionKind ClassifyByName(string name)
    {
        foreach (var p in s_EndPatterns) if (name.Contains(p)) return TransitionKind.End;
        foreach (var p in s_StartPatterns) if (name.Contains(p)) return TransitionKind.Start;
        foreach (var p in s_TurnPivotPatterns)
        {
            if (name.Contains(p))
            {
                if (name.Contains("_Pivot")) return TransitionKind.Pivot;
                return TransitionKind.Turn;
            }
        }
        return TransitionKind.None;
    }
}
#endif

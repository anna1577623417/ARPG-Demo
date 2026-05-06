#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Phase 6：从 ActionDataSO 批量生成最小 SkillData（单 Stage、Instant），以及从若干 SkillData 组装连招根技能。
/// </summary>
public static class SkillMigrationTool
{
    const string MenuRoot = "Tools/Skill/";

    [MenuItem(MenuRoot + "Migrate Selected Action(s) → SkillStage + SkillData (Instant)", false, 10)]
    public static void MigrateSelectedActions()
    {
        var actions = CollectSelectedActions();
        if (actions.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Skill Migration",
                "在 Project 窗口选中一个或多个 ActionDataSO。",
                "OK");
            return;
        }

        var created = MigrateActions(actions, skipExisting: true);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Skill Migration] migrated Action → Skill assets count={created}");
    }

    [MenuItem(MenuRoot + "Migrate All ActionDataSO in Project → SkillStage + SkillData (skip existing)", false, 11)]
    public static void MigrateAllActionsInProject()
    {
        if (!EditorUtility.DisplayDialog(
                "Skill Migration",
                "将为工程中每个 ActionDataSO 生成并排的 *_SkillStage / *_SkillData（已存在则跳过）。继续？",
                "Yes",
                "Cancel"))
        {
            return;
        }

        var guids = AssetDatabase.FindAssets("t:ActionDataSO");
        var list = new List<ActionDataSO>(guids.Length);
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var a = AssetDatabase.LoadAssetAtPath<ActionDataSO>(path);
            if (a != null)
            {
                list.Add(a);
            }
        }

        var created = MigrateActions(list, skipExisting: true);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Skill Migration] batch migrated count={created} / scanned={list.Count}");
    }

    [MenuItem(MenuRoot + "Report ActionDataSO missing _SkillData peer (same folder)", false, 12)]
    public static void ReportMissingSkillPeers()
    {
        var guids = AssetDatabase.FindAssets("t:ActionDataSO");
        var missing = new List<string>();
        for (var i = 0; i < guids.Length; i++)
        {
            var actionPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            var dir = Path.GetDirectoryName(actionPath)?.Replace('\\', '/');
            var baseName = Path.GetFileNameWithoutExtension(actionPath);
            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(baseName))
            {
                continue;
            }

            var peer = $"{dir}/{baseName}_SkillData.asset";
            if (AssetDatabase.LoadAssetAtPath<SkillDataSO>(peer) == null)
            {
                missing.Add(actionPath);
            }
        }

        Debug.Log($"[Skill Migration] Action without *_SkillData peer: count={missing.Count}");
        foreach (var p in missing)
        {
            Debug.Log($"  – {p}");
        }
    }

    [MenuItem(MenuRoot + "Create Combo Root SkillData from Selected SkillData (order = combo chain)", false, 20)]
    public static void CreateComboRootFromSelection()
    {
        var skills = new List<SkillDataSO>();
        foreach (var obj in Selection.objects)
        {
            if (obj is SkillDataSO s && s != null)
            {
                skills.Add(s);
            }
        }

        if (skills.Count < 2)
        {
            EditorUtility.DisplayDialog(
                "Combo Root",
                "请按连招顺序多选至少 2 个 SkillDataSO（第一个为段 0）。",
                "OK");
            return;
        }

        var folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(skills[0]));
        var safeName = SanitizeFileName(skills[0].name + "_ComboRoot");
        var path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folder, safeName + ".asset"));

        var root = ScriptableObject.CreateInstance<SkillDataSO>();
        root.skillName = skills[0].name + " Combo";
        root.skillId = SanitizeId(root.skillName);
        root.castType = CastType.Instant;
        root.cdPolicy = CooldownPolicy.OnLastCast;
        root.baseCooldown = 0f;
        root.targetType = TargetType.Direction;
        root.comboResetTime = 1.2f;
        root.comboChain = skills.ToArray();

        AssetDatabase.CreateAsset(root, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = root;
        EditorGUIUtility.PingObject(root);
        Debug.Log($"[Skill Migration] combo root created at {path} | segments={skills.Count}");
    }

    static List<ActionDataSO> CollectSelectedActions()
    {
        var list = new List<ActionDataSO>();
        foreach (var obj in Selection.objects)
        {
            if (obj is ActionDataSO a)
            {
                list.Add(a);
            }
        }

        return list;
    }

    static int MigrateActions(List<ActionDataSO> actions, bool skipExisting)
    {
        var created = 0;
        for (var i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            if (action == null)
            {
                continue;
            }

            var actionPath = AssetDatabase.GetAssetPath(action);
            var dir = Path.GetDirectoryName(actionPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(dir))
            {
                continue;
            }

            var baseName = Path.GetFileNameWithoutExtension(actionPath);
            var stagePath = $"{dir}/{baseName}_SkillStage.asset";
            var skillPath = $"{dir}/{baseName}_SkillData.asset";

            if (skipExisting && AssetDatabase.LoadAssetAtPath<SkillDataSO>(skillPath) != null)
            {
                continue;
            }

            var stage = ScriptableObject.CreateInstance<SkillStageSO>();
            stage.name = $"{baseName}_Stage0";
            stage.action = action;
            stage.transitionType = StageTransitionType.Auto;

            AssetDatabase.CreateAsset(stage, stagePath);
            var stageRef = AssetDatabase.LoadAssetAtPath<SkillStageSO>(stagePath);

            var skill = ScriptableObject.CreateInstance<SkillDataSO>();
            skill.skillName = string.IsNullOrEmpty(action.name) ? baseName : action.name;
            skill.skillId = SanitizeId(skill.skillName);
            skill.castType = CastType.Instant;
            skill.cdPolicy = CooldownPolicy.OnLastCast;
            skill.baseCooldown = 0f;
            skill.targetType = TargetType.Direction;
            skill.maxRange = 8f;
            skill.stages = stageRef != null ? new[] { stageRef } : Array.Empty<SkillStageSO>();

            AssetDatabase.CreateAsset(skill, skillPath);
            created++;
        }

        return created;
    }

    static string SanitizeId(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "skill_" + Guid.NewGuid().ToString("N")[..8];
        }

        var sb = new StringBuilder(name.Length);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                sb.Append(char.ToLowerInvariant(c));
            }
            else if (c == ' ' || c == '-')
            {
                sb.Append('_');
            }
        }

        return sb.Length > 0 ? sb.ToString() : "skill_" + Guid.NewGuid().ToString("N")[..8];
    }

    static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        }

        return sb.Length > 0 ? sb.ToString() : "SkillComboRoot";
    }
}
#endif

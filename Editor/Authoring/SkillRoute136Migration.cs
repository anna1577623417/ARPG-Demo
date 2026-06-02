//#if UNITY_EDITOR
//using System.Collections.Generic;
//using System.IO;
//using UnityEditor;
//using UnityEngine;

///// <summary>
///// 136.1 — 清理已废弃的 DirectionalRouteSet 资产（类型已删除；四向由 SkillGroup 承担）。
///// </summary>
//public static class SkillRoute136Migration
//{
//    const string LegacyDirectionalScriptGuid = "1854b95f92707f94a83a3bf8ea6f1caf";

//    const string LoadoutPath = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/Loadout_Skill_C1.asset";
//    const string ContextGroupPath =
//        "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/SkillUnit/ContextGroup_StandingDodge_Space.asset";
//    const string AbilityMapPath =
//        "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/SkillUnit/AbilityMap_C1.asset";
//    const string MobilityRulePath =
//        "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/SkillUnit/AbilityGateRule_MobilityGrounded.asset";
//    const string GroupPath =
//        "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/SkillUnit/Group_Standing_Dodge.asset";

//    [MenuItem("Tools/Skill/136.1 Purge Legacy DirectionalRouteSet Assets")]
//    public static void PurgeLegacyDirectionalRouteSetAssets()
//    {
//        var legacyPaths = FindLegacyDirectionalAssetPaths();
//        var migrated = 0;
//        for (var i = 0; i < legacyPaths.Count; i++)
//        {
//            var legacyGuid = AssetDatabase.AssetPathToGUID(legacyPaths[i]);
//            if (TryMigrateLegacyAssetAtPath(legacyPaths[i], out var group))
//            {
//                WireEntriesToGroup(legacyGuid, group);
//                migrated++;
//            }

//            if (AssetDatabase.DeleteAsset(legacyPaths[i]))
//            {
//                Debug.Log($"[SkillRoute][136.1] Deleted legacy asset: {legacyPaths[i]}");
//            }
//        }

//        AssetDatabase.SaveAssets();
//        AssetDatabase.Refresh();
//        Debug.Log($"[SkillRoute][136.1] Purge complete migrated={migrated} deleted={legacyPaths.Count}");
//    }

//    [MenuItem("Tools/Skill/136.1 Setup C1 Loadout (Ability+Context+Flow)")]
//    public static void SetupC1LoadoutFromMenu()
//    {
//        var loadout = AssetDatabase.LoadAssetAtPath<SkillEntryLoadoutSO>(LoadoutPath);
//        if (loadout == null)
//        {
//            Debug.LogError($"[SkillRoute][136.1] Loadout 缺失: {LoadoutPath}");
//            return;
//        }

//        SetupC1LoadoutAssembly(loadout);
//        WireC1StandingDodgeRoutesAndMotion();
//        AssetDatabase.SaveAssets();
//        Debug.Log("[SkillRoute][136.1] C1 Loadout Ability+Context 装配完成");
//    }

//    [MenuItem("Tools/Skill/136.3 Wire C1 Dodge (Route Gate + Motion Frame)")]
//    public static void WireC1DodgeFromMenu()
//    {
//        WireC1StandingDodgeRoutesAndMotion();
//        AssetDatabase.SaveAssets();
//        Debug.Log("[SkillRoute][136.3] C1 四向闪避 Route 准入 + Motion CharacterForward 已写入");
//    }

//    public static void SetupC1LoadoutAssembly(SkillEntryLoadoutSO loadout)
//    {
//        if (loadout == null)
//        {
//            return;
//        }

//        var rule = LoadOrCreate<AbilityGateRuleSO>(MobilityRulePath);
//        var soRule = new SerializedObject(rule);
//        soRule.FindProperty("ability").enumValueIndex = (int)AbilitySemantic.Mobility;
//        soRule.FindProperty("requireGrounded").boolValue = true;
//        soRule.FindProperty("requireAirborne").boolValue = false;
//        soRule.FindProperty("allowWhenGrounded").boolValue = true;
//        soRule.FindProperty("allowWhenAirborne").boolValue = false;
//        soRule.ApplyModifiedPropertiesWithoutUndo();

//        var abilityMap = LoadOrCreate<AbilityMapSO>(AbilityMapPath);
//        var soMap = new SerializedObject(abilityMap);
//        soMap.FindProperty("bindings").arraySize = 0;
//        soMap.ApplyModifiedPropertiesWithoutUndo();

//        var group = AssetDatabase.LoadAssetAtPath<SkillGroupDefinition>(GroupPath);
//        var ctxGroup = LoadOrCreate<SkillContextGroupDefinition>(ContextGroupPath);
//        var soCtx = new SerializedObject(ctxGroup);
//        soCtx.FindProperty("targetGroup").objectReferenceValue = group;
//        soCtx.FindProperty("requiredSlot").enumValueIndex = (int)SkillEntrySlot.Space;
//        soCtx.FindProperty("requireDirectional").boolValue = false;
//        soCtx.FindProperty("requireGrounded").boolValue = true;
//        soCtx.FindProperty("requireAirborne").boolValue = false;
//        soCtx.FindProperty("requiredMoveDirection").enumValueIndex = (int)MoveDirection8.None;
//        soCtx.FindProperty("priority").intValue = 0;
//        soCtx.ApplyModifiedPropertiesWithoutUndo();

//        var soLoadout = new SerializedObject(loadout);
//        soLoadout.FindProperty("abilityMap").objectReferenceValue = abilityMap;
//        soLoadout.FindProperty("contextGroups").arraySize = 1;
//        soLoadout.FindProperty("contextGroups").GetArrayElementAtIndex(0).objectReferenceValue = ctxGroup;
//        soLoadout.ApplyModifiedPropertiesWithoutUndo();

//        EditorUtility.SetDirty(loadout);
//        EditorUtility.SetDirty(abilityMap);
//        EditorUtility.SetDirty(ctxGroup);
//        EditorUtility.SetDirty(rule);
//    }

//    static void WireC1StandingDodgeRoutesAndMotion()
//    {
//        var rule = LoadOrCreate<AbilityGateRuleSO>(MobilityRulePath);
//        var group = AssetDatabase.LoadAssetAtPath<SkillGroupDefinition>(GroupPath);
//        if (group == null)
//        {
//            Debug.LogWarning($"[SkillRoute][136.3] Group 缺失: {GroupPath}");
//            return;
//        }

//        WireRouteGate(group.Forward, rule);
//        WireRouteGate(group.Backward, rule);
//        WireRouteGate(group.Left, rule);
//        WireRouteGate(group.Right, rule);

//        WireMotionFrame(group.Forward);
//        WireMotionFrame(group.Backward);
//        WireMotionFrame(group.Left);
//        WireMotionFrame(group.Right);
//    }

//    static void WireRouteGate(NormalRouteDefinition route, AbilityGateRuleSO rule)
//    {
//        if (route == null || rule == null)
//        {
//            return;
//        }

//        var so = new SerializedObject(route);
//        var gates = so.FindProperty("abilityGateRules");
//        gates.arraySize = 1;
//        gates.GetArrayElementAtIndex(0).objectReferenceValue = rule;
//        so.ApplyModifiedPropertiesWithoutUndo();
//        EditorUtility.SetDirty(route);
//    }

//    static void WireMotionFrame(SkillRouteDefinition route)
//    {
//        if (route?.FirstStage()?.Action?.MotionProfile == null)
//        {
//            return;
//        }

//        var profile = route.FirstStage().Action.MotionProfile;
//        var so = new SerializedObject(profile);
//        so.FindProperty("DisplacementFrame").enumValueIndex = (int)MotionDisplacementFrame.CharacterForward;
//        so.ApplyModifiedPropertiesWithoutUndo();
//        EditorUtility.SetDirty(profile);
//    }

//    static List<string> FindLegacyDirectionalAssetPaths()
//    {
//        var list = new List<string>();
//        var guids = AssetDatabase.FindAssets("t:ScriptableObject");
//        for (var i = 0; i < guids.Length; i++)
//        {
//            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
//            if (!path.EndsWith(".asset"))
//            {
//                continue;
//            }

//            if (!File.ReadAllText(path).Contains(LegacyDirectionalScriptGuid))
//            {
//                continue;
//            }

//            list.Add(path);
//        }

//        return list;
//    }

//    static bool TryMigrateLegacyAssetAtPath(string legacyPath, out SkillGroupDefinition group)
//    {
//        group = null;
//        var obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(legacyPath);
//        if (obj == null)
//        {
//            return false;
//        }

//        var so = new SerializedObject(obj);
//        var fwd = so.FindProperty("forward").objectReferenceValue as SkillRouteDefinition;
//        var back = so.FindProperty("backward").objectReferenceValue as SkillRouteDefinition;
//        var left = so.FindProperty("left").objectReferenceValue as SkillRouteDefinition;
//        var right = so.FindProperty("right").objectReferenceValue as SkillRouteDefinition;
//        if (fwd == null)
//        {
//            Debug.LogWarning($"[SkillRoute][136.1] SKIP migrate (no forward): {legacyPath}");
//            return false;
//        }

//        var groupPath = legacyPath
//            .Replace("Route_Directional_", "Group_")
//            .Replace("/Routes/", "/SkillUnit/")
//            .Replace(".asset", "_Group.asset");
//        if (groupPath == legacyPath)
//        {
//            groupPath = Path.Combine(Path.GetDirectoryName(legacyPath) ?? legacyPath, "Group_Migrated.asset")
//                .Replace('\\', '/');
//        }

//        group = SkillGroupFourDirEditorUtil.CreateOrUpdateFourDirGroup(
//            groupPath,
//            obj.name,
//            1f,
//            fwd as NormalRouteDefinition,
//            back as NormalRouteDefinition,
//            left as NormalRouteDefinition,
//            right as NormalRouteDefinition);
//        return group != null;
//    }

//    static void WireEntriesToGroup(string legacyAssetGuid, SkillGroupDefinition group)
//    {
//        if (group == null || string.IsNullOrEmpty(legacyAssetGuid))
//        {
//            return;
//        }

//        var entryGuids = AssetDatabase.FindAssets("t:SkillEntryDefinition");
//        for (var i = 0; i < entryGuids.Length; i++)
//        {
//            var path = AssetDatabase.GUIDToAssetPath(entryGuids[i]);
//            var entry = AssetDatabase.LoadAssetAtPath<SkillEntryDefinition>(path);
//            if (entry == null || !File.ReadAllText(path).Contains(legacyAssetGuid))
//            {
//                continue;
//            }

//            SkillGroupFourDirEditorUtil.BindEntryPrimaryGroup(entry, group);
//            Debug.Log($"[SkillRoute][136.1] Entry {entry.name} → primaryUnit={group.name}", entry);
//        }
//    }

//    static T LoadOrCreate<T>(string path) where T : ScriptableObject
//    {
//        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
//        if (asset != null)
//        {
//            return asset;
//        }

//        var dir = Path.GetDirectoryName(path);
//        if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
//        {
//            EnsureFolderPath(dir);
//        }

//        asset = ScriptableObject.CreateInstance<T>();
//        AssetDatabase.CreateAsset(asset, path);
//        return asset;
//    }

//    static void EnsureFolderPath(string path)
//    {
//        var parts = path.Replace('\\', '/').Split('/');
//        var current = parts[0];
//        for (var i = 1; i < parts.Length - 1; i++)
//        {
//            var next = current + "/" + parts[i];
//            if (!AssetDatabase.IsValidFolder(next))
//            {
//                AssetDatabase.CreateFolder(current, parts[i]);
//            }

//            current = next;
//        }
//    }
//}
//#endif

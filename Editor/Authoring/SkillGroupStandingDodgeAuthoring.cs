#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 四向站立翻滚 → SkillGroup（组内四向）+ Entry_Space.primaryUnit。
/// </summary>
public static class SkillGroupStandingDodgeAuthoring
{
    const string GroupPath = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/SkillUnit/Group_Standing_Dodge.asset";
    const string EntryPath = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/Entry_Space.asset";
    const string LoadoutPath = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/Loadout_Skill_C1.asset";

    const string FwdPath = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/SkillUnit/Route_Normal_Standing Dodge Forward.asset";
    const string BackPath = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/SkillUnit/Route_Normal_Standing Dodge Backward.asset";
    const string LeftPath = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/SkillUnit/Route_Normal_Standing Dodge Left.asset";
    const string RightPath = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/SkillUnit/Route_Normal_Standing Dodge Right.asset";

    [MenuItem("Tools/Skill/136.1 Setup Standing Dodge SkillGroup (4 dir)")]
    public static void SetupStandingDodgeGroup()
    {
        SetupFourDirGroup(
            GroupPath,
            "Standing Dodge",
            1f,
            FwdPath,
            BackPath,
            LeftPath,
            RightPath,
            bindEntry: true,
            wireLoadoutFlow: false);
        AssetDatabase.Refresh();
    }

    const string WuxiaGroupPath = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/SkillUnit/Group_WuxiaRoll_4dir.asset";
    const string WuxiaFwd = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/SkillUnit/Route_Normal_Stage_Wuxia_roll_front.asset";
    const string WuxiaBack = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/SkillUnit/Route_Normal_Stage_Wuxia_roll_back.asset";
    const string WuxiaLeft = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/SkillUnit/Route_Normal_Stage_Wuxia_roll_left.asset";
    const string WuxiaRight = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/SkillUnit/Route_Normal_Stage_Wuxia_roll_right.asset";

    [MenuItem("Tools/Skill/136.1 Setup Wuxia Roll SkillGroup (4 dir)")]
    public static void SetupWuxiaRollGroup()
    {
        SetupFourDirGroup(
            WuxiaGroupPath,
            "Wuxia Roll",
            1f,
            WuxiaFwd,
            WuxiaBack,
            WuxiaLeft,
            WuxiaRight,
            bindEntry: false,
            wireLoadoutFlow: false);
    }

    static void SetupFourDirGroup(
        string groupPath,
        string displayName,
        float cd,
        string fwdPath,
        string backPath,
        string leftPath,
        string rightPath,
        bool bindEntry,
        bool wireLoadoutFlow)
    {
        var fwd = AssetDatabase.LoadAssetAtPath<NormalRouteDefinition>(fwdPath);
        var back = AssetDatabase.LoadAssetAtPath<NormalRouteDefinition>(backPath);
        var left = AssetDatabase.LoadAssetAtPath<NormalRouteDefinition>(leftPath);
        var right = AssetDatabase.LoadAssetAtPath<NormalRouteDefinition>(rightPath);
        if (fwd == null || back == null || left == null || right == null)
        {
            Debug.LogError($"[SkillGroup] Route 缺失: {groupPath}");
            return;
        }

        var group = AssetDatabase.LoadAssetAtPath<SkillGroupDefinition>(groupPath)
                    ?? CreateAsset<SkillGroupDefinition>(groupPath);
        var soGroup = new SerializedObject(group);
        soGroup.FindProperty("displayName").stringValue = displayName;
        soGroup.FindProperty("baseCooldownSeconds").floatValue = cd;
        soGroup.FindProperty("forward").objectReferenceValue = fwd;
        soGroup.FindProperty("backward").objectReferenceValue = back;
        soGroup.FindProperty("left").objectReferenceValue = left;
        soGroup.FindProperty("right").objectReferenceValue = right;
        soGroup.FindProperty("defaultToForwardWhenNeutral").boolValue = true;
        soGroup.FindProperty("fallbackRoute").objectReferenceValue = fwd;
        soGroup.ApplyModifiedPropertiesWithoutUndo();

        foreach (var r in new[] { fwd, back, left, right })
        {
            var soR = new SerializedObject(r);
            soR.FindProperty("ownerGroup").objectReferenceValue = group;
            soR.FindProperty("overrideCooldown").boolValue = false;
            soR.FindProperty("baseCooldownSeconds").floatValue = 0f;
            soR.FindProperty("showOnHud").boolValue = false;
            soR.ApplyModifiedPropertiesWithoutUndo();
        }

        if (bindEntry)
        {
            var entry = AssetDatabase.LoadAssetAtPath<SkillEntryDefinition>(EntryPath);
            if (entry != null)
            {
                var soEntry = new SerializedObject(entry);
                soEntry.FindProperty("primaryUnit").objectReferenceValue = group;
                soEntry.FindProperty("normalRoute").objectReferenceValue = null;
                soEntry.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        if (wireLoadoutFlow)
        {
            Debug.LogWarning("[SkillGroup] wireLoadoutFlow 已弃用；请在 Loadout Inspector 手动绑定 combatFlow 并 Validate & Compile。");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[SkillGroup] 136.1 {displayName} → Group 四向 + Entry 完成");
    }

    static T CreateAsset<T>(string path) where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
#endif

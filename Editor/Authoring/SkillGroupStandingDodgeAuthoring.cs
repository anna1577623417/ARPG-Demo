#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 四向站立翻滚 → SkillGroup + DirectionalRouteSet + Entry_Space 接线。
/// </summary>
public static class SkillGroupStandingDodgeAuthoring
{
    const string GroupPath = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/Group/Group_StandingDodge_4dir.asset";
    const string DirPath = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/Route/Route_Directional_StandingDodge.asset";
    const string EntryPath = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/Entry_Space.asset";

    const string FwdPath = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/Route/Route_Normal_Standing Dodge Forward.asset";
    const string BackPath = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/Route/Route_Normal_Standing Dodge Backward.asset";
    const string LeftPath = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/Route/Route_Normal_Standing Dodge Left.asset";
    const string RightPath = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/Route/Route_Normal_Standing Dodge Right.asset";

    [MenuItem("Tools/Skill/Setup Standing Dodge SkillGroup (4 dir)")]
    public static void SetupStandingDodgeGroup()
    {
        SetupFourDirGroup(
            GroupPath,
            DirPath,
            "站立翻滚",
            1f,
            FwdPath,
            BackPath,
            LeftPath,
            RightPath,
            bindEntry: true);
        AssetDatabase.Refresh();
    }

    const string WuxiaGroupPath = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/Group/Group_WuxiaRoll_4dir.asset";
    const string WuxiaDirPath = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/Route/Route_Directional_WuxiaRoll.asset";
    const string WuxiaFwd = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/Route/Route_Normal_Stage_Wuxia_roll_front.asset";
    const string WuxiaBack = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/Route/Route_Normal_Stage_Wuxia_roll_back.asset";
    const string WuxiaLeft = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/Route/Route_Normal_Stage_Wuxia_roll_left.asset";
    const string WuxiaRight = "Assets/GameMain/Scripts/4_Data/1.Skills/C1_Default_Skill/Route/Route_Normal_Stage_Wuxia_roll_right.asset";

    [MenuItem("Tools/Skill/Setup Wuxia Roll SkillGroup (4 dir)")]
    public static void SetupWuxiaRollGroup()
    {
        SetupFourDirGroup(
            WuxiaGroupPath,
            WuxiaDirPath,
            "武侠四向翻滚",
            1f,
            WuxiaFwd,
            WuxiaBack,
            WuxiaLeft,
            WuxiaRight,
            bindEntry: false);
    }

    static void SetupFourDirGroup(
        string groupPath,
        string dirPath,
        string displayName,
        float cd,
        string fwdPath,
        string backPath,
        string leftPath,
        string rightPath,
        bool bindEntry)
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
        soGroup.FindProperty("routes").arraySize = 4;
        soGroup.FindProperty("routes").GetArrayElementAtIndex(0).objectReferenceValue = fwd;
        soGroup.FindProperty("routes").GetArrayElementAtIndex(1).objectReferenceValue = back;
        soGroup.FindProperty("routes").GetArrayElementAtIndex(2).objectReferenceValue = left;
        soGroup.FindProperty("routes").GetArrayElementAtIndex(3).objectReferenceValue = right;
        soGroup.FindProperty("fallbackRoute").objectReferenceValue = fwd;
        soGroup.ApplyModifiedPropertiesWithoutUndo();

        var dir = AssetDatabase.LoadAssetAtPath<DirectionalRouteSet>(dirPath)
                  ?? CreateAsset<DirectionalRouteSet>(dirPath);
        var soDir = new SerializedObject(dir);
        soDir.FindProperty("forward").objectReferenceValue = fwd;
        soDir.FindProperty("backward").objectReferenceValue = back;
        soDir.FindProperty("left").objectReferenceValue = left;
        soDir.FindProperty("right").objectReferenceValue = right;
        soDir.FindProperty("showOnHud").boolValue = false;
        soDir.ApplyModifiedPropertiesWithoutUndo();

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
                soEntry.FindProperty("directionalRoute").objectReferenceValue = dir;
                soEntry.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[SkillGroup] {displayName} → Group + Directional 完成");
    }

    static T CreateAsset<T>(string path) where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
#endif

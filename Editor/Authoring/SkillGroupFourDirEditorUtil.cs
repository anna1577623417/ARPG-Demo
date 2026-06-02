#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor 共用：创建/更新四向 SkillGroup 并绑定 Entry.primaryUnit。
/// </summary>
public static class SkillGroupFourDirEditorUtil
{
    public static SkillGroupDefinition CreateOrUpdateFourDirGroup(
        string groupPath,
        string displayName,
        float groupCooldownSeconds,
        NormalRouteDefinition forward,
        NormalRouteDefinition backward,
        NormalRouteDefinition left,
        NormalRouteDefinition right,
        bool defaultToForwardWhenNeutral = true)
    {
        if (forward == null)
        {
            Debug.LogError($"[SkillGroup] forward 为空: {groupPath}");
            return null;
        }

        var group = AssetDatabase.LoadAssetAtPath<SkillGroupDefinition>(groupPath)
                    ?? CreateAsset<SkillGroupDefinition>(groupPath);

        var soGroup = new SerializedObject(group);
        soGroup.FindProperty("displayName").stringValue = displayName;
        soGroup.FindProperty("baseCooldownSeconds").floatValue = groupCooldownSeconds;
        soGroup.FindProperty("forward").objectReferenceValue = forward;
        soGroup.FindProperty("backward").objectReferenceValue = backward != null ? backward : forward;
        soGroup.FindProperty("left").objectReferenceValue = left != null ? left : forward;
        soGroup.FindProperty("right").objectReferenceValue = right != null ? right : forward;
        soGroup.FindProperty("defaultToForwardWhenNeutral").boolValue = defaultToForwardWhenNeutral;
        soGroup.FindProperty("fallbackRoute").objectReferenceValue = forward;
        soGroup.ApplyModifiedPropertiesWithoutUndo();

        foreach (var r in new[] { forward, backward, left, right })
        {
            if (r == null)
            {
                continue;
            }

            var soR = new SerializedObject(r);
            soR.FindProperty("ownerGroup").objectReferenceValue = group;
            soR.ApplyModifiedPropertiesWithoutUndo();
        }

        return group;
    }

    public static void BindEntryPrimaryGroup(SkillEntryDefinition entry, SkillGroupDefinition group, NormalRouteDefinition normalFallback = null)
    {
        if (entry == null)
        {
            return;
        }

        var soEntry = new SerializedObject(entry);
        soEntry.FindProperty("primaryUnit").objectReferenceValue = group;
        if (normalFallback != null)
        {
            soEntry.FindProperty("normalRoute").objectReferenceValue = normalFallback;
        }

        soEntry.ApplyModifiedPropertiesWithoutUndo();
    }

    static T CreateAsset<T>(string path) where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
#endif

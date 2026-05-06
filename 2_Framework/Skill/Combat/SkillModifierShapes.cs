using UnityEngine;

/// <summary>
/// 判定 / 指示器 / 投放物共用：<see cref="SkillModifiers"/> 对尺寸的缩放（蓝图 5.12）。
/// </summary>
public static class SkillModifierShapes
{
    public static float ScaleCollisionRadius(float baseRadius, in SkillModifiers mods)
    {
        return (baseRadius + mods.RadiusAdd) * Mathf.Max(0f, mods.RadiusScale);
    }

    public static float ScaleMaxRange(float baseMaxRange, in SkillModifiers mods)
    {
        return (baseMaxRange + mods.RadiusAdd) * Mathf.Max(0f, mods.RadiusScale);
    }
}
